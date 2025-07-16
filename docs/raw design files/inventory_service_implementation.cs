// InventoryService.cs - Complete inventory management implementation
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

namespace Instrument.Scheduler.Services
{
    public class InventoryService : IInventoryService, IHostedService, IDisposable
    {
        private readonly ConcurrentDictionary<string, InventoryArticle> _inventory;
        private readonly ConcurrentDictionary<Guid, InventoryReservation> _reservations;
        private readonly IInventoryRepository _repository;
        private readonly ILogger<InventoryService> _logger;
        private readonly Timer _expirationTimer;
        private readonly Timer _cleanupTimer;
        private readonly SemaphoreSlim _operationSemaphore;

        public event EventHandler<InventoryChangedEventArgs> OnInventoryChanged;
        public event EventHandler<ItemExpiringEventArgs> OnItemExpiring;
        public event EventHandler<ItemExpiredEventArgs> OnItemExpired;

        public InventoryService(
            IInventoryRepository repository,
            ILogger<InventoryService> logger)
        {
            _inventory = new ConcurrentDictionary<string, InventoryArticle>();
            _reservations = new ConcurrentDictionary<Guid, InventoryReservation>();
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _operationSemaphore = new SemaphoreSlim(1, 1);

            // Set up monitoring timers
            _expirationTimer = new Timer(CheckExpirations, null, 
                TimeSpan.FromHours(1), TimeSpan.FromHours(1));
            _cleanupTimer = new Timer(CleanupExpiredReservations, null,
                TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30));
        }

        public async Task<InventoryCheckResult> CheckAvailability(List<InventoryRequirement> requirements)
        {
            if (requirements == null) throw new ArgumentNullException(nameof(requirements));

            var result = new InventoryCheckResult(true);
            var consolidatedRequirements = ConsolidateRequirements(requirements);

            foreach (var requirement in consolidatedRequirements)
            {
                try
                {
                    var availability = await CheckSingleRequirement(requirement);
                    
                    if (!availability.IsAvailable)
                    {
                        result.IsAvailable = false;
                        result.AddMissingItem(requirement);
                        
                        if (!string.IsNullOrEmpty(availability.Reason))
                        {
                            result.AddError(requirement.ArticleId, availability.Reason);
                        }
                    }
                }
                catch (Exception ex)
                {
                    result.IsAvailable = false;
                    result.AddError(requirement.ArticleId, ex.Message);
                    _logger.LogError(ex, "Error checking availability for {ArticleId}", requirement.ArticleId);
                }
            }

            _logger.LogInformation("Inventory check completed: {IsAvailable}, {MissingCount} missing items", 
                result.IsAvailable, result.MissingItems.Count);

            return result;
        }

        public async Task<bool> ReserveInventory(List<InventoryRequirement> requirements)
        {
            if (requirements == null) throw new ArgumentNullException(nameof(requirements));

            await _operationSemaphore.WaitAsync();
            try
            {
                // First, validate all requirements can be satisfied
                var checkResult = await CheckAvailability(requirements);
                if (!checkResult.IsAvailable)
                {
                    _logger.LogWarning("Cannot reserve inventory - insufficient items: {MissingItems}",
                        string.Join(", ", checkResult.MissingItems.Select(i => $"{i.ArticleId}:{i.Quantity}")));
                    return false;
                }

                var reservations = new List<InventoryReservation>();
                var consolidatedRequirements = ConsolidateRequirements(requirements);

                // Create reservations
                foreach (var requirement in consolidatedRequirements)
                {
                    if (_inventory.TryGetValue(requirement.ArticleId, out var article))
                    {
                        if (article.Reserve(requirement.Quantity))
                        {
                            var reservation = new InventoryReservation(
                                requirement.ArticleId, 
                                requirement.Quantity, 
                                requirement.AssaySampleId ?? Guid.Empty);
                            
                            reservations.Add(reservation);
                            _reservations.TryAdd(reservation.Id, reservation);
                            
                            _logger.LogDebug("Reserved {Quantity} of {ArticleId}", 
                                requirement.Quantity, requirement.ArticleId);
                        }
                        else
                        {
                            // Rollback previous reservations
                            await RollbackReservations(reservations);
                            return false;
                        }
                    }
                    else
                    {
                        // Rollback previous reservations
                        await RollbackReservations(reservations);
                        _logger.LogError("Article {ArticleId} not found in inventory", requirement.ArticleId);
                        return false;
                    }
                }

                // Persist reservations
                foreach (var reservation in reservations)
                {
                    await _repository.SaveReservation(reservation);
                }

                _logger.LogInformation("Successfully reserved inventory for {RequirementCount} requirements", 
                    consolidatedRequirements.Count);

                // Notify listeners
                OnInventoryChanged?.Invoke(this, new InventoryChangedEventArgs(
                    InventoryChangeType.Reserved, requirements));

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reserve inventory");
                return false;
            }
            finally
            {
                _operationSemaphore.Release();
            }
        }

