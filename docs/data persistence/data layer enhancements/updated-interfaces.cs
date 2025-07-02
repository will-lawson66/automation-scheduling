// ============================================================================
// UPDATED REPOSITORY INTERFACES
// ============================================================================

using Instrument.Data.Entities;
using Instrument.Data.Entities.Enums;

namespace Instrument.Data;

/// <summary>
/// Enhanced base repository interface with comprehensive CancellationToken support
/// </summary>
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Enhanced parameter repository interface
/// </summary>
public interface IParameterRepository : IRepository<Parameter>
{
    Task<IEnumerable<Parameter>> GetParametersByTypeAsync(ParameterType type, CancellationToken cancellationToken = default);
    Task<IEnumerable<Parameter>> GetParametersByNamePatternAsync(string namePattern, CancellationToken cancellationToken = default);
    Task<Parameter?> GetParameterByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<IEnumerable<Parameter>> GetParametersWithValidationRulesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Enhanced sequence repository interface
/// </summary>
public interface ISequenceRepository : IRepository<Sequence>
{
    Task<IEnumerable<Sequence>> GetSequencesByStatusAsync(SequenceStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<Sequence>> GetSequencesWithParametersAsync(CancellationToken cancellationToken = default);
    Task<Sequence?> GetSequenceWithFullDetailsAsync(int sequenceId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Sequence>> SearchSequencesAsync(string searchTerm, CancellationToken cancellationToken = default);
}

/// <summary>
/// Enhanced resource repository interface
/// </summary>
public interface IResourceRepository : IRepository<Resource>
{
    Task<IEnumerable<Resource>> GetResourcesByTypeAsync(ResourceType type, CancellationToken cancellationToken = default);
    Task<IEnumerable<Resource>> GetAvailableResourcesAsync(CancellationToken cancellationToken = default);
    Task<Resource?> GetResourceByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<bool> IsResourceAvailableAsync(int resourceId, CancellationToken cancellationToken = default);
}

// ============================================================================
// UPDATED SERVICE INTERFACES
// ============================================================================

/// <summary>
/// Enhanced parameter service interface with comprehensive async support
/// </summary>
public interface IParameterService
{
    // Core CRUD operations
    Task<Parameter?> GetParameterByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Parameter?> GetParameterByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<IEnumerable<Parameter>> GetAllParametersAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<Parameter>> GetParametersByTypeAsync(ParameterType type, CancellationToken cancellationToken = default);
    
    Task<Parameter> CreateParameterAsync(Parameter parameter, CancellationToken cancellationToken = default);
    Task UpdateParameterAsync(Parameter parameter, CancellationToken cancellationToken = default);
    Task DeleteParameterAsync(int id, CancellationToken cancellationToken = default);
    
    // Validation operations
    Task ValidateParameterValueAsync(Parameter parameter, string value, CancellationToken cancellationToken = default);
    Task<bool> TryValidateParameterValueAsync(Parameter parameter, string value, CancellationToken cancellationToken = default);
    Task<ValidationResult> ValidateParameterAsync(Parameter parameter, CancellationToken cancellationToken = default);
    
    // Search and filtering
    Task<IEnumerable<Parameter>> SearchParametersAsync(string searchTerm, CancellationToken cancellationToken = default);
    Task<IEnumerable<Parameter>> GetParametersWithValidationRulesAsync(CancellationToken cancellationToken = default);
    
    // Bulk operations
    Task<IEnumerable<Parameter>> CreateParametersAsync(IEnumerable<Parameter> parameters, CancellationToken cancellationToken = default);
    Task UpdateParametersAsync(IEnumerable<Parameter> parameters, CancellationToken cancellationToken = default);
    Task DeleteParametersAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default);
    
    // Existence checks
    Task<bool> ParameterExistsAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> ParameterNameExistsAsync(string name, CancellationToken cancellationToken = default);
}

/// <summary>
/// Enhanced sequence service interface
/// </summary>
public interface ISequenceService
{
    // Core CRUD operations
    Task<Sequence?> GetSequenceByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Sequence?> GetSequenceWithDetailsAsync(int id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Sequence>> GetAllSequencesAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<Sequence>> GetSequencesByStatusAsync(SequenceStatus status, CancellationToken cancellationToken = default);
    
    Task<Sequence> CreateSequenceAsync(Sequence sequence, CancellationToken cancellationToken = default);
    Task UpdateSequenceAsync(Sequence sequence, CancellationToken cancellationToken = default);
    Task DeleteSequenceAsync(int id, CancellationToken cancellationToken = default);
    
    // Sequence execution
    Task<ExecutionResult> ExecuteSequenceAsync(int sequenceId, CancellationToken cancellationToken = default);
    Task<ExecutionResult> ValidateSequenceAsync(int sequenceId, CancellationToken cancellationToken = default);
    Task PauseSequenceAsync(int sequenceId, CancellationToken cancellationToken = default);
    Task ResumeSequenceAsync(int sequenceId, CancellationToken cancellationToken = default);
    Task StopSequenceAsync(int sequenceId, CancellationToken cancellationToken = default);
    
    // Parameter associations
    Task AssignParameterToSequenceAsync(int sequenceId, int parameterId, CancellationToken cancellationToken = default);
    Task RemoveParameterFromSequenceAsync(int sequenceId, int parameterId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Parameter>> GetSequenceParametersAsync(int sequenceId, CancellationToken cancellationToken = default);
    
    // Search and filtering
    Task<IEnumerable<Sequence>> SearchSequencesAsync(string searchTerm, CancellationToken cancellationToken = default);
    Task<IEnumerable<Sequence>> GetSequencesByResourceAsync(int resourceId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Enhanced resource service interface
/// </summary>
public interface IResourceService
{
    // Core CRUD operations
    Task<Resource?> GetResourceByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Resource?> GetResourceByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<IEnumerable<Resource>> GetAllResourcesAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<Resource>> GetResourcesByTypeAsync(ResourceType type, CancellationToken cancellationToken = default);
    
    Task<Resource> CreateResourceAsync(Resource resource, CancellationToken cancellationToken = default);
    Task UpdateResourceAsync(Resource resource, CancellationToken cancellationToken = default);
    Task DeleteResourceAsync(int id, CancellationToken cancellationToken = default);
    
    // Resource availability
    Task<bool> IsResourceAvailableAsync(int resourceId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Resource>> GetAvailableResourcesAsync(CancellationToken cancellationToken = default);
    Task ReserveResourceAsync(int resourceId, TimeSpan duration, CancellationToken cancellationToken = default);
    Task ReleaseResourceAsync(int resourceId, CancellationToken cancellationToken = default);
    
    // Resource health
    Task<ResourceHealthStatus> GetResourceHealthAsync(int resourceId, CancellationToken cancellationToken = default);
    Task<IEnumerable<ResourceHealthStatus>> GetAllResourceHealthAsync(CancellationToken cancellationToken = default);
    
    // Search and filtering
    Task<IEnumerable<Resource>> SearchResourcesAsync(string searchTerm, CancellationToken cancellationToken = default);
}

// ============================================================================
// ENHANCED GRPC GATEWAY INTERFACES
// ============================================================================

/// <summary>
/// Enhanced gRPC Gateway interface with better cancellation support
/// </summary>
public interface IGrpcGateway : IDisposable
{
    Task<GatewayResult<TResponse>> ExecuteAsync<TRequest, TResponse>(
        IGrpcOperation<TRequest, TResponse> operation,
        TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : class
        where TResponse : class;
        
    Task<GatewayResult<TResponse>> ExecuteWithRetryAsync<TRequest, TResponse>(
        IGrpcOperation<TRequest, TResponse> operation,
        TRequest request,
        RetryPolicy retryPolicy,
        CancellationToken cancellationToken = default)
        where TRequest : class
        where TResponse : class;
        
    Task<bool> IsServiceAvailableAsync(string serviceName, CancellationToken cancellationToken = default);
    Task<ServiceHealthStatus> GetServiceHealthAsync(string serviceName, CancellationToken cancellationToken = default);
    
    GatewayStatistics GetStatistics();
    Task<GatewayStatistics> GetDetailedStatisticsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Enhanced gRPC operation interface
/// </summary>
public interface IGrpcOperation<in TRequest, TResponse>
    where TRequest : class
    where TResponse : class
{
    string ServiceName { get; }
    string OperationName { get; }
    TimeSpan? Timeout { get; }
    int Priority { get; }
    
    Task<TResponse> ExecuteAsync(TRequest request, CancellationToken cancellationToken = default);
    Task<bool> CanRetryAsync(Exception exception, int attemptNumber, CancellationToken cancellationToken = default);
    Task<TimeSpan> GetRetryDelayAsync(Exception exception, int attemptNumber, CancellationToken cancellationToken = default);
}

// ============================================================================
// ORCHESTRATION INTERFACE ENHANCEMENTS
// ============================================================================

/// <summary>
/// Enhanced process manager interface
/// </summary>
public interface IProcessManager<in TRequest, TResult>
{
    Task<TResult> ExecuteAsync(TRequest request, CancellationToken cancellationToken = default);
    Task<TResult> ExecuteWithTimeoutAsync(TRequest request, TimeSpan timeout, CancellationToken cancellationToken = default);
    Task<ProcessStatus> GetProcessStatusAsync(string processId, CancellationToken cancellationToken = default);
    
    // Process management
    Task<string> StartProcessAsync(TRequest request, CancellationToken cancellationToken = default);
    Task PauseProcessAsync(string processId, CancellationToken cancellationToken = default);
    Task ResumeProcessAsync(string processId, CancellationToken cancellationToken = default);
    Task CancelProcessAsync(string processId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Enhanced orchestration step interface
/// </summary>
public interface IOrchestrationStep
{
    string StepName { get; }
    int Order { get; }
    TimeSpan? Timeout { get; }
    bool IsCritical { get; }
    
    Task<StepResult> ExecuteAsync(OrchestrationContext context, CancellationToken cancellationToken = default);
    Task<bool> CanSkipAsync(OrchestrationContext context, CancellationToken cancellationToken = default);
    Task<StepResult> CompensateAsync(OrchestrationContext context, CancellationToken cancellationToken = default);
}

// ============================================================================
// SUPPORTING TYPES AND ENUMS
// ============================================================================

/// <summary>
/// Validation result with detailed information
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<ValidationError> Errors { get; set; } = new();
    public List<ValidationWarning> Warnings { get; set; } = new();
    public TimeSpan ValidationDuration { get; set; }
}

/// <summary>
/// Validation error details
/// </summary>
public class ValidationError
{
    public string Property { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public object? AttemptedValue { get; set; }
}

/// <summary>
/// Validation warning details
/// </summary>
public class ValidationWarning
{
    public string Property { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

/// <summary>
/// Sequence execution result
/// </summary>
public class ExecutionResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan ExecutionDuration { get; set; }
    public Dictionary<string, object?> Results { get; set; } = new();
    public List<ExecutionStep> Steps { get; set; } = new();
}

/// <summary>
/// Individual execution step result
/// </summary>
public class ExecutionStep
{
    public string Name { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan Duration { get; set; }
    public Dictionary<string, object?> Data { get; set; } = new();
}

/// <summary>
/// Resource health status
/// </summary>
public class ResourceHealthStatus
{
    public int ResourceId { get; set; }
    public string ResourceName { get; set; } = string.Empty;
    public HealthStatus Status { get; set; }
    public string? Message { get; set; }
    public DateTime LastChecked { get; set; }
    public TimeSpan ResponseTime { get; set; }
    public Dictionary<string, object?> Metrics { get; set; } = new();
}

/// <summary>
/// Service health status
/// </summary>
public class ServiceHealthStatus
{
    public string ServiceName { get; set; } = string.Empty;
    public HealthStatus Status { get; set; }
    public string? Message { get; set; }
    public DateTime LastChecked { get; set; }
    public TimeSpan ResponseTime { get; set; }
    public List<string> Dependencies { get; set; } = new();
}

/// <summary>
/// Process status information
/// </summary>
public class ProcessStatus
{
    public string ProcessId { get; set; } = string.Empty;
    public ProcessState State { get; set; }
    public string? CurrentStep { get; set; }
    public double CompletionPercentage { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan? EstimatedTimeRemaining { get; set; }
    public List<string> CompletedSteps { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Health status enumeration
/// </summary>
public enum HealthStatus
{
    Unknown = 0,
    Healthy = 1,
    Degraded = 2,
    Unhealthy = 3,
    Critical = 4
}

/// <summary>
/// Process state enumeration
/// </summary>
public enum ProcessState
{
    NotStarted = 0,
    Running = 1,
    Paused = 2,
    Completed = 3,
    Failed = 4,
    Cancelled = 5,
    CompensatingAsync = 6
}

/// <summary>
/// Resource type enumeration (example)
/// </summary>
public enum ResourceType
{
    Hardware = 1,
    Software = 2,
    Network = 3,
    Storage = 4,
    Compute = 5
}

/// <summary>
/// Sequence status enumeration (example)
/// </summary>
public enum SequenceStatus
{
    Draft = 0,
    Active = 1,
    Paused = 2,
    Completed = 3,
    Failed = 4,
    Archived = 5
}
