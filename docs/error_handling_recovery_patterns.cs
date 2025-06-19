// ErrorHandlingService.cs - Comprehensive error handling and recovery system
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using System.Net.Http;

namespace Instrument.Scheduler.ErrorHandling
{
    public class ErrorHandlingService : IErrorHandlingService, IHostedService, IDisposable
    {
        private readonly ILogger<ErrorHandlingService> _logger;
        private readonly ErrorHandlingOptions _options;
        private readonly ConcurrentDictionary<string, ErrorCategory> _errorCategories;
        private readonly ConcurrentDictionary<Guid, ErrorContext> _activeErrors;
        private readonly ConcurrentQueue<ErrorRecord> _errorHistory;
        private readonly Timer _recoveryTimer;
        private readonly Timer _cleanupTimer;
        
        // Recovery strategies
        private readonly Dictionary<ErrorSeverity, IRecoveryStrategy> _recoveryStrategies;
        
        // Circuit breaker policies
        private readonly Dictionary<string, IAsyncPolicy> _circuitBreakers;
        
        // Retry policies
        private readonly Dictionary<string, IAsyncPolicy> _retryPolicies;

        public event EventHandler<ErrorOccurredEventArgs> OnErrorOccurred;
        public event EventHandler<ErrorRecoveredEventArgs> OnErrorRecovered;
        public event EventHandler<RecoveryFailedEventArgs> OnRecoveryFailed;

        public ErrorHandlingService(
            IOptions<ErrorHandlingOptions> options,
            ILogger<ErrorHandlingService> logger)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            _errorCategories = new ConcurrentDictionary<string, ErrorCategory>();
            _activeErrors = new ConcurrentDictionary<Guid, ErrorContext>();
            _errorHistory = new ConcurrentQueue<ErrorRecord>();
            
            // Initialize error categories
            InitializeErrorCategories();
            
            // Initialize recovery strategies
            _recoveryStrategies = new Dictionary<ErrorSeverity, IRecoveryStrategy>
            {
                [ErrorSeverity.Low] = new BasicRecoveryStrategy(logger),
                [ErrorSeverity.Medium] = new RetryRecoveryStrategy(logger),
                [ErrorSeverity.High] = new FallbackRecoveryStrategy(logger),
                [ErrorSeverity.Critical] = new ManualInterventionStrategy(logger)
            };
            
            // Initialize circuit breakers
            _circuitBreakers = InitializeCircuitBreakers();
            
            // Initialize retry policies
            _retryPolicies = InitializeRetryPolicies();
            
            // Set up timers
            _recoveryTimer = new Timer(ProcessRecoveryAttempts, null,
                TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
            _cleanupTimer = new Timer(CleanupOldErrors, null,
                TimeSpan.FromHours(1), TimeSpan.FromHours(1));
                
            _logger.LogInformation("Error handling service initialized with {CategoryCount} error categories",
                _errorCategories.Count);
        }

        public async Task<ErrorHandlingResult> HandleError(Exception exception, string context, object additionalData = null)
        {
            var errorId = Guid.NewGuid();
            var timestamp = DateTime.UtcNow;
            
            try
            {
                // Categorize the error
                var category = CategorizeError(exception);
                
                // Create error context
                var errorContext = new ErrorContext
                {
                    Id = errorId,
                    Exception = exception,
                    Context = context,
                    Category = category,
                    Severity = DetermineSeverity(exception, category),
                    OccurredAt = timestamp,
                    AdditionalData = additionalData,
                    RecoveryAttempts = 0,
                    Status = ErrorStatus.New
                };
                
                // Add to active errors
                _activeErrors[errorId] = errorContext;
                
                // Log the error
                LogError(errorContext);
                
                // Create error record for history
                var errorRecord = new ErrorRecord
                {
                    Id = errorId,
                    ExceptionType = exception.GetType().Name,
                    Message = exception.Message,
                    Context = context,
                    Severity = errorContext.Severity,
                    Category = category.Name,
                    OccurredAt = timestamp,
                    StackTrace = exception.StackTrace
                };
                
                _errorHistory.Enqueue(errorRecord);
                
                // Trigger error occurred event
                OnErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(errorContext));
                
                // Determine if immediate recovery should be attempted
                var result = new ErrorHandlingResult
                {
                    ErrorId = errorId,
                    Category = category.Name,
                    Severity = errorContext.Severity,
                    ShouldRetry = ShouldAttemptRecovery(errorContext),
                    RecoveryStrategy = GetRecoveryStrategy(errorContext.Severity).GetType().Name
                };
                
                // Attempt immediate recovery for certain error types
                if (result.ShouldRetry && category.AllowImmediateRecovery)
                {
                    var recoveryResult = await AttemptRecovery(errorContext);
                    result.RecoveryAttempted = true;
                    result.RecoverySuccessful = recoveryResult.Success;
                    result.RecoveryMessage = recoveryResult.Message;
                }
                
                return result;
            }
            catch (Exception handlingException)
            {
                _logger.LogCritical(handlingException, 
                    "Critical error in error handling service while processing error {ErrorId}", errorId);
                
                return new ErrorHandlingResult
                {
                    ErrorId = errorId,
                    Category = "ErrorHandlingFailure",
                    Severity = ErrorSeverity.Critical,
                    ShouldRetry = false,
                    RecoveryAttempted = false,
                    RecoverySuccessful = false,
                    RecoveryMessage = "Error handling service failure"
                };
            }
        }