        public async Task<bool> ReleaseInventory(List<InventoryRequirement> requirements)
        {
            if (requirements == null) throw new ArgumentNullException(nameof(requirements));

            await _operationSemaphore.WaitAsync();
            try
            {
                var consolidatedRequirements = ConsolidateRequirements(requirements);
                var releasedCount = 0;

                foreach (var requirement in consolidatedRequirements)
                {
                    // Find matching reservations
                    var matchingReservations = _reservations.Values
                        .Where(r => r.ArticleId == requirement.ArticleId && 
                                   r.Status == ReservationStatus.Active)
                        .OrderBy(r => r.ReservedAt)
                        .ToList();

                    var remainingToRelease = requirement.Quantity;

                    foreach (var reservation in matchingReservations)
                    {
                        if (remainingToRelease <= 0) break;

                        var releaseQuantity = Math.Min(remainingToRelease, reservation.Quantity);
                        
                        if (_inventory.TryGetValue(requirement.ArticleId, out var article))
                        {
                            article.Release(releaseQuantity);
                            
                            if (releaseQuantity == reservation.Quantity)
                            {
                                // Release entire reservation
                                reservation.Release();
                                await _repository.SaveReservation(reservation);
                            }
                            else
                            {
                                // Partial release - create new reservation for remaining
                                reservation.Quantity -= releaseQuantity;
                                await _repository.SaveReservation(reservation);
                            }

                            remainingToRelease -= releaseQuantity;
                            releasedCount++;
                            
                            _logger.LogDebug("Released {Quantity} of {ArticleId}", 
                                releaseQuantity, requirement.ArticleId);
                        }
                    }

                    if (remainingToRelease > 0)
                    {
                        _logger.LogWarning("Could not release all requested inventory for {ArticleId}: {Remaining} remaining",
                            requirement.ArticleId, remainingToRelease);
                    }
                }

                _logger.LogInformation("Released inventory for {ReleasedCount} items", releasedCount);

                // Notify listeners
                OnInventoryChanged?.Invoke(this, new InventoryChangedEventArgs(
                    InventoryChangeType.Released, requirements));

                return releasedCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to release inventory");
                return false;
            }
            finally
            {
                _operationSemaphore.Release();
            }
        }

        public async Task<bool> UpdateInventory(InventoryArticle article, int quantityChange)
        {
            if (article == null) throw new ArgumentNullException(nameof(article));

            await _operationSemaphore.WaitAsync();
            try
            {
                if (_inventory.TryGetValue(article.Id, out var existingArticle))
                {
                    // Update existing article
                    var newQuantity = existingArticle.Quantity + quantityChange;
                    if (newQuantity < 0)
                    {
                        _logger.LogWarning("Cannot update inventory for {ArticleId}: would result in negative quantity", 
                            article.Id);
                        return false;
                    }

                    existingArticle.Quantity = newQuantity;
                    existingArticle.LastUpdated = DateTime.UtcNow;
                    
                    // Update status based on quantity
                    if (newQuantity == 0)
                    {
                        existingArticle.Status = ArticleStatus.OutOfStock;
                    }
                    else if (existingArticle.Status == ArticleStatus.OutOfStock)
                    {
                        existingArticle.Status = ArticleStatus.Available;
                    }
                }
                else
                {
                    // Add new article
                    article.LastUpdated = DateTime.UtcNow;
                    _inventory.TryAdd(article.Id, article);
                }

                // Persist changes
                await _repository.SaveArticle(article);

                _logger.LogInformation("Updated inventory for {ArticleId}: {QuantityChange} (total: {Total})",
                    article.Id, quantityChange, article.Quantity);

                // Notify listeners
                OnInventoryChanged?.Invoke(this, new InventoryChangedEventArgs(
                    quantityChange > 0 ? InventoryChangeType.Added : InventoryChangeType.Consumed,
                    new List<InventoryRequirement> { new(article.Type.ToString(), article.Id, Math.Abs(quantityChange)) }));

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update inventory for {ArticleId}", article.Id);
                return false;
            }
            finally
            {
                _operationSemaphore.Release();
            }
        }

