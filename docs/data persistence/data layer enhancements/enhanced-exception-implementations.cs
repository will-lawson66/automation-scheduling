using System.Text.Json;

namespace Instrument.Data.Exceptions;

/// <summary>
/// Base exception type for all Scheduler Data layer exceptions
/// </summary>
public class SchedulerDataException : Exception
{
    public string? CorrelationId { get; }
    public DateTime Timestamp { get; }

    public SchedulerDataException(string message, string? correlationId = null) : base(message) 
    { 
        CorrelationId = correlationId;
        Timestamp = DateTime.UtcNow;
    }

    public SchedulerDataException(string message, Exception innerException, string? correlationId = null)
        : base(message, innerException) 
    { 
        CorrelationId = correlationId;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Exception thrown when validation fails
/// </summary>
public class ValidationException : SchedulerDataException
{
    public string Value { get; }
    public string Reason { get; }
    public string? Field { get; }

    public ValidationException(int parameterId, string value, string reason, string? field = null, string? correlationId = null)
        : base($"Invalid value '{value}' for parameter {parameterId}: {reason}", correlationId)
    {
        Value = value;
        Reason = reason;
        Field = field;
    }
}

/// <summary>
/// Exception thrown when storage operations fail
/// </summary>
public class StorageProviderException : SchedulerDataException
{
    public string Operation { get; }

    public StorageProviderException(string operation, Exception innerException, string? correlationId = null)
        : base($"Storage operation '{operation}' failed", innerException, correlationId)
    {
        Operation = operation;
    }
}

/// <summary>
/// Exception thrown when a required entity is not found
/// </summary>
public class EntityNotFoundException : SchedulerDataException
{
    public string EntityType { get; }
    public int EntityId { get; }

    public EntityNotFoundException(string entityType, int entityId, string? correlationId = null)
        : base($"{entityType} with ID '{entityId}' not found.", correlationId)
    {
        EntityType = entityType;
        EntityId = entityId;
    }
}

// NEW GRPC GATEWAY EXCEPTIONS

/// <summary>
/// Base exception for all gRPC Gateway operations
/// </summary>
public class GrpcGatewayException : SchedulerDataException
{
    public string ServiceName { get; }
    public string OperationName { get; }
    public TimeSpan Duration { get; }
    public int AttemptCount { get; }

    public GrpcGatewayException(
        string serviceName, 
        string operationName, 
        string message, 
        TimeSpan duration,
        int attemptCount = 1,
        Exception? innerException = null,
        string? correlationId = null)
        : base(message, innerException, correlationId)
    {
        ServiceName = serviceName;
        OperationName = operationName;
        Duration = duration;
        AttemptCount = attemptCount;
    }
}

/// <summary>
/// Exception thrown when gRPC operations timeout
/// </summary>
public class GrpcTimeoutException : GrpcGatewayException
{
    public TimeSpan ConfiguredTimeout { get; }
    public bool IsOperationTimeout { get; }

    public GrpcTimeoutException(
        string serviceName,
        string operationName,
        TimeSpan configuredTimeout,
        TimeSpan actualDuration,
        bool isOperationTimeout,
        int attemptCount = 1,
        string? correlationId = null)
        : base(serviceName, operationName, 
               $"Operation {serviceName}.{operationName} timed out after {actualDuration.TotalMilliseconds}ms (configured: {configuredTimeout.TotalMilliseconds}ms)",
               actualDuration, attemptCount, null, correlationId)
    {
        ConfiguredTimeout = configuredTimeout;
        IsOperationTimeout = isOperationTimeout;
    }
}

/// <summary>
/// Exception thrown when gRPC service is unavailable
/// </summary>
public class GrpcServiceUnavailableException : GrpcGatewayException
{
    public DateTime? LastSuccessfulCall { get; }
    public string? HealthCheckDetails { get; }

    public GrpcServiceUnavailableException(
        string serviceName,
        string operationName,
        TimeSpan duration,
        DateTime? lastSuccessfulCall = null,
        string? healthCheckDetails = null,
        int attemptCount = 1,
        Exception? innerException = null,
        string? correlationId = null)
        : base(serviceName, operationName, 
               $"Service {serviceName} is currently unavailable",
               duration, attemptCount, innerException, correlationId)
    {
        LastSuccessfulCall = lastSuccessfulCall;
        HealthCheckDetails = healthCheckDetails;
    }
}

/// <summary>
/// Exception thrown when gateway concurrency limits are exceeded
/// </summary>
public class GrpcConcurrencyException : GrpcGatewayException
{
    public int MaxConcurrentRequests { get; }
    public int CurrentActiveRequests { get; }
    public TimeSpan WaitTime { get; }

    public GrpcConcurrencyException(
        string serviceName,
        string operationName,
        int maxConcurrentRequests,
        int currentActiveRequests,
        TimeSpan waitTime,
        string? correlationId = null)
        : base(serviceName, operationName,
               $"Concurrency limit exceeded for {serviceName}.{operationName}. Max: {maxConcurrentRequests}, Current: {currentActiveRequests}",
               waitTime, 1, null, correlationId)
    {
        MaxConcurrentRequests = maxConcurrentRequests;
        CurrentActiveRequests = currentActiveRequests;
        WaitTime = waitTime;
    }
}

// NEW ORCHESTRATION EXCEPTIONS

/// <summary>
/// Base exception for all orchestration operations
/// </summary>
public class OrchestrationException : SchedulerDataException
{
    public string? WorkflowName { get; }
    public List<string> CompletedSteps { get; }
    public string? CurrentStep { get; }
    public Dictionary<string, object?> ContextData { get; }

    public OrchestrationException(
        string message,
        string? workflowName = null,
        List<string>? completedSteps = null,
        string? currentStep = null,
        Dictionary<string, object?>? contextData = null,
        Exception? innerException = null,
        string? correlationId = null)
        : base(message, innerException, correlationId)
    {
        WorkflowName = workflowName;
        CompletedSteps = completedSteps ?? new List<string>();
        CurrentStep = currentStep;
        ContextData = contextData ?? new Dictionary<string, object?>();
    }
}

/// <summary>
/// Exception thrown when individual orchestration step fails
/// </summary>
public class OrchestrationStepException : OrchestrationException
{
    public string StepName { get; }
    public int StepOrder { get; }
    public bool ShouldContinue { get; }
    public TimeSpan StepDuration { get; }

    public OrchestrationStepException(
        string stepName,
        int stepOrder,
        string message,
        bool shouldContinue,
        TimeSpan stepDuration,
        string? workflowName = null,
        List<string>? completedSteps = null,
        Dictionary<string, object?>? contextData = null,
        Exception? innerException = null,
        string? correlationId = null)
        : base(message, workflowName, completedSteps, stepName, contextData, innerException, correlationId)
    {
        StepName = stepName;
        StepOrder = stepOrder;
        ShouldContinue = shouldContinue;
        StepDuration = stepDuration;
    }
}

/// <summary>
/// Exception thrown when orchestration workflow times out
/// </summary>
public class OrchestrationTimeoutException : OrchestrationException
{
    public TimeSpan ConfiguredTimeout { get; }
    public TimeSpan ActualDuration { get; }
    public double CompletionPercentage { get; }

    public OrchestrationTimeoutException(
        TimeSpan configuredTimeout,
        TimeSpan actualDuration,
        double completionPercentage,
        string? workflowName = null,
        List<string>? completedSteps = null,
        string? currentStep = null,
        Dictionary<string, object?>? contextData = null,
        string? correlationId = null)
        : base($"Orchestration workflow {workflowName} timed out after {actualDuration.TotalMilliseconds}ms (configured: {configuredTimeout.TotalMilliseconds}ms). Completion: {completionPercentage:P}",
               workflowName, completedSteps, currentStep, contextData, null, correlationId)
    {
        ConfiguredTimeout = configuredTimeout;
        ActualDuration = actualDuration;
        CompletionPercentage = completionPercentage;
    }
}

// NEW RESILIENCE PATTERN EXCEPTIONS

/// <summary>
/// Exception thrown when retry policies are exhausted
/// </summary>
public class RetryPolicyException : SchedulerDataException
{
    public int MaxAttempts { get; }
    public int ActualAttempts { get; }
    public TimeSpan TotalDuration { get; }
    public List<Exception> AttemptExceptions { get; }
    public string PolicyType { get; }

    public RetryPolicyException(
        string policyType,
        int maxAttempts,
        int actualAttempts,
        TimeSpan totalDuration,
        List<Exception> attemptExceptions,
        string? correlationId = null)
        : base($"Retry policy '{policyType}' exhausted after {actualAttempts} attempts over {totalDuration.TotalMilliseconds}ms",
               attemptExceptions.LastOrDefault(), correlationId)
    {
        PolicyType = policyType;
        MaxAttempts = maxAttempts;
        ActualAttempts = actualAttempts;
        TotalDuration = totalDuration;
        AttemptExceptions = attemptExceptions;
    }
}

/// <summary>
/// Exception thrown when circuit breaker is open
/// </summary>
public class CircuitBreakerException : SchedulerDataException
{
    public string CircuitName { get; }
    public DateTime OpenedAt { get; }
    public TimeSpan EstimatedRecoveryTime { get; }
    public int FailureCount { get; }
    public int FailureThreshold { get; }

    public CircuitBreakerException(
        string circuitName,
        DateTime openedAt,
        TimeSpan estimatedRecoveryTime,
        int failureCount,
        int failureThreshold,
        string? correlationId = null)
        : base($"Circuit breaker '{circuitName}' is open. Failures: {failureCount}/{failureThreshold}. Estimated recovery: {estimatedRecoveryTime.TotalSeconds}s",
               null, correlationId)
    {
        CircuitName = circuitName;
        OpenedAt = openedAt;
        EstimatedRecoveryTime = estimatedRecoveryTime;
        FailureCount = failureCount;
        FailureThreshold = failureThreshold;
    }
}

// EXTENSION METHODS FOR STRUCTURED LOGGING

public static class ExceptionExtensions
{
    /// <summary>
    /// Convert exception to structured data for logging
    /// </summary>
    public static Dictionary<string, object?> ToStructuredData(this SchedulerDataException exception)
    {
        var data = new Dictionary<string, object?>
        {
            ["ExceptionType"] = exception.GetType().Name,
            ["Message"] = exception.Message,
            ["CorrelationId"] = exception.CorrelationId,
            ["Timestamp"] = exception.Timestamp,
            ["StackTrace"] = exception.StackTrace
        };

        // Add specific data based on exception type
        switch (exception)
        {
            case GrpcGatewayException grpcEx:
                data["ServiceName"] = grpcEx.ServiceName;
                data["OperationName"] = grpcEx.OperationName;
                data["Duration"] = grpcEx.Duration.TotalMilliseconds;
                data["AttemptCount"] = grpcEx.AttemptCount;
                break;

            case OrchestrationException orchEx:
                data["WorkflowName"] = orchEx.WorkflowName;
                data["CompletedSteps"] = orchEx.CompletedSteps;
                data["CurrentStep"] = orchEx.CurrentStep;
                data["ContextDataKeys"] = orchEx.ContextData.Keys.ToList();
                break;

            case RetryPolicyException retryEx:
                data["PolicyType"] = retryEx.PolicyType;
                data["MaxAttempts"] = retryEx.MaxAttempts;
                data["ActualAttempts"] = retryEx.ActualAttempts;
                data["TotalDuration"] = retryEx.TotalDuration.TotalMilliseconds;
                break;
        }

        return data;
    }

    /// <summary>
    /// Check if exception indicates a transient failure that might benefit from retry
    /// </summary>
    public static bool IsTransient(this Exception exception)
    {
        return exception switch
        {
            GrpcTimeoutException => true,
            GrpcServiceUnavailableException => true,
            StorageProviderException storageEx when IsTransientStorageException(storageEx) => true,
            TaskCanceledException => true,
            TimeoutException => true,
            _ => false
        };
    }

    private static bool IsTransientStorageException(StorageProviderException ex)
    {
        // Check inner exception for transient database errors
        return ex.InnerException switch
        {
            TimeoutException => true,
            OperationCanceledException => true,
            _ => false
        };
    }
}
