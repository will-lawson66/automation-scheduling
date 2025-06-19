// ============================================================================
// REPOSITORY LAYER ENHANCEMENTS
// ============================================================================

using Instrument.Data.DataContext;
using Instrument.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Instrument.Data.Repository;

/// <summary>
/// Enhanced base repository with comprehensive CancellationToken support
/// </summary>
public abstract class Repository<T> : IRepository<T> where T : class
{
    protected readonly SchedulerDbContext _context;
    protected readonly ILogger<Repository<T>> _logger;

    protected Repository(SchedulerDbContext context, ILogger<Repository<T>> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public virtual async Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving {EntityType} with ID: {Id}", typeof(T).Name, id);
        
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await _context.Set<T>().FindAsync(new object[] { id }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get operation for {EntityType} ID: {Id} was cancelled", typeof(T).Name, id);
            throw;
        }
    }

    public virtual async Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving all {EntityType} entities", typeof(T).Name);
        
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await _context.Set<T>().ToListAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("GetAll operation for {EntityType} was cancelled", typeof(T).Name);
            throw;
        }
    }

    public virtual async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));
        
        _logger.LogDebug("Adding new {EntityType} entity", typeof(T).Name);
        
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await _context.Set<T>().AddAsync(entity, cancellationToken);
            return result.Entity;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Add operation for {EntityType} was cancelled", typeof(T).Name);
            throw;
        }
    }

    public virtual async Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));
        
        _logger.LogDebug("Updating {EntityType} entity", typeof(T).Name);
        
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            _context.Set<T>().Update(entity);
            await Task.CompletedTask; // Update is synchronous, but we check cancellation
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Update operation for {EntityType} was cancelled", typeof(T).Name);
            throw;
        }
    }

    public virtual async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Deleting {EntityType} with ID: {Id}", typeof(T).Name, id);
        
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entity = await GetByIdAsync(id, cancellationToken);
            if (entity != null)
            {
                _context.Set<T>().Remove(entity);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Delete operation for {EntityType} ID: {Id} was cancelled", typeof(T).Name, id);
            throw;
        }
    }

    public virtual async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Saving changes for {EntityType}", typeof(T).Name);
        
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("SaveChanges operation for {EntityType} was cancelled", typeof(T).Name);
            throw;
        }
    }
}

/// <summary>
/// Enhanced Parameter Repository with CancellationToken support
/// </summary>
public class ParameterRepository : Repository<Parameter>, IParameterRepository
{
    public ParameterRepository(SchedulerDbContext dbContext, ILogger<ParameterRepository> logger)
        : base(dbContext, logger)
    {
    }

    public async Task<IEnumerable<Parameter>> GetParametersByTypeAsync(ParameterType type, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving parameters of type: {ParameterType}", type);
        
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await _context.Set<Parameter>()
                .Where(p => p.Type == type)
                .ToListAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("GetParametersByType operation for type {ParameterType} was cancelled", type);
            throw;
        }
    }

    public async Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Checking if parameter exists with ID: {Id}", id);
        
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await _context.Set<Parameter>().AnyAsync(p => p.Id == id, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Exists check for parameter ID: {Id} was cancelled", id);
            throw;
        }
    }
}

// ============================================================================
// SERVICE LAYER ENHANCEMENTS
// ============================================================================

using Instrument.Data.Entities.Enums;
using Instrument.Data.Entities;
using Instrument.Data.Exceptions;
using Microsoft.Extensions.Logging;

namespace Instrument.Data.Services;

/// <summary>
/// Enhanced Parameter Service with comprehensive CancellationToken and timeout support
/// </summary>
public class ParameterService : IParameterService
{
    private readonly ILogger<ParameterService> _logger;
    private readonly IParameterRepository _parameterRepository;
    private readonly TimeoutOptions _timeoutOptions;