        public InventoryStatus GetInventoryStatus()
        {
            var articles = _inventory.Values.ToList();
            var reservations = _reservations.Values.Where(r => r.IsActive()).ToList();

            return new InventoryStatus
            {
                TotalArticles = articles.Count,
                TotalQuantity = articles.Sum(a => a.Quantity),
                AvailableQuantity = articles.Sum(a => a.Quantity - a.ReservedQuantity),
                ReservedQuantity = articles.Sum(a => a.ReservedQuantity),
                ActiveReservations = reservations.Count,
                OutOfStockItems = articles.Count(a => a.Status == ArticleStatus.OutOfStock),
                ExpiringItems = articles.Count(a => a.IsExpiringSoon(7)),
                ExpiredItems = articles.Count(a => a.IsExpired()),
                StatusDistribution = articles.GroupBy(a => a.Status).ToDictionary(g => g.Key, g => g.Count())
            };
        }

        public List<InventoryArticle> GetExpiringItems(int days)
        {
            return _inventory.Values
                .Where(article => article.IsExpiringSoon(days))
                .OrderBy(article => article.ExpirationDate)
                .ToList();
        }

        public List<InventoryArticle> GetOutOfStockItems()
        {
            return _inventory.Values
                .Where(article => article.Status == ArticleStatus.OutOfStock)
                .ToList();
        }

        public List<InventoryArticle> GetLowStockItems(int threshold)
        {
            return _inventory.Values
                .Where(article => article.Quantity <= threshold && article.Quantity > 0)
                .OrderBy(article => article.Quantity)
                .ToList();
        }

        public async Task<bool> LoadInventoryFromRepository()
        {
            try
            {
                var articles = await _repository.GetAllArticles();
                var reservations = await _repository.GetActiveReservations();

                _inventory.Clear();
                _reservations.Clear();

                foreach (var article in articles)
                {
                    _inventory.TryAdd(article.Id, article);
                }

                foreach (var reservation in reservations)
                {
                    _reservations.TryAdd(reservation.Id, reservation);
                    
                    // Restore reserved quantities
                    if (_inventory.TryGetValue(reservation.ArticleId, out var article))
                    {
                        article.ReservedQuantity += reservation.Quantity;
                    }
                }

                _logger.LogInformation("Loaded {ArticleCount} articles and {ReservationCount} reservations from repository",
                    articles.Count, reservations.Count);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load inventory from repository");
                return false;
            }
        }

        private async Task<ItemAvailability> CheckSingleRequirement(InventoryRequirement requirement)
        {
            if (!_inventory.TryGetValue(requirement.ArticleId, out var article))
            {
                return new ItemAvailability(false, $"Article {requirement.ArticleId} not found");
            }

            if (article.IsExpired())
            {
                return new ItemAvailability(false, $"Article {requirement.ArticleId} is expired");
            }

            if (article.Status != ArticleStatus.Available)
            {
                return new ItemAvailability(false, $"Article {requirement.ArticleId} is not available: {article.Status}");
            }

            if (!article.CanReserve(requirement.Quantity))
            {
                return new ItemAvailability(false, 
                    $"Insufficient quantity for {requirement.ArticleId}: requested {requirement.Quantity}, available {article.Quantity - article.ReservedQuantity}");
            }

            return new ItemAvailability(true);
        }