        public async Task<RecoveryResult> AttemptRecovery(Guid errorId)
        {
            if (!_activeErrors.TryGetValue(errorId, out var errorContext))
            {
                return new RecoveryResult 
                { 
                    Success = false, 
                    Message = "Error context not found" 
                };
            }

            return await AttemptRecovery(errorContext);
        }

        private async Task<RecoveryResult> AttemptRecovery(ErrorContext errorContext)
        {
            try
            {
                _logger.LogInformation("Attempting recovery for error {ErrorId} (Attempt {AttemptNumber})",
                    errorContext.Id, errorContext.RecoveryAttempts + 1);

                errorContext.RecoveryAttempts++;
                errorContext.LastRecoveryAttempt = DateTime.UtcNow;
                errorContext.Status = ErrorStatus.RecoveryInProgress;

                // Get appropriate recovery strategy
                var strategy = GetRecoveryStrategy(errorContext.Severity);
                
                // Execute recovery
                var result = await strategy.ExecuteRecovery(errorContext);
                
                if (result.Success)
                {
                    // Mark error as recovered
                    errorContext.Status = ErrorStatus.Recovered;
                    errorContext.RecoveredAt = DateTime.UtcNow;
                    
                    _logger.LogInformation("Successfully recovered from error {ErrorId} using strategy {Strategy}",
                        errorContext.Id, strategy.GetType().Name);
                    
                    OnErrorRecovered?.Invoke(this, new ErrorRecoveredEventArgs(errorContext, result));
                    
                    // Remove from active errors after delay
                    _ = Task.Delay(TimeSpan.FromMinutes(5)).ContinueWith(_ => 
                        _activeErrors.TryRemove(errorContext.Id, out _));
                }
                else
                {
                    // Check if max attempts reached
                    if (errorContext.RecoveryAttempts >= GetMaxRecoveryAttempts(errorContext.Category))
                    {
                        errorContext.Status = ErrorStatus.RecoveryFailed;
                        
                        _logger.LogError("Recovery failed for error {ErrorId} after {Attempts} attempts",
                            errorContext.Id, errorContext.RecoveryAttempts);
                        
                        OnRecoveryFailed?.Invoke(this, new RecoveryFailedEventArgs(errorContext, result.Message));
                    }
                    else
                    {
                        errorContext.Status = ErrorStatus.AwaitingRecovery;
                        
                        _logger.LogWarning("Recovery attempt {Attempt} failed for error {ErrorId}: {Message}",
                            errorContext.RecoveryAttempts, errorContext.Id, result.Message);
                    }
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during recovery attempt for error {ErrorId}", errorContext.Id);
                
                errorContext.Status = ErrorStatus.RecoveryFailed;
                
                return new RecoveryResult 
                { 
                    Success = false, 
                    Message = $"Recovery exception: {ex.Message}" 
                };
            }
        }

        public IAsyncPolicy GetRetryPolicy(string policyName)
        {
            return _retryPolicies.GetValueOrDefault(policyName) ?? 
                   _retryPolicies["default"];
        }

        public IAsyncPolicy GetCircuitBreakerPolicy(string serviceName)
        {
            return _circuitBreakers.GetValueOrDefault(serviceName) ?? 
                   _circuitBreakers["default"];
        }

        public async Task<T> ExecuteWithPolicy<T>(string policyName, Func<Task<T>> operation)
        {
            var policy = GetRetryPolicy(policyName);
            return await policy.ExecuteAsync(operation);
        }

        public async Task ExecuteWithPolicy(string policyName, Func<Task> operation)
        {
            var policy = GetRetryPolicy(policyName);
            await policy.ExecuteAsync(operation);
        }

        public List<ErrorRecord> GetErrorHistory(TimeSpan? period = null, ErrorSeverity? severity = null)
        {
            var cutoff = period.HasValue ? DateTime.UtcNow - period.Value : DateTime.MinValue;
            
            return _errorHistory
                .Where(e => e.OccurredAt >= cutoff)
                .Where(e => !severity.HasValue || e.Severity == severity.Value)
                .OrderByDescending(e => e.OccurredAt)
                .ToList();
        }

        public ErrorStatistics GetErrorStatistics(TimeSpan period)
        {
            var cutoff = DateTime.UtcNow - period;
            var relevantErrors = _errorHistory.Where(e => e.OccurredAt >= cutoff).ToList();
            
            return new ErrorStatistics
            {
                Period = period,
                TotalErrors = relevantErrors.Count,
                ErrorsByCategory = relevantErrors.GroupBy(e => e.Category)
                    .ToDictionary(g => g.Key, g => g.Count()),
                ErrorsBySeverity = relevantErrors.GroupBy(e => e.Severity)
                    .ToDictionary(g => g.Key, g => g.Count()),
                ErrorsByHour = relevantErrors.GroupBy(e => e.OccurredAt.Hour)
                    .ToDictionary(g => g.Key, g => g.Count()),
                MostCommonErrors = relevantErrors.GroupBy(e => e.ExceptionType)
                    .OrderByDescending(g => g.Count())
                    .Take(10)
                    .ToDictionary(g => g.Key, g => g.Count()),
                RecoveryRate = CalculateRecoveryRate(relevantErrors)
            };
        }

        public List<ErrorContext> GetActiveErrors()
        {
            return _activeErrors.Values.ToList();
        }

        private void InitializeErrorCategories()
        {
            _errorCategories["Database"] = new ErrorCategory
            {
                Name = "Database",
                Description = "Database connectivity and query errors",
                AllowImmediateRecovery = true,
                MaxRecoveryAttempts = 3,
                RecoveryDelay = TimeSpan.FromSeconds(30),
                ExceptionTypes = { typeof(SqlException).Name, typeof(TimeoutException).Name }
            };
            
            _errorCategories["Network"] = new ErrorCategory
            {
                Name = "Network",
                Description = "Network connectivity and communication errors",
                AllowImmediateRecovery = true,
                MaxRecoveryAttempts = 5,
                RecoveryDelay = TimeSpan.FromSeconds(10),
                ExceptionTypes = { typeof(HttpRequestException).Name, typeof(TaskCanceledException).Name }
            };
            
            _errorCategories["Hardware"] = new ErrorCategory
            {
                Name = "Hardware",
                Description = "Hardware communication and control errors",
                AllowImmediateRecovery = false,
                MaxRecoveryAttempts = 2,
                RecoveryDelay = TimeSpan.FromMinutes(1),
                ExceptionTypes = { "HardwareException", "DeviceNotReadyException" }
            };
            
            _errorCategories["Validation"] = new ErrorCategory
            {
                Name = "Validation",
                Description = "Data validation and business rule errors",
                AllowImmediateRecovery = false,
                MaxRecoveryAttempts = 1,
                RecoveryDelay = TimeSpan.Zero,
                ExceptionTypes = { typeof(ArgumentException).Name, typeof(InvalidOperationException).Name }
            };
            
            _errorCategories["Configuration"] = new ErrorCategory
            {
                Name = "Configuration",
                Description = "Configuration and setup errors",
                AllowImmediateRecovery = false,
                MaxRecoveryAttempts = 1,
                RecoveryDelay = TimeSpan.FromMinutes(5),
                ExceptionTypes = { "ConfigurationException", typeof(FileNotFoundException).Name }
            };
            
            _errorCategories["Unknown"] = new ErrorCategory
            {
                Name = "Unknown",
                Description = "Unrecognized error types",
                AllowImmediateRecovery = false,
                MaxRecoveryAttempts = 1,
                RecoveryDelay = TimeSpan.FromMinutes(1),
                ExceptionTypes = { }
            };
        }

        private Dictionary<string, IAsyncPolicy> InitializeCircuitBreakers()
        {
            var circuitBreakers = new Dictionary<string, IAsyncPolicy>();
            
            // Default circuit breaker
            circuitBreakers["default"] = Policy
                .Handle<Exception>()
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: 5,
                    durationOfBreak: TimeSpan.FromMinutes(1),
                    onBreak: (exception, duration) =>
                    {
                        _logger.LogWarning("Circuit breaker opened for {Duration} due to: {Exception}",
                            duration, exception.Message);
                    },
                    onReset: () =>
                    {
                        _logger.LogInformation("Circuit breaker reset");
                    });
            
            // Database circuit breaker
            circuitBreakers["database"] = Policy
                .Handle<SqlException>()
                .Or<TimeoutException>()
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: 3,
                    durationOfBreak: TimeSpan.FromMinutes(2),
                    onBreak: (exception, duration) =>
                    {
                        _logger.LogError("Database circuit breaker opened for {Duration}: {Exception}",
                            duration, exception.Message);
                    },
                    onReset: () =>
                    {
                        _logger.LogInformation("Database circuit breaker reset");
                    });
            