    public ParameterService(
        IParameterRepository parameterRepository,
        ILogger<ParameterService> logger,
        IOptions<TimeoutOptions> timeoutOptions)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _parameterRepository = parameterRepository ?? throw new ArgumentNullException(nameof(parameterRepository));
        _timeoutOptions = timeoutOptions?.Value ?? throw new ArgumentNullException(nameof(timeoutOptions));
    }

    public async Task<Parameter?> GetParameterByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        using var timeoutCts = new CancellationTokenSource(_timeoutOptions.ServiceTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        
        _logger.LogInformation("Retrieving parameter with ID: {Id}", id);
        
        try
        {
            linkedCts.Token.ThrowIfCancellationRequested();
            return await _parameterRepository.GetByIdAsync(id, linkedCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
        {
            _logger.LogWarning("GetParameter operation timed out for ID: {Id} after {Timeout}ms", 
                id, _timeoutOptions.ServiceTimeout.TotalMilliseconds);
            throw new GrpcTimeoutException("ParameterService", "GetParameterById", 
                _timeoutOptions.ServiceTimeout, timeoutCts.Elapsed, true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("GetParameter operation was cancelled for ID: {Id}", id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving parameter with ID: {Id}", id);
            throw new StorageProviderException("GetParameter", ex);
        }
    }

    public async Task<Parameter> CreateParameterAsync(Parameter parameter, CancellationToken cancellationToken = default)
    {
        if (parameter == null)
            throw new ArgumentNullException(nameof(parameter));

        using var timeoutCts = new CancellationTokenSource(_timeoutOptions.ServiceTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        _logger.LogInformation("Creating new parameter with Name: {Name}", parameter.Name);
        
        try
        {
            linkedCts.Token.ThrowIfCancellationRequested();
            
            // Add the parameter
            await _parameterRepository.AddAsync(parameter, linkedCts.Token);
            
            // Save changes with cancellation support
            await _parameterRepository.SaveChangesAsync(linkedCts.Token);
            
            _logger.LogInformation("Successfully created parameter with ID: {Id}", parameter.Id);
            return parameter;
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
        {
            _logger.LogWarning("CreateParameter operation timed out for parameter: {Name} after {Timeout}ms", 
                parameter.Name, _timeoutOptions.ServiceTimeout.TotalMilliseconds);
            throw new GrpcTimeoutException("ParameterService", "CreateParameter", 
                _timeoutOptions.ServiceTimeout, timeoutCts.Elapsed, true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("CreateParameter operation was cancelled for parameter: {Name}", parameter.Name);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating parameter with Name: {Name}", parameter.Name);
            throw new StorageProviderException("CreateParameter", ex);
        }
    }

    public async Task UpdateParameterAsync(Parameter parameter, CancellationToken cancellationToken = default)
    {
        if (parameter == null)
            throw new ArgumentNullException(nameof(parameter));

        using var timeoutCts = new CancellationTokenSource(_timeoutOptions.ServiceTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        _logger.LogInformation("Updating parameter with ID: {Id}, Name: {Name}", parameter.Id, parameter.Name);
        
        try
        {
            linkedCts.Token.ThrowIfCancellationRequested();
            
            // Check if the parameter exists first
            var existingParameter = await _parameterRepository.GetByIdAsync(parameter.Id, linkedCts.Token);
            if (existingParameter == null)
            {
                _logger.LogWarning("Parameter with ID {Id} does not exist", parameter.Id);
                throw new EntityNotFoundException("Parameter", parameter.Id);
            }

            linkedCts.Token.ThrowIfCancellationRequested();
            
            // Update the parameter
            await _parameterRepository.UpdateAsync(parameter, linkedCts.Token);
            await _parameterRepository.SaveChangesAsync(linkedCts.Token);
            
            _logger.LogInformation("Successfully updated parameter with ID: {Id}", parameter.Id);
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
        {
            _logger.LogWarning("UpdateParameter operation timed out for ID: {Id} after {Timeout}ms", 
                parameter.Id, _timeoutOptions.ServiceTimeout.TotalMilliseconds);
            throw new GrpcTimeoutException("ParameterService", "UpdateParameter", 
                _timeoutOptions.ServiceTimeout, timeoutCts.Elapsed, true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("UpdateParameter operation was cancelled for ID: {Id}", parameter.Id);
            throw;
        }
        catch (EntityNotFoundException)
        {
            throw; // Re-throw entity not found exceptions
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating parameter with ID: {Id}", parameter.Id);
            throw new StorageProviderException("UpdateParameter", ex);
        }
    }
    
    public async Task DeleteParameterAsync(int id, CancellationToken cancellationToken = default)
    {
        using var timeoutCts = new CancellationTokenSource(_timeoutOptions.ServiceTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        _logger.LogInformation("Deleting parameter with ID: {Id}", id);
        
        try
        {
            linkedCts.Token.ThrowIfCancellationRequested();
            
            // Validate if the parameter exists
            var existingParameter = await _parameterRepository.GetByIdAsync(id, linkedCts.Token);
            if (existingParameter == null)
            {
                _logger.LogWarning("Parameter with ID {Id} does not exist", id);
                throw new EntityNotFoundException("Parameter", id);
            }

            linkedCts.Token.ThrowIfCancellationRequested();
            
            // Delete the parameter
            await _parameterRepository.DeleteAsync(id, linkedCts.Token);
            await _parameterRepository.SaveChangesAsync(linkedCts.Token);
            
            _logger.LogInformation("Successfully deleted parameter with ID: {Id}", id);
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
        {
            _logger.LogWarning("DeleteParameter operation timed out for ID: {Id} after {Timeout}ms", 
                id, _timeoutOptions.ServiceTimeout.TotalMilliseconds);
            throw new GrpcTimeoutException("ParameterService", "DeleteParameter", 
                _timeoutOptions.ServiceTimeout, timeoutCts.Elapsed, true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("DeleteParameter operation was cancelled for ID: {Id}", id);
            throw;
        }
        catch (EntityNotFoundException)
        {
            throw; // Re-throw entity not found exceptions
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting parameter with ID: {Id}", id);
            throw new StorageProviderException("DeleteParameter", ex);
        }
    }

    public async Task<IEnumerable<Parameter>> GetAllParametersAsync(CancellationToken cancellationToken = default)
    {
        using var timeoutCts = new CancellationTokenSource(_timeoutOptions.ServiceTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        _logger.LogInformation("Retrieving all parameters");
        
        try
        {
            linkedCts.Token.ThrowIfCancellationRequested();
            return await _parameterRepository.GetAllAsync(linkedCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
        {
            _logger.LogWarning("GetAllParameters operation timed out after {Timeout}ms", 
                _timeoutOptions.ServiceTimeout.TotalMilliseconds);
            throw new GrpcTimeoutException("ParameterService", "GetAllParameters", 
                _timeoutOptions.ServiceTimeout, timeoutCts.Elapsed, true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("GetAllParameters operation was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all parameters");
            throw new StorageProviderException("GetAllParameters", ex);
        }
    }

    public async Task ValidateParameterValueAsync(Parameter parameter, string value, CancellationToken cancellationToken = default)
    {
        if (parameter == null)
            throw new ArgumentNullException(nameof(parameter));

        // For validation, we use a shorter timeout since it's mostly CPU-bound
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            
        _logger.LogInformation("Validating value '{Value}' for parameter {ParameterId} of type {ParameterType}", 
            value, parameter.Id, parameter.Type);
        
        try
        {
            linkedCts.Token.ThrowIfCancellationRequested();
            
            if (string.IsNullOrEmpty(value))
            {
                _logger.LogInformation("Value is null or empty for parameter {ParameterId}", parameter.Id);
                throw new ValidationException(parameter.Id, value, "Value cannot be null or empty");
            }
            
            // Simulate potential async validation (e.g., database lookups for constraints)
            await Task.Delay(1, linkedCts.Token); // Minimal delay to make it properly async
            
            switch (parameter.Type)
            {
                case ParameterType.IntegerType:
                case ParameterType.DecimalType:
                    await ValidateNumericValueAsync(parameter, value, linkedCts.Token);
                    break;
                    
                case ParameterType.StringType:
                    await ValidateStringValueAsync(parameter, value, linkedCts.Token);
                    break;
                    
                case ParameterType.BooleanType:
                    await ValidateBooleanValueAsync(parameter, value, linkedCts.Token);
                    break;
                    
                default:
                    _logger.LogInformation("Custom type parameter {ParameterId} validation defaulting to success", 
                        parameter.Id);
                    break;
            }
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
        {
            _logger.LogWarning("ValidateParameterValue operation timed out for parameter {ParameterId} after 5 seconds", 
                parameter.Id);
            throw new GrpcTimeoutException("ParameterService", "ValidateParameterValue", 
                TimeSpan.FromSeconds(5), timeoutCts.Elapsed, true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("ValidateParameterValue operation was cancelled for parameter {ParameterId}", parameter.Id);
            throw;
        }
        catch (ValidationException)
        {
            throw; // Re-throw validation exceptions
        }
    }

    private async Task ValidateNumericValueAsync(Parameter parameter, string value, CancellationToken cancellationToken)
    {
        if (!decimal.TryParse(value, out var numValue))
        {
            _logger.LogWarning("Value '{Value}' is not a valid number for parameter {ParameterId}", 
                value, parameter.Id);
            throw new ValidationException(parameter.Id, value, "Value is not a valid number");
        }
        
        cancellationToken.ThrowIfCancellationRequested();
        
        if (!string.IsNullOrEmpty(parameter.Min) && 
            decimal.TryParse(parameter.Min, out var minValue) &&
            numValue < minValue)
        {
            _logger.LogWarning("Value {Value} is less than minimum value {MinValue} for parameter {ParameterId}", 
                numValue, minValue, parameter.Id);
            throw new ValidationException(parameter.Id, value, $"Value must be greater than or equal to {minValue}");
        }
        
        cancellationToken.ThrowIfCancellationRequested();
        
        if (!string.IsNullOrEmpty(parameter.Max) && 
            decimal.TryParse(parameter.Max, out var maxValue) &&
            numValue > maxValue)
        {
            _logger.LogWarning("Value {Value} is greater than maximum value {MaxValue} for parameter {ParameterId}", 
                numValue, maxValue, parameter.Id);
            throw new ValidationException(parameter.Id, value, $"Value must be less than or equal to {maxValue}");
        }
        
        _logger.LogInformation("Value {Value} is valid for numeric parameter {ParameterId}", 
            numValue, parameter.Id);
        
        await Task.CompletedTask;
    }

    private async Task ValidateStringValueAsync(Parameter parameter, string value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        if (!string.IsNullOrEmpty(parameter.Min) && 
            int.TryParse(parameter.Min, out var minLength) &&
            value.Length < minLength)
        {
            _logger.LogWarning("String length {Length} is less than minimum length {MinLength} for parameter {ParameterId}", 
                value.Length, minLength, parameter.Id);
            throw new ValidationException(parameter.Id, value, $"String length must be at least {minLength} characters");
        }
        
        cancellationToken.ThrowIfCancellationRequested();
        
        if (!string.IsNullOrEmpty(parameter.Max) && 
            int.TryParse(parameter.Max, out var maxLength) &&
            value.Length > maxLength)
        {
            _logger.LogWarning("String length {Length} is greater than maximum length {MaxLength} for parameter {ParameterId}", 
                value.Length, maxLength, parameter.Id);
            throw new ValidationException(parameter.Id, value, $"String length must be at most {maxLength} characters");
        }
        
        _logger.LogInformation("String value is valid for parameter {ParameterId}", parameter.Id);
        
        await Task.CompletedTask;
    }

    private async Task ValidateBooleanValueAsync(Parameter parameter, string value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        if (!bool.TryParse(value, out _))
        {
            _logger.LogWarning("Value '{Value}' is not a valid boolean for parameter {ParameterId}", 
                value, parameter.Id);
            throw new ValidationException(parameter.Id, value, "Value is not a valid boolean (true/false)");
        }
        
        _logger.LogInformation("Boolean value is valid for parameter {ParameterId}", parameter.Id);
        
        await Task.CompletedTask;
    }
}

// ============================================================================
// CONFIGURATION AND SUPPORT CLASSES
// ============================================================================

/// <summary>
/// Configuration options for timeout management
/// </summary>
public class TimeoutOptions
{
    public const string SectionName = "Timeouts";
    
    /// <summary>
    /// Timeout for database operations
    /// </summary>
    public TimeSpan DatabaseTimeout { get; set; } = TimeSpan.FromSeconds(20);
    
    /// <summary>
    /// Timeout for service layer operations
    /// </summary>
    public TimeSpan ServiceTimeout { get; set; } = TimeSpan.FromSeconds(25);
    
    /// <summary>
    /// Timeout for individual HTTP requests
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);
    
    /// <summary>
    /// Timeout for background operations
    /// </summary>
    public TimeSpan BackgroundOperationTimeout { get; set; } = TimeSpan.FromMinutes(5);
    
    /// <summary>
    /// Timeout for validation operations
    /// </summary>
    public TimeSpan ValidationTimeout { get; set; } = TimeSpan.FromSeconds(5);
}

/// <summary>
/// Interface for updated repository pattern with CancellationToken support
/// </summary>
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for updated parameter repository with CancellationToken support
/// </summary>
public interface IParameterRepository : IRepository<Parameter>
{
    Task<IEnumerable<Parameter>> GetParametersByTypeAsync(ParameterType type, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for updated parameter service with CancellationToken support
/// </summary>
public interface IParameterService
{
    Task<Parameter?> GetParameterByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Parameter> CreateParameterAsync(Parameter parameter, CancellationToken cancellationToken = default);
    Task UpdateParameterAsync(Parameter parameter, CancellationToken cancellationToken = default);
    Task DeleteParameterAsync(int id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Parameter>> GetAllParametersAsync(CancellationToken cancellationToken = default);
    Task ValidateParameterValueAsync(Parameter parameter, string value, CancellationToken cancellationToken = default);
}

// ============================================================================
// DEPENDENCY INJECTION CONFIGURATION
// ============================================================================

namespace Instrument.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSchedulerDataServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure timeout options
        services.Configure<TimeoutOptions>(configuration.GetSection(TimeoutOptions.SectionName));
        
        // Register repositories with logging
        services.AddScoped<IParameterRepository, ParameterRepository>();
        services.AddScoped<IParameterService, ParameterService>();
        
        // Register other services...
        
        return services;
    }
}