        private List<InventoryRequirement> ConsolidateRequirements(List<InventoryRequirement> requirements)
        {
            return requirements
                .GroupBy(r => r.ArticleId)
                .Select(g => new InventoryRequirement(
                    g.First().ArticleType,
                    g.Key,
                    g.Sum(r => r.Quantity))
                {
                    AssaySampleId = g.First().AssaySampleId
                })
                .ToList();
        }

        private async Task RollbackReservations(List<InventoryReservation> reservations)
        {
            foreach (var reservation in reservations)
            {
                if (_inventory.TryGetValue(reservation.ArticleId, out var article))
                {
                    article.Release(reservation.Quantity);
                }
                
                _reservations.TryRemove(reservation.Id, out _);
                await _repository.DeleteReservation(reservation.Id);
            }
            
            _logger.LogWarning("Rolled back {Count} reservations due to failure", reservations.Count);
        }

        private void CheckExpirations(object state)
        {
            try
            {
                var expiringItems = GetExpiringItems(7); // Check items expiring in 7 days
                var expiredItems = _inventory.Values.Where(a => a.IsExpired()).ToList();

                // Notify about expiring items
                foreach (var item in expiringItems.Where(i => !i.IsExpired()))
                {
                    OnItemExpiring?.Invoke(this, new ItemExpiringEventArgs(item));
                }

                // Handle expired items
                foreach (var item in expiredItems.Where(i => i.Status != ArticleStatus.Expired))
                {
                    item.Status = ArticleStatus.Expired;
                    OnItemExpired?.Invoke(this, new ItemExpiredEventArgs(item));
                    
                    _logger.LogWarning("Article {ArticleId} has expired", item.Id);
                }

                if (expiringItems.Any() || expiredItems.Any())
                {
                    _logger.LogInformation("Expiration check: {ExpiringCount} expiring, {ExpiredCount} expired",
                        expiringItems.Count, expiredItems.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during expiration check");
            }
        }

        private void CleanupExpiredReservations(object state)
        {
            try
            {
                var cutoffTime = DateTime.UtcNow.AddDays(-1); // Clean up reservations older than 1 day
                var expiredReservations = _reservations.Values
                    .Where(r => r.Status == ReservationStatus.Released && r.ReleasedAt < cutoffTime)
                    .ToList();

                foreach (var reservation in expiredReservations)
                {
                    _reservations.TryRemove(reservation.Id, out _);
                }

                if (expiredReservations.Any())
                {
                    _logger.LogInformation("Cleaned up {Count} expired reservations", expiredReservations.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during reservation cleanup");
            }
        }

        // IHostedService implementation
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("InventoryService starting");
            await LoadInventoryFromRepository();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("InventoryService stopping");
            // Ensure all changes are persisted
        }

        public void Dispose()
        {
            _expirationTimer?.Dispose();
            _cleanupTimer?.Dispose();
            _operationSemaphore?.Dispose();
        }
    }

    // Supporting classes and interfaces
    public interface IInventoryService
    {
        Task<InventoryCheckResult> CheckAvailability(List<InventoryRequirement> requirements);
        Task<bool> ReserveInventory(List<InventoryRequirement> requirements);
        Task<bool> ReleaseInventory(List<InventoryRequirement> requirements);
        Task<bool> UpdateInventory(InventoryArticle article, int quantityChange);
        InventoryStatus GetInventoryStatus();
        List<InventoryArticle> GetExpiringItems(int days);
        List<InventoryArticle> GetOutOfStockItems();
        List<InventoryArticle> GetLowStockItems(int threshold);
        
        event EventHandler<InventoryChangedEventArgs> OnInventoryChanged;
        event EventHandler<ItemExpiringEventArgs> OnItemExpiring;
        event EventHandler<ItemExpiredEventArgs> OnItemExpired;
    }

    public class InventoryArticle
    {
        public string Id { get; set; }
        public ArticleType Type { get; set; }
        public string Name { get; set; }
        public string LotNumber { get; set; }
        public int Quantity { get; set; }
        public int ReservedQuantity { get; set; }
        public DateTime ExpirationDate { get; set; }
        public DateTime LastUpdated { get; set; }
        public ArticleStatus Status { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new();
        public Location StorageLocation { get; set; }

        public InventoryArticle(string id, ArticleType type, string name)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Type = type;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Status = ArticleStatus.Available;
            LastUpdated = DateTime.UtcNow;
        }

        public bool IsExpired() => DateTime.UtcNow > ExpirationDate;
        
        public bool IsExpiringSoon(int days) => 
            DateTime.UtcNow.AddDays(days) > ExpirationDate && !IsExpired();

        public bool CanReserve(int quantity) => 
            Quantity - ReservedQuantity >= quantity && Status == ArticleStatus.Available;

        public bool Reserve(int quantity)
        {
            if (!CanReserve(quantity)) return false;
            
            ReservedQuantity += quantity;
            LastUpdated = DateTime.UtcNow;
            return true;
        }

        public bool Release(int quantity)
        {
            if (ReservedQuantity < quantity) return false;
            
            ReservedQuantity -= quantity;
            LastUpdated = DateTime.UtcNow;
            return true;
        }
    }

    public class InventoryReservation
    {
        public Guid Id { get; private set; }
        public string ArticleId { get; set; }
        public int Quantity { get; set; }
        public DateTime ReservedAt { get; private set; }
        public DateTime? ReleasedAt { get; private set; }
        public Guid AssaySampleId { get; set; }
        public ReservationStatus Status { get; private set; }

        public InventoryReservation(string articleId, int quantity, Guid assaySampleId)
        {
            Id = Guid.NewGuid();
            ArticleId = articleId;
            Quantity = quantity;
            AssaySampleId = assaySampleId;
            ReservedAt = DateTime.UtcNow;
            Status = ReservationStatus.Active;
        }

        public bool IsActive() => Status == ReservationStatus.Active;

        public void Release()
        {
            Status = ReservationStatus.Released;
            ReleasedAt = DateTime.UtcNow;
        }
    }

    public class InventoryStatus
    {
        public int TotalArticles { get; set; }
        public int TotalQuantity { get; set; }
        public int AvailableQuantity { get; set; }
        public int ReservedQuantity { get; set; }
        public int ActiveReservations { get; set; }
        public int OutOfStockItems { get; set; }
        public int ExpiringItems { get; set; }
        public int ExpiredItems { get; set; }
        public Dictionary<ArticleStatus, int> StatusDistribution { get; set; } = new();
    }

    public class ItemAvailability
    {
        public bool IsAvailable { get; }
        public string Reason { get; }

        public ItemAvailability(bool isAvailable, string reason = null)
        {
            IsAvailable = isAvailable;
            Reason = reason;
        }
    }

    // Event argument classes
    public class InventoryChangedEventArgs : EventArgs
    {
        public InventoryChangeType ChangeType { get; }
        public List<InventoryRequirement> AffectedItems { get; }
        public DateTime Timestamp { get; }

        public InventoryChangedEventArgs(InventoryChangeType changeType, List<InventoryRequirement> affectedItems)
        {
            ChangeType = changeType;
            AffectedItems = affectedItems;
            Timestamp = DateTime.UtcNow;
        }
    }

    public class ItemExpiringEventArgs : EventArgs
    {
        public InventoryArticle Article { get; }
        public TimeSpan TimeUntilExpiration { get; }

        public ItemExpiringEventArgs(InventoryArticle article)
        {
            Article = article;
            TimeUntilExpiration = article.ExpirationDate - DateTime.UtcNow;
        }
    }

    public class ItemExpiredEventArgs : EventArgs
    {
        public InventoryArticle Article { get; }
        public TimeSpan TimeSinceExpiration { get; }

        public ItemExpiredEventArgs(InventoryArticle article)
        {
            Article = article;
            TimeSinceExpiration = DateTime.UtcNow - article.ExpirationDate;
        }
    }

    // Enums
    public enum ArticleStatus
    {
        Available,
        Reserved,
        OutOfStock,
        Expired,
        Damaged,
        OnOrder
    }

    public enum ReservationStatus
    {
        Active,
        Released,
        Expired
    }

    public enum InventoryChangeType
    {
        Added,
        Consumed,
        Reserved,
        Released,
        Expired,
        Damaged
    }
}