// AssaySample.cs - Enhanced domain entity
using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel.DataAnnotations;

namespace Instrument.Scheduler.Model
{
    public class AssaySample
    {
        private AssayStatus _status;
        private readonly object _statusLock = new object();

        public Guid Id { get; private set; }
        public Sample Sample { get; private set; }
        public List<Assay> Assays { get; private set; }
        
        public AssayStatus Status 
        { 
            get => _status; 
            private set => _status = value; 
        }
        
        public DateTime CreatedAt { get; private set; }
        public DateTime? StartedAt { get; private set; }
        public DateTime? CompletedAt { get; private set; }
        public string ErrorMessage { get; private set; }
        public Dictionary<string, object> Metadata { get; private set; }
        public SequenceGroup SequenceGroup { get; private set; }
        public IFlrSampleTestContext FlrContext { get; private set; }
        public int Priority { get; private set; }
        public List<AssayResult> Results { get; private set; }

        // Events
        public event EventHandler<AssayStatusChangedEventArgs> OnStatusChanged;

        public AssaySample(Sample sample, List<Assay> assays, int priority = 0)
        {
            Id = Guid.NewGuid();
            Sample = sample ?? throw new ArgumentNullException(nameof(sample));
            Assays = assays ?? throw new ArgumentNullException(nameof(assays));
            Priority = priority;
            CreatedAt = DateTime.UtcNow;
            Status = AssayStatus.Created;
            Metadata = new Dictionary<string, object>();
            Results = new List<AssayResult>();
            
            ValidateConstruction();
        }

        public void SetStatus(AssayStatus newStatus)
        {
            lock (_statusLock)
            {
                if (!IsValidStatusTransition(Status, newStatus))
                {
                    throw new InvalidOperationException(
                        $"Invalid status transition from {Status} to {newStatus} for sample {Id}");
                }

                var oldStatus = Status;
                Status = newStatus;
                UpdateTimestamps(newStatus);
                
                OnStatusChanged?.Invoke(this, new AssayStatusChangedEventArgs(Id, oldStatus, newStatus));
            }
        }

        public void SetErrorMessage(string errorMessage)
        {
            ErrorMessage = errorMessage;
            Metadata["LastError"] = new { Message = errorMessage, Timestamp = DateTime.UtcNow };
        }

        public void SetFlrContext(IFlrSampleTestContext context)
        {
            FlrContext = context ?? throw new ArgumentNullException(nameof(context));
        }

        public List<InventoryRequirement> ValidateInventoryRequirements()
        {
            var requirements = new List<InventoryRequirement>();
            
            // Add assay-specific requirements
            foreach (var assay in Assays)
            {
                requirements.AddRange(assay.GetInventoryRequirements());
            }
            
            // Add sample-specific requirements
            requirements.AddRange(GetSampleInventoryRequirements());
            
            // Remove duplicates and consolidate quantities
            return ConsolidateInventoryRequirements(requirements);
        }

        public SequenceGroup CreateSequenceGroup()
        {
            if (SequenceGroup != null)
            {
                throw new InvalidOperationException($"SequenceGroup already created for sample {Id}");
            }

            var sequenceGroup = new SequenceGroup(Guid.NewGuid(), $"Sample_{Sample.Id}_{Id:N}");
            
            foreach (var assay in Assays)
            {
                var sequences = CreateSequencesForAssay(assay);
                foreach (var sequence in sequences)
                {
                    sequenceGroup.AddSequence(sequence);
                }
            }

            SequenceGroup = sequenceGroup;
            return sequenceGroup;
        }

        public TimeSpan GetEstimatedDuration()
        {
            var baseDuration = Assays.Aggregate(TimeSpan.Zero, 
                (total, assay) => total.Add(assay.EstimatedDuration));
            
            // Add overhead based on sample type and complexity
            var overhead = CalculateExecutionOverhead();
            return baseDuration.Add(overhead);
        }

        public bool CanExecute()
        {
            return Status == AssayStatus.InventoryReserved &&
                   Assays.All(a => a.ValidateParameters()) &&
                   Sample.ValidateProperties() &&
                   ValidateExecutionReadiness();
        }

        public void AddResult(AssayResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            
            Results.Add(result);
            Metadata["LastResultAdded"] = DateTime.UtcNow;
        }