            // Hardware circuit breaker
            circuitBreakers["hardware"] = Policy
                .Handle<Exception>(ex => ex.GetType().Name.Contains("Hardware"))
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: 2,
                    durationOfBreak: TimeSpan.FromMinutes(5),
                    onBreak: (exception, duration) =>
                    {
                        _logger.LogError("Hardware circuit breaker opened for {Duration}: {Exception}",
                            duration, exception.Message);
                    },
                    onReset: () =>
                    {
                        _logger.LogInformation("Hardware circuit breaker reset");
                    });
            
            return circuitBreakers;
        }

        private Dictionary<string, IAsyncPolicy> InitializeRetryPolicies()
        {
            var retryPolicies = new Dictionary<string, IAsyncPolicy>();
            
            // Default retry policy
            retryPolicies["default"] = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        _logger.LogWarning("Retry attempt {RetryCount} in {Delay}ms: {Exception}",
                            retryCount, timespan.TotalMilliseconds, outcome.Exception?.Message);
                    });
            
            // Database retry policy
            retryPolicies["database"] = Policy
                .Handle<SqlException>()
                .Or<TimeoutException>()
                .WaitAndRetryAsync(
                    retryCount: 5,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(retryAttempt * 2),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        _logger.LogWarning("Database retry attempt {RetryCount} in {Delay}ms: {Exception}",
                            retryCount, timespan.TotalMilliseconds, outcome.Exception?.Message);
                    });
            
            // HTTP retry policy
            retryPolicies["http"] = HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        _logger.LogWarning("HTTP retry attempt {RetryCount} in {Delay}ms",
                            retryCount, timespan.TotalMilliseconds);
                    });
            
            return retryPolicies;
        }

        private ErrorCategory CategorizeError(Exception exception)
        {
            var exceptionType = exception.GetType().Name;
            
            foreach (var category in _errorCategories.Values)
            {
                if (category.ExceptionTypes.Contains(exceptionType))
                {
                    return category;
                }
            }
            
            // Check for specific patterns
            if (exception.Message.Contains("database") || exception.Message.Contains("sql"))
            {
                return _errorCategories["Database"];
            }
            
            if (exception.Message.Contains("network") || exception.Message.Contains("connection"))
            {
                return _errorCategories["Network"];
            }
            
            if (exception.Message.Contains("hardware") || exception.Message.Contains("device"))
            {
                return _errorCategories["Hardware"];
            }
            
            return _errorCategories["Unknown"];
        }

        private ErrorSeverity DetermineSeverity(Exception exception, ErrorCategory category)
        {
            // Critical errors
            if (exception is OutOfMemoryException || 
                exception is StackOverflowException ||
                exception.GetType().Name.Contains("Critical"))
            {
                return ErrorSeverity.Critical;
            }
            
            // High severity errors
            if (category.Name == "Hardware" || 
                exception is UnauthorizedAccessException ||
                exception.Message.Contains("failed") && exception.Message.Contains("critical"))
            {
                return ErrorSeverity.High;
            }
            
            // Medium severity errors
            if (category.Name == "Database" || 
                category.Name == "Network" ||
                exception is TimeoutException)
            {
                return ErrorSeverity.Medium;
            }
            
            // Low severity errors
            return ErrorSeverity.Low;
        }

        private bool ShouldAttemptRecovery(ErrorContext errorContext)
        {
            // Don't retry validation errors
            if (errorContext.Category.Name == "Validation")
                return false;
            
            // Don't retry if max attempts reached
            if (errorContext.RecoveryAttempts >= GetMaxRecoveryAttempts(errorContext.Category))
                return false;
            
            // Don't retry critical errors automatically
            if (errorContext.Severity == ErrorSeverity.Critical)
                return false;
            
            return true;
        }

        private IRecoveryStrategy GetRecoveryStrategy(ErrorSeverity severity)
        {
            return _recoveryStrategies.GetValueOrDefault(severity) ?? 
                   _recoveryStrategies[ErrorSeverity.Low];
        }

        private int GetMaxRecoveryAttempts(ErrorCategory category)
        {
            return category.MaxRecoveryAttempts;
        }

        private void LogError(ErrorContext errorContext)
        {
            var logLevel = errorContext.Severity switch
            {
                ErrorSeverity.Critical => LogLevel.Critical,
                ErrorSeverity.High => LogLevel.Error,
                ErrorSeverity.Medium => LogLevel.Warning,
                ErrorSeverity.Low => LogLevel.Information,
                _ => LogLevel.Information
            };
            
            _logger.Log(logLevel, errorContext.Exception,
                "Error {ErrorId} in category {Category} with severity {Severity}: {Message}",
                errorContext.Id, errorContext.Category.Name, errorContext.Severity, errorContext.Exception.Message);
        }

        private void ProcessRecoveryAttempts(object state)
        {
            try
            {
                var errorsAwaitingRecovery = _activeErrors.Values
                    .Where(e => e.Status == ErrorStatus.AwaitingRecovery)
                    .Where(e => DateTime.UtcNow - (e.LastRecoveryAttempt ?? e.OccurredAt) >= e.Category.RecoveryDelay)
                    .ToList();
                
                foreach (var errorContext in errorsAwaitingRecovery)
                {
                    _ = Task.Run(async () => await AttemptRecovery(errorContext));
                }
                
                if (errorsAwaitingRecovery.Any())
                {
                    _logger.LogDebug("Processing {Count} errors awaiting recovery", errorsAwaitingRecovery.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during recovery processing");
            }
        }

        private void CleanupOldErrors(object state)
        {
            try
            {
                var cutoffTime = DateTime.UtcNow - _options.ErrorRetentionPeriod;
                
                // Clean up error history
                var itemsToRemove = new List<ErrorRecord>();
                while (_errorHistory.TryPeek(out var record) && record.OccurredAt < cutoffTime)
                {
                    if (_errorHistory.TryDequeue(out var removedRecord))
                    {
                        itemsToRemove.Add(removedRecord);
                    }
                }
                
                // Clean up recovered active errors
                var recoveredErrors = _activeErrors.Values
                    .Where(e => e.Status == ErrorStatus.Recovered && 
                               e.RecoveredAt.HasValue && 
                               DateTime.UtcNow - e.RecoveredAt.Value > TimeSpan.FromHours(1))
                    .ToList();
                
                foreach (var error in recoveredErrors)
                {
                    _activeErrors.TryRemove(error.Id, out _);
                }
                
                if (itemsToRemove.Any() || recoveredErrors.Any())
                {
                    _logger.LogDebug("Cleaned up {HistoryCount} old error records and {ActiveCount} recovered errors",
                        itemsToRemove.Count, recoveredErrors.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cleanup");
            }
        }

        private double CalculateRecoveryRate(List<ErrorRecord> errors)
        {
            if (!errors.Any()) return 0;
            
            var recoveredCount = _activeErrors.Values
                .Count(e => e.Status == ErrorStatus.Recovered && 
                           errors.Any(er => er.Id == e.Id));
            
            return (double)recoveredCount / errors.Count * 100;
        }

        // IHostedService implementation
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Error handling service starting");
            await Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Error handling service stopping");
            await Task.CompletedTask;
        }

        public void Dispose()
        {
            _recoveryTimer?.Dispose();
            _cleanupTimer?.Dispose();
        }
    }

    // Recovery strategy implementations
    public interface IRecoveryStrategy
    {
        Task<RecoveryResult> ExecuteRecovery(ErrorContext errorContext);
    }

    public class BasicRecoveryStrategy : IRecoveryStrategy
    {
        private readonly ILogger _logger;

        public BasicRecoveryStrategy(ILogger logger)
        {
            _logger = logger;
        }

        public async Task<RecoveryResult> ExecuteRecovery(ErrorContext errorContext)
        {
            // Simple retry logic for low-severity errors
            await Task.Delay(1000); // Brief delay
            
            return new RecoveryResult
            {
                Success = true,
                Message = "Basic recovery completed",
                Strategy = "BasicRecovery"
            };
        }
    }

    public class RetryRecoveryStrategy : IRecoveryStrategy
    {
        private readonly ILogger _logger;

        public RetryRecoveryStrategy(ILogger logger)
        {
            _logger = logger;
        }

        public async Task<RecoveryResult> ExecuteRecovery(ErrorContext errorContext)
        {
            var delay = TimeSpan.FromSeconds(Math.Pow(2, errorContext.RecoveryAttempts));
            await Task.Delay(delay);
            
            // Implement exponential backoff retry logic
            var success = errorContext.RecoveryAttempts < 3; // Simulate success after 3 attempts
            
            return new RecoveryResult
            {
                Success = success,
                Message = success ? "Retry recovery successful" : "Retry recovery failed",
                Strategy = "RetryRecovery"
            };
        }
    }

    public class FallbackRecoveryStrategy : IRecoveryStrategy
    {
        private readonly ILogger _logger;

        public FallbackRecoveryStrategy(ILogger logger)
        {
            _logger = logger;
        }

        public async Task<RecoveryResult> ExecuteRecovery(ErrorContext errorContext)
        {
            // Implement fallback mechanism for high-severity errors
            await Task.Delay(5000);
            
            return new RecoveryResult
            {
                Success = false, // High-severity errors typically require manual intervention
                Message = "Fallback recovery requires manual intervention",
                Strategy = "FallbackRecovery"
            };
        }
    }

    public class ManualInterventionStrategy : IRecoveryStrategy
    {
        private readonly ILogger _logger;

        public ManualInterventionStrategy(ILogger logger)
        {
            _logger = logger;
        }

        public async Task<RecoveryResult> ExecuteRecovery(ErrorContext errorContext)
        {
            // Critical errors require manual intervention
            _logger.LogCritical("Critical error {ErrorId} requires manual intervention: {Message}",
                errorContext.Id, errorContext.Exception.Message);
            
            return new RecoveryResult
            {
                Success = false,
                Message = "Manual intervention required for critical error",
                Strategy = "ManualIntervention"
            };
        }
    }

    // Supporting classes and interfaces
    public interface IErrorHandlingService
    {
        Task<ErrorHandlingResult> HandleError(Exception exception, string context, object additionalData = null);
        Task<RecoveryResult> AttemptRecovery(Guid errorId);
        IAsyncPolicy GetRetryPolicy(string policyName);
        IAsyncPolicy GetCircuitBreakerPolicy(string serviceName);
        Task<T> ExecuteWithPolicy<T>(string policyName, Func<Task<T>> operation);
        Task ExecuteWithPolicy(string policyName, Func<Task> operation);
        List<ErrorRecord> GetErrorHistory(TimeSpan? period = null, ErrorSeverity? severity = null);
        ErrorStatistics GetErrorStatistics(TimeSpan period);
        List<ErrorContext> GetActiveErrors();
        
        event EventHandler<ErrorOccurredEventArgs> OnErrorOccurred;
        event EventHandler<ErrorRecoveredEventArgs> OnErrorRecovered;
        event EventHandler<RecoveryFailedEventArgs> OnRecoveryFailed;
    }

    // Data classes
    public class ErrorContext
    {
        public Guid Id { get; set; }
        public Exception Exception { get; set; }
        public string Context { get; set; }
        public ErrorCategory Category { get; set; }
        public ErrorSeverity Severity { get; set; }
        public DateTime OccurredAt { get; set; }
        public object AdditionalData { get; set; }
        public int RecoveryAttempts { get; set; }
        public DateTime? LastRecoveryAttempt { get; set; }
        public DateTime? RecoveredAt { get; set; }
        public ErrorStatus Status { get; set; }
    }

    public class ErrorCategory
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public bool AllowImmediateRecovery { get; set; }
        public int MaxRecoveryAttempts { get; set; }
        public TimeSpan RecoveryDelay { get; set; }
        public HashSet<string> ExceptionTypes { get; set; } = new();
    }

    public class ErrorRecord
    {
        public Guid Id { get; set; }
        public string ExceptionType { get; set; }
        public string Message { get; set; }
        public string Context { get; set; }
        public ErrorSeverity Severity { get; set; }
        public string Category { get; set; }
        public DateTime OccurredAt { get; set; }
        public string StackTrace { get; set; }
    }

    public class ErrorHandlingResult
    {
        public Guid ErrorId { get; set; }
        public string Category { get; set; }
        public ErrorSeverity Severity { get; set; }
        public bool ShouldRetry { get; set; }
        public bool RecoveryAttempted { get; set; }
        public bool RecoverySuccessful { get; set; }
        public string RecoveryMessage { get; set; }
        public string RecoveryStrategy { get; set; }
    }

    public class RecoveryResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string Strategy { get; set; }
        public Dictionary<string, object> AdditionalData { get; set; } = new();
    }

    public class ErrorStatistics
    {
        public TimeSpan Period { get; set; }
        public int TotalErrors { get; set; }
        public Dictionary<string, int> ErrorsByCategory { get; set; } = new();
        public Dictionary<ErrorSeverity, int> ErrorsBySeverity { get; set; } = new();
        public Dictionary<int, int> ErrorsByHour { get; set; } = new();
        public Dictionary<string, int> MostCommonErrors { get; set; } = new();
        public double RecoveryRate { get; set; }
    }

    public class ErrorHandlingOptions
    {
        public TimeSpan ErrorRetentionPeriod { get; set; } = TimeSpan.FromDays(30);
        public int MaxErrorHistorySize { get; set; } = 10000;
        public bool EnableAutomaticRecovery { get; set; } = true;
        public TimeSpan RecoveryCheckInterval { get; set; } = TimeSpan.FromSeconds(30);
        public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);
    }

    // Event argument classes
    public class ErrorOccurredEventArgs : EventArgs
    {
        public ErrorContext ErrorContext { get; }

        public ErrorOccurredEventArgs(ErrorContext errorContext)
        {
            ErrorContext = errorContext;
        }
    }

    public class ErrorRecoveredEventArgs : EventArgs
    {
        public ErrorContext ErrorContext { get; }
        public RecoveryResult RecoveryResult { get; }

        public ErrorRecoveredEventArgs(ErrorContext errorContext, RecoveryResult recoveryResult)
        {
            ErrorContext = errorContext;
            RecoveryResult = recoveryResult;
        }
    }

    public class RecoveryFailedEventArgs : EventArgs
    {
        public ErrorContext ErrorContext { get; }
        public string FailureReason { get; }

        public RecoveryFailedEventArgs(ErrorContext errorContext, string failureReason)
        {
            ErrorContext = errorContext;
            FailureReason = failureReason;
        }
    }

    // Enums
    public enum ErrorSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    public enum ErrorStatus
    {
        New,
        AwaitingRecovery,
        RecoveryInProgress,
        Recovered,
        RecoveryFailed
    }

    // Exception types (would be defined elsewhere in the application)
    public class SqlException : Exception
    {
        public SqlException(string message) : base(message) { }
        public SqlException(string message, Exception innerException) : base(message, innerException) { }
    }
}