        public void AddResults(IEnumerable<AssayResult> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            
            Results.AddRange(results);
            Metadata["LastResultsAdded"] = DateTime.UtcNow;
        }

        private bool IsValidStatusTransition(AssayStatus from, AssayStatus to)
        {
            return from switch
            {
                AssayStatus.Created => to is AssayStatus.Queued or AssayStatus.Failed,
                AssayStatus.Queued => to is AssayStatus.InventoryReserved or AssayStatus.InventoryUnavailable or AssayStatus.Cancelled,
                AssayStatus.InventoryUnavailable => to is AssayStatus.Queued or AssayStatus.Cancelled,
                AssayStatus.InventoryReserved => to is AssayStatus.InProgress or AssayStatus.Cancelled,
                AssayStatus.InProgress => to is AssayStatus.Completed or AssayStatus.Failed or AssayStatus.Cancelled,
                AssayStatus.Failed => to is AssayStatus.Queued, // Allow retry
                _ => false
            };
        }

        private void UpdateTimestamps(AssayStatus status)
        {
            switch (status)
            {
                case AssayStatus.InProgress:
                    StartedAt = DateTime.UtcNow;
                    break;
                case AssayStatus.Completed:
                case AssayStatus.Failed:
                case AssayStatus.Cancelled:
                    CompletedAt = DateTime.UtcNow;
                    break;
            }
        }

        private void ValidateConstruction()
        {
            if (!Assays.Any())
                throw new ArgumentException("At least one assay must be specified", nameof(Assays));
            
            if (Priority < 0)
                throw new ArgumentException("Priority cannot be negative", nameof(Priority));
        }

        private List<InventoryRequirement> GetSampleInventoryRequirements()
        {
            var requirements = new List<InventoryRequirement>();
            
            // Sample tube requirement
            requirements.Add(new InventoryRequirement("Tube", Sample.TubeType.ToString(), 1));
            
            // Technology-specific requirements
            var technologies = Assays.Select(a => a.Technology).Distinct();
            foreach (var tech in technologies)
            {
                requirements.AddRange(GetTechnologyRequirements(tech));
            }
            
            return requirements;
        }

        private List<InventoryRequirement> ConsolidateInventoryRequirements(List<InventoryRequirement> requirements)
        {
            return requirements
                .GroupBy(r => new { r.ArticleType, r.ArticleId })
                .Select(g => new InventoryRequirement(
                    g.Key.ArticleType, 
                    g.Key.ArticleId, 
                    g.Sum(r => r.Quantity)))
                .ToList();
        }

        private List<Sequence> CreateSequencesForAssay(Assay assay)
        {
            // This would integrate with configuration/defaults service
            var sequenceBuilder = new SequenceBuilder(assay, Sample);
            return sequenceBuilder.CreateSequences();
        }

        private TimeSpan CalculateExecutionOverhead()
        {
            // Calculate overhead based on sample complexity
            var baseOverhead = TimeSpan.FromMinutes(1);
            var assayOverhead = TimeSpan.FromSeconds(Assays.Count * 30);
            return baseOverhead.Add(assayOverhead);
        }

        private bool ValidateExecutionReadiness()
        {
            // Additional validation checks
            return Sample.Id != null && 
                   Assays.All(a => a.Id > 0) &&
                   !string.IsNullOrEmpty(Sample.Position);
        }

        private List<InventoryRequirement> GetTechnologyRequirements(Technology technology)
        {
            // Technology-specific inventory requirements
            return technology switch
            {
                Technology.ImmunoCap => new List<InventoryRequirement>
                {
                    new("Cartridge", "ImmunoCap", 1),
                    new("Buffer", "ImmunoCapBuffer", 1)
                },
                Technology.Elia => new List<InventoryRequirement>
                {
                    new("Cartridge", "Elia", 1),
                    new("Buffer", "EliaBuffer", 1)
                },
                _ => new List<InventoryRequirement>()
            };
        }
    }

    // Supporting classes and enums
    public enum AssayStatus
    {
        Created,
        Queued,
        InventoryReserved,
        InventoryUnavailable,
        InProgress,
        Completed,
        Failed,
        Cancelled
    }

    public class AssayStatusChangedEventArgs : EventArgs
    {
        public Guid SampleId { get; }
        public AssayStatus OldStatus { get; }
        public AssayStatus NewStatus { get; }
        public DateTime Timestamp { get; }

        public AssayStatusChangedEventArgs(Guid sampleId, AssayStatus oldStatus, AssayStatus newStatus)
        {
            SampleId = sampleId;
            OldStatus = oldStatus;
            NewStatus = newStatus;
            Timestamp = DateTime.UtcNow;
        }
    }
}

// AssayManager.cs - Enhanced service implementation
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

namespace Instrument.Scheduler.Components
{
    public class AssayManager : IAssayManager, IHostedService, IDisposable
    {
        private readonly ConcurrentDictionary<Guid, AssaySample> _assaySamples;
        private readonly AssayExecutionPlanner _executionPlanner;
        private readonly SequenceGroupManager _sequenceGroupManager;
        private readonly IFlrContextFactory _flrContextFactory;
        private readonly IInventoryService _inventoryService;
        private readonly ILogger<AssayManager> _logger;
        private readonly SemaphoreSlim _executionSemaphore;
        private readonly Timer _monitoringTimer;
        
        private IFlrAssayRunContext _flrAssayRunContext;
        private CancellationTokenSource _cancellationTokenSource;
        private Task _executionTask;
        private volatile bool _isExecuting;

        public event EventHandler<AssaySampleStatusChangedEventArgs> OnAssaySampleStatusChanged;

        public AssayManager(
            AssayExecutionPlanner executionPlanner,
            SequenceGroupManager sequenceGroupManager,
            IFlrContextFactory flrContextFactory,
            IInventoryService inventoryService,
            ILogger<AssayManager> logger)
        {
            _assaySamples = new ConcurrentDictionary<Guid, AssaySample>();
            _executionPlanner = executionPlanner ?? throw new ArgumentNullException(nameof(executionPlanner));
            _sequenceGroupManager = sequenceGroupManager ?? throw new ArgumentNullException(nameof(sequenceGroupManager));
            _flrContextFactory = flrContextFactory ?? throw new ArgumentNullException(nameof(flrContextFactory));
            _inventoryService = inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _executionSemaphore = new SemaphoreSlim(1, 1);
            
            // Set up monitoring timer
            _monitoringTimer = new Timer(MonitorSamples, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
            
            // Subscribe to sequence group manager events
            _sequenceGroupManager.OnSequenceGroupCompleted += HandleSequenceGroupCompletion;
        }

        public async Task<bool> AddAssaySamples(IEnumerable<AssaySample> assaySamples)
        {
            if (assaySamples == null) throw new ArgumentNullException(nameof(assaySamples));

            var addedSamples = new List<AssaySample>();
            var sampleList = assaySamples.ToList();

            foreach (var sample in sampleList)
            {
                if (_assaySamples.TryAdd(sample.Id, sample))
                {
                    sample.OnStatusChanged += HandleAssaySampleStatusChanged;
                    sample.SetStatus(AssayStatus.Queued);
                    addedSamples.Add(sample);
                    
                    _logger.LogInformation("Added AssaySample {SampleId} to queue with {AssayCount} assays", 
                        sample.Id, sample.Assays.Count);
                }
                else
                {
                    _logger.LogWarning("Failed to add AssaySample {SampleId} - already exists", sample.Id);
                }
            }

            if (addedSamples.Count > 0)
            {
                // Trigger inventory check for new samples
                _ = Task.Run(async () => await TriggerInventoryCheck(addedSamples));
            }

            OnAssaySampleStatusChanged?.Invoke(this, 
                new AssaySampleStatusChangedEventArgs($"Added {addedSamples.Count} of {sampleList.Count} samples"));

            return addedSamples.Count == sampleList.Count;
        }

        public async Task<bool> RemoveAssaySample(Guid sampleId)
        {
            if (_assaySamples.TryRemove(sampleId, out var sample))
            {
                sample.OnStatusChanged -= HandleAssaySampleStatusChanged;
                
                // Clean up resources
                await CleanupSample(sample);
                
                _logger.LogInformation("Removed AssaySample {SampleId}", sampleId);
                return true;
            }

            return false;
        }

        public AssaySample GetAssaySample(Guid sampleId)
        {
            _assaySamples.TryGetValue(sampleId, out var sample);
            return sample;
        }

        public IReadOnlyCollection<AssaySample> GetAllAssaySamples()
        {
            return _assaySamples.Values.ToList().AsReadOnly();
        }

        public IReadOnlyCollection<AssaySample> GetAssaySamplesByStatus(AssayStatus status)
        {
            return _assaySamples.Values.Where(s => s.Status == status).ToList().AsReadOnly();
        }

        public async Task<AssayExecutionPlan> CreateExecutionPlan()
        {
            var readySamples = GetReadyForExecution();
            
            if (!readySamples.Any())
            {
                _logger.LogInformation("No samples ready for execution");
                return new AssayExecutionPlan(new List<AssayExecutionStep>());
            }

            try
            {
                var plan = await _executionPlanner.CreateExecutionPlan(readySamples);
                _logger.LogInformation("Created execution plan with {StepCount} steps for {SampleCount} samples", 
                    plan.ExecutionSteps.Count(), readySamples.Count);
                return plan;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create execution plan");
                throw;
            }
        }

        public async Task<bool> StartExecution()
        {
            await _executionSemaphore.WaitAsync();
            try
            {
                if (_isExecuting)
                {
                    _logger.LogWarning("Execution already in progress");
                    return false;
                }

                _cancellationTokenSource = new CancellationTokenSource();
                _flrAssayRunContext = await _flrContextFactory.CreateAssayRunContext();
                
                _executionTask = Task.Run(async () => await ExecuteAssaySamples(_cancellationTokenSource.Token));
                _isExecuting = true;
                
                _logger.LogInformation("Started assay execution");
                return true;
            }
            finally
            {
                _executionSemaphore.Release();
            }
        }

        public async Task<bool> StopExecution()
        {
            await _executionSemaphore.WaitAsync();
            try
            {
                if (!_isExecuting)
                {
                    _logger.LogWarning("No execution in progress");
                    return false;
                }

                _cancellationTokenSource?.Cancel();
                
                if (_executionTask != null)
                {
                    await _executionTask;
                }

                _isExecuting = false;
                _logger.LogInformation("Stopped assay execution");
                return true;
            }
            finally
            {
                _executionSemaphore.Release();
            }
        }

        public async Task<InventoryCheckResult> CheckInventoryAvailability()
        {
            var allSamples = GetAllAssaySamples();
            var allRequirements = new List<InventoryRequirement>();
            
            foreach (var sample in allSamples.Where(s => s.Status == AssayStatus.Queued))
            {
                allRequirements.AddRange(sample.ValidateInventoryRequirements());
            }

            var consolidatedRequirements = ConsolidateRequirements(allRequirements);
            return await _inventoryService.CheckAvailability(consolidatedRequirements);
        }

        private async Task TriggerInventoryCheck(List<AssaySample> samples)
        {
            foreach (var sample in samples)
            {
                try
                {
                    var requirements = sample.ValidateInventoryRequirements();
                    var checkResult = await _inventoryService.CheckAvailability(requirements);

                    if (checkResult.IsAvailable)
                    {
                        var reserved = await _inventoryService.ReserveInventory(requirements);
                        if (reserved)
                        {
                            sample.SetStatus(AssayStatus.InventoryReserved);
                            _logger.LogInformation("Inventory reserved for sample {SampleId}", sample.Id);
                        }
                        else
                        {
                            sample.SetStatus(AssayStatus.InventoryUnavailable);
                            _logger.LogWarning("Failed to reserve inventory for sample {SampleId}", sample.Id);
                        }
                    }
                    else
                    {
                        sample.SetStatus(AssayStatus.InventoryUnavailable);
                        _logger.LogWarning("Inventory unavailable for sample {SampleId}: {MissingItems}",
                            sample.Id, string.Join(", ", checkResult.MissingItems.Select(i => $"{i.ArticleType}:{i.Quantity}")));
                    }
                }
                catch (Exception ex)
                {
                    sample.SetStatus(AssayStatus.Failed);
                    sample.SetErrorMessage($"Inventory check failed: {ex.Message}");
                    _logger.LogError(ex, "Inventory check failed for sample {SampleId}", sample.Id);
                }
            }
        }

        private async Task ExecuteAssaySamples(CancellationToken cancellationToken)
        {
            try
            {
                await _flrAssayRunContext.BeginAssayRun();
                _logger.LogInformation("Started assay run");

                while (!cancellationToken.IsCancellationRequested)
                {
                    var readySamples = GetReadyForExecution();

                    if (!readySamples.Any())
                    {
                        await Task.Delay(5000, cancellationToken); // Wait 5 seconds for new samples
                        continue;
                    }

                    var executionPlan = await _executionPlanner.CreateExecutionPlan(readySamples);
                    await ProcessExecutionPlan(executionPlan, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Execution cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Execution failed");
            }
            finally
            {
                try
                {
                    await _flrAssayRunContext.EndAssayRun();
                    _logger.LogInformation("Ended assay run");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error ending assay run");
                }
            }
        }

        private async Task ProcessExecutionPlan(AssayExecutionPlan plan, CancellationToken cancellationToken)
        {
            foreach (var step in plan.ExecutionSteps)
            {
                if (cancellationToken.IsCancellationRequested) break;

                _logger.LogInformation("Processing execution step with {SampleCount} samples", 
                    step.AssaySamples.Count());

                var tasks = step.AssaySamples.Select(sample => 
                    ExecuteAssaySample(sample, cancellationToken)).ToArray();

                await Task.WhenAll(tasks);
            }
        }

        private async Task ExecuteAssaySample(AssaySample sample, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Starting execution of sample {SampleId}", sample.Id);
                sample.SetStatus(AssayStatus.InProgress);

                // Create FLR context
                var flrContext = await _flrAssayRunContext.CreateSampleTestContext(sample);
                sample.SetFlrContext(flrContext);

                // Create and execute sequence group
                var sequenceGroup = sample.CreateSequenceGroup();
                await _sequenceGroupManager.AddSequenceGroup(sequenceGroup);
                
                var executionResult = await _sequenceGroupManager.ExecuteSequenceGroup(sequenceGroup.Id);
                
                if (executionResult.IsSuccess)
                {
                    _logger.LogInformation("Successfully executed sample {SampleId}", sample.Id);
                }
                else
                {
                    throw new InvalidOperationException($"Sequence group execution failed: {executionResult.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                sample.SetStatus(AssayStatus.Failed);
                sample.SetErrorMessage(ex.Message);
                _logger.LogError(ex, "Failed to execute sample {SampleId}", sample.Id);

                // Ensure inventory is released
                await ReleaseInventoryForSample(sample);
            }
        }

        private void HandleAssaySampleStatusChanged(object sender, AssayStatusChangedEventArgs e)
        {
            var sample = sender as AssaySample;
            _logger.LogInformation("Sample {SampleId} status changed from {OldStatus} to {NewStatus}",
                sample.Id, e.OldStatus, e.NewStatus);

            OnAssaySampleStatusChanged?.Invoke(this, e);

            // Handle terminal states
            switch (e.NewStatus)
            {
                case AssayStatus.Completed:
                case AssayStatus.Failed:
                case AssayStatus.Cancelled:
                    _ = Task.Run(async () => await HandleSampleCompletion(sample));
                    break;
            }
        }

        private async Task HandleSequenceGroupCompletion(object sender, SequenceGroupCompletedEventArgs e)
        {
            var sample = _assaySamples.Values.FirstOrDefault(s => s.SequenceGroup?.Id == e.SequenceGroupId);

            if (sample == null)
            {
                _logger.LogWarning("No sample found for completed sequence group {SequenceGroupId}", e.SequenceGroupId);
                return;
            }

            if (e.Result.IsSuccess)
            {
                // Convert sequence results to assay results
                var assayResults = ConvertSequenceResultsToAssayResults(e.Result.SequenceResults, sample);
                sample.AddResults(assayResults);

                // Report to FLR
                if (sample.FlrContext != null)
                {
                    await sample.FlrContext.ReportResults(assayResults);
                }

                sample.SetStatus(AssayStatus.Completed);
            }
            else
            {
                sample.SetStatus(AssayStatus.Failed);
                sample.SetErrorMessage(e.Result.ErrorMessage);
            }

            // Release inventory
            await ReleaseInventoryForSample(sample);
        }

        private async Task HandleSampleCompletion(AssaySample sample)
        {
            // Schedule for removal after a delay to allow result retrieval
            await Task.Delay(TimeSpan.FromMinutes(5));
            await RemoveAssaySample(sample.Id);
        }

        private List<AssaySample> GetReadyForExecution()
        {
            return _assaySamples.Values
                .Where(s => s.Status == AssayStatus.InventoryReserved && s.CanExecute())
                .OrderByDescending(s => s.Priority)
                .ThenBy(s => s.CreatedAt)
                .ToList();
        }

        private async Task CleanupSample(AssaySample sample)
        {
            try
            {
                // Release any reserved inventory
                await ReleaseInventoryForSample(sample);

                // Clean up FLR context
                sample.FlrContext?.Dispose();

                // Remove from sequence group manager if present
                if (sample.SequenceGroup != null)
                {
                    await _sequenceGroupManager.RemoveSequenceGroup(sample.SequenceGroup.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up sample {SampleId}", sample.Id);
            }
        }

        private async Task ReleaseInventoryForSample(AssaySample sample)
        {
            try
            {
                var requirements = sample.ValidateInventoryRequirements();
                await _inventoryService.ReleaseInventory(requirements);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to release inventory for sample {SampleId}", sample.Id);
            }
        }

        private void MonitorSamples(object state)
        {
            try
            {
                var statusCounts = _assaySamples.Values
                    .GroupBy(s => s.Status)
                    .ToDictionary(g => g.Key, g => g.Count());

                _logger.LogInformation("Sample status summary: {StatusCounts}", 
                    string.Join(", ", statusCounts.Select(kvp => $"{kvp.Key}:{kvp.Value}")));

                // Check for stuck samples
                var stuckSamples = _assaySamples.Values
                    .Where(s => s.Status == AssayStatus.InProgress && 
                               s.StartedAt.HasValue && 
                               DateTime.UtcNow - s.StartedAt.Value > TimeSpan.FromHours(2))
                    .ToList();

                if (stuckSamples.Any())
                {
                    _logger.LogWarning("Found {Count} potentially stuck samples", stuckSamples.Count);
                    foreach (var sample in stuckSamples)
                    {
                        _logger.LogWarning("Sample {SampleId} has been in progress for {Duration}", 
                            sample.Id, DateTime.UtcNow - sample.StartedAt.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in sample monitoring");
            }
        }

        private List<InventoryRequirement> ConsolidateRequirements(List<InventoryRequirement> requirements)
        {
            return requirements
                .GroupBy(r => new { r.ArticleType, r.ArticleId })
                .Select(g => new InventoryRequirement(
                    g.Key.ArticleType,
                    g.Key.ArticleId,
                    g.Sum(r => r.Quantity)))
                .ToList();
        }

        private List<AssayResult> ConvertSequenceResultsToAssayResults(
            List<SequenceResult> sequenceResults, AssaySample sample)
        {
            var assayResults = new List<AssayResult>();

            foreach (var assay in sample.Assays)
            {
                var relevantResults = sequenceResults
                    .Where(sr => sr.AssayId == assay.Id)
                    .ToList();

                if (relevantResults.Any())
                {
                    var assayResult = new AssayResult(assay.Id);
                    
                    foreach (var seqResult in relevantResults)
                    {
                        foreach (var measurement in seqResult.Measurements)
                        {
                            assayResult.AddMeasurement(measurement.Key, measurement.Value);
                        }
                    }

                    assayResults.Add(assayResult);
                }
            }

            return assayResults;
        }

        // IHostedService implementation
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("AssayManager service starting");
            // Perform any startup initialization
            await Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("AssayManager service stopping");
            await StopExecution();
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _executionTask?.Wait(TimeSpan.FromSeconds(30));
            
            _monitoringTimer?.Dispose();
            _executionSemaphore?.Dispose();
            _cancellationTokenSource?.Dispose();
            _flrAssayRunContext?.Dispose();
            
            // Unsubscribe from events
            _sequenceGroupManager.OnSequenceGroupCompleted -= HandleSequenceGroupCompletion;
            
            // Clean up all samples
            foreach (var sample in _assaySamples.Values)
            {
                sample.OnStatusChanged -= HandleAssaySampleStatusChanged;
            }
            
            _assaySamples.Clear();
        }
    }
}