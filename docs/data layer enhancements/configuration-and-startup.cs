// ============================================================================
// CONFIGURATION CLASSES
// ============================================================================

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Instrument.Data.Configuration;

/// <summary>
/// Configuration options for timeout management across the application
/// </summary>
public class TimeoutOptions
{
    public const string SectionName = "Timeouts";
    
    /// <summary>
    /// Timeout for database operations (EF Core queries)
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
    /// Timeout for gRPC operations
    /// </summary>
    public TimeSpan GrpcTimeout { get; set; } = TimeSpan.FromSeconds(15);
    
    /// <summary>
    /// Timeout for background operations
    /// </summary>
    public TimeSpan BackgroundOperationTimeout { get; set; } = TimeSpan.FromMinutes(5);
    
    /// <summary>
    /// Timeout for validation operations
    /// </summary>
    public TimeSpan ValidationTimeout { get; set; } = TimeSpan.FromSeconds(5);
    
    /// <summary>
    /// Timeout for orchestration workflows
    /// </summary>
    public TimeSpan OrchestrationTimeout { get; set; } = TimeSpan.FromMinutes(10);
    
    /// <summary>
    /// Timeout for individual orchestration steps
    /// </summary>
    public TimeSpan OrchestrationStepTimeout { get; set; } = TimeSpan.FromMinutes(2);
    
    /// <summary>
    /// Timeout for health checks
    /// </summary>
    public TimeSpan HealthCheckTimeout { get; set; } = TimeSpan.FromSeconds(10);
}

/// <summary>
/// Configuration options for retry policies
/// </summary>
public class RetryPolicyOptions
{
    public const string SectionName = "RetryPolicy";
    
    /// <summary>
    /// Maximum number of retry attempts
    /// </summary>
    public int MaxAttempts { get; set; } = 3;
    
    /// <summary>
    /// Base delay between retries in milliseconds
    /// </summary>
    public int BaseDelayMs { get; set; } = 1000;
    
    /// <summary>
    /// Backoff multiplier for exponential backoff
    /// </summary>
    public double BackoffMultiplier { get; set; } = 2.0;
    
    /// <summary>
    /// Maximum delay between retries in milliseconds
    /// </summary>
    public int MaxDelayMs { get; set; } = 30000;
    
    /// <summary>
    /// Whether to use jitter in retry delays
    /// </summary>
    public bool UseJitter { get; set; } = true;
    
    /// <summary>
    /// Jitter percentage (0.0 to 1.0)
    /// </summary>
    public double JitterPercentage { get; set; } = 0.1;
}

/// <summary>
/// Configuration options for orchestration workflows
/// </summary>
public class OrchestrationOptions
{
    public const string SectionName = "Orchestration";
    
    /// <summary>
    /// Default workflow timeout
    /// </summary>
    public TimeSpan DefaultWorkflowTimeout { get; set; } = TimeSpan.FromMinutes(30);
    
    /// <summary>
    /// Maximum number of concurrent workflows
    /// </summary>
    public int MaxConcurrentWorkflows { get; set; } = 10;
    
    /// <summary>
    /// Whether to enable workflow persistence
    /// </summary>
    public bool EnablePersistence { get; set; } = true;
    
    /// <summary>
    /// Whether to enable workflow compensation
    /// </summary>
    public bool EnableCompensation { get; set; } = true;
    
    /// <summary>
    /// Interval for workflow status checks
    /// </summary>
    public TimeSpan StatusCheckInterval { get; set; } = TimeSpan.FromSeconds(30);
    
    /// <summary>
    /// Timeout for workflow persistence operations
    /// </summary>
    public TimeSpan PersistenceTimeout { get; set; } = TimeSpan.FromSeconds(10);
}

/// <summary>
/// Configuration options for health checks
/// </summary>
public class HealthCheckOptions
{
    public const string SectionName = "HealthCheck";
    
    /// <summary>
    /// Interval between health checks
    /// </summary>
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromMinutes(1);
    
    /// <summary>
    /// Timeout for individual health checks
    /// </summary>
    public TimeSpan CheckTimeout { get; set; } = TimeSpan.FromSeconds(10);
    
    /// <summary>
    /// Number of consecutive failures before marking as unhealthy
    /// </summary>
    public int FailureThreshold { get; set; } = 3;
    
    /// <summary>
    /// Whether to enable detailed health check logging
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;
}

// ============================================================================
// APPSETTINGS.JSON CONFIGURATION
// ============================================================================

/*
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning",
      "Instrument.Data": "Information"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=SchedulerData;Trusted_Connection=true;MultipleActiveResultSets=true"
  },
  "Timeouts": {
    "DatabaseTimeout": "00:00:20",
    "ServiceTimeout": "00:00:25",
    "RequestTimeout": "00:00:30",
    "GrpcTimeout": "00:00:15",
    "BackgroundOperationTimeout": "00:05:00",
    "ValidationTimeout": "00:00:05",
    "OrchestrationTimeout": "00:10:00",
    "OrchestrationStepTimeout": "00:02:00",
    "HealthCheckTimeout": "00:00:10"
  },
  "RetryPolicy": {
    "MaxAttempts": 3,
    "BaseDelayMs": 1000,
    "BackoffMultiplier": 2.0,
    "MaxDelayMs": 30000,
    "UseJitter": true,
    "JitterPercentage": 0.1
  },
  "GrpcGateway": {
    "DefaultTimeoutSeconds": 30,
    "MaxConcurrentRequests": 100,
    "RetryOptions": {
      "MaxAttempts": 3,
      "BaseDelayMs": 1000,
      "BackoffMultiplier": 2.0,
      "UseJitter": true
    }
  },
  "Orchestration": {
    "DefaultWorkflowTimeout": "00:30:00",
    "MaxConcurrentWorkflows": 10,
    "EnablePersistence": true,
    "EnableCompensation": true,
    "StatusCheckInterval": "00:00:30",
    "PersistenceTimeout": "00:00:10"
  },
  "HealthCheck": {
    "CheckInterval": "00:01:00",
    "CheckTimeout": "00:00:10",
    "FailureThreshold": 3,
    "EnableDetailedLogging": false
  }
}
*/

// ============================================================================
// DEPENDENCY INJECTION EXTENSIONS
// ============================================================================

namespace Instrument.Data.Extensions;

/// <summary>
/// Extension methods for configuring Scheduler Data services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add all Scheduler Data services with enhanced configuration
    /// </summary>
    public static IServiceCollection AddSchedulerDataServices(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // Configure options
        services.Configure<TimeoutOptions>(configuration.GetSection(TimeoutOptions.SectionName));
        services.Configure<RetryPolicyOptions>(configuration.GetSection(RetryPolicyOptions.SectionName));
        services.Configure<OrchestrationOptions>(configuration.GetSection(OrchestrationOptions.SectionName));
        services.Configure<HealthCheckOptions>(configuration.GetSection(HealthCheckOptions.SectionName));
        services.Configure<GrpcGatewayOptions>(configuration.GetSection("GrpcGateway"));

        // Add repositories
        services.AddScoped<IParameterRepository, ParameterRepository>();
        services.AddScoped<ISequenceRepository, SequenceRepository>();
        services.AddScoped<IResourceRepository, ResourceRepository>();
        services.AddScoped<IRangeRepository, RangeRepository>();
        services.AddScoped<IRangeValueRepository, RangeValueRepository>();
        services.AddScoped<ISequenceGroupRepository, SequenceGroupRepository>();
        services.AddScoped<ISequenceGroupCollectionRepository, SequenceGroupCollectionRepository>();

        // Add services
        services.AddScoped<IParameterService, ParameterService>();
        services.AddScoped<ISequenceService, SequenceService>();
        services.AddScoped<IResourceService, ResourceService>();
        services.AddScoped<IRangeService, RangeService>();
        services.AddScoped<IRangeValueService, RangeValueService>();
        services.AddScoped<ISequenceGroupService, SequenceGroupService>();
        services.AddScoped<ISequenceGroupCollectionService, SequenceGroupCollectionService>();

        // Add gRPC services
        services.AddGrpcGatewayServices();
        
        // Add orchestration services
        services.AddOrchestrationServices();

        // Add health check services
        services.AddHealthCheckServices();

        return services;
    }

    /// <summary>
    /// Add gRPC Gateway services
    /// </summary>
    public static IServiceCollection AddGrpcGatewayServices(this IServiceCollection services)
    {
        services.AddScoped<IGrpcGateway, GrpcGateway>();
        services.AddScoped<IRetryPolicy, ExponentialBackoffRetryPolicy>();
        services.AddScoped<IExecutionConfigurationOperationFactory, ExecutionConfigurationOperationFactory>();
        
        return services;
    }

    /// <summary>
    /// Add orchestration services
    /// </summary>
    public static IServiceCollection AddOrchestrationServices(this IServiceCollection services)
    {
        // Register process managers
        services.AddScoped<IProcessManager<ConfigurationImportRequest, ConfigurationImportResult>, ConfigurationImportManager>();
        
        // Register orchestration steps
        services.AddScoped<IOrchestrationStep, ValidateRequestStep>();
        services.AddScoped<IOrchestrationStep, ClearExistingDataStep>();
        services.AddScoped<IOrchestrationStep, InitializeDatabaseStep>();
        services.AddScoped<IOrchestrationStep, GetConfigurationStep>();
        services.AddScoped<IOrchestrationStep, ImportSequencesStep>();
        services.AddScoped<IOrchestrationStep, ImportResourcesStep>();
        
        return services;
    }

    /// <summary>
    /// Add health check services
    /// </summary>
    public static IServiceCollection AddHealthCheckServices(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddDbContextCheck<SchedulerDbContext>("database")
            .AddCheck<GrpcServiceHealthCheck>("grpc-services")
            .AddCheck<OrchestrationHealthCheck>("orchestration");
            
        return services;
    }

    /// <summary>
    /// Add Entity Framework context with timeout configuration
    /// </summary>
    public static IServiceCollection AddSchedulerDbContext(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        var timeoutOptions = configuration.GetSection(TimeoutOptions.SectionName).Get<TimeoutOptions>() ?? new TimeoutOptions();

        services.AddDbContext<SchedulerDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.CommandTimeout((int)timeoutOptions.DatabaseTimeout.TotalSeconds);
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorNumbersToAdd: null);
            });
            
            // Configure logging for EF Core
            options.LogTo(Console.WriteLine, LogLevel.Information);
            options.EnableSensitiveDataLogging(false);
            options.EnableDetailedErrors(true);
        });

        return services;
    }
}

// ============================================================================
// MIDDLEWARE EXTENSIONS
// ============================================================================

namespace Instrument.Data.Middleware;

/// <summary>
/// Middleware for handling request timeouts and cancellation tokens
/// </summary>
public class RequestTimeoutMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestTimeoutMiddleware> _logger;
    private readonly TimeoutOptions _timeoutOptions;

    public RequestTimeoutMiddleware(
        RequestDelegate next,
        ILogger<RequestTimeoutMiddleware> logger,
        IOptions<TimeoutOptions> timeoutOptions)
    {
        _next = next;
        _logger = logger;
        _timeoutOptions = timeoutOptions.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Check if request has custom timeout header
        var requestTimeout = GetRequestTimeout(context);
        var effectiveTimeout = requestTimeout ?? _timeoutOptions.RequestTimeout;

        _logger.LogDebug("Processing request with timeout: {Timeout}ms", effectiveTimeout.TotalMilliseconds);

        using var timeoutCts = new CancellationTokenSource(effectiveTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            context.RequestAborted, 
            timeoutCts.Token);

        // Store the composite cancellation token for downstream services
        context.Items["CancellationToken"] = linkedCts.Token;
        context.Items["RequestTimeout"] = effectiveTimeout;

        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
        {
            _logger.LogWarning("Request timed out after {Timeout}ms", effectiveTimeout.TotalMilliseconds);
            
            context.Response.StatusCode = 408; // Request Timeout
            await context.Response.WriteAsync("Request timed out");
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogInformation("Request was cancelled by client");
            // Client disconnected - no need to write response
        }
    }

    private TimeSpan? GetRequestTimeout(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("Request-Timeout", out var timeoutHeader) &&
            int.TryParse(timeoutHeader, out var timeoutSeconds))
        {
            return TimeSpan.FromSeconds(Math.Min(timeoutSeconds, 300)); // Max 5 minutes
        }

        return null;
    }
}

/// <summary>
/// Middleware for handling correlation IDs
/// </summary>
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;
    private const string CorrelationIdHeader = "X-Correlation-ID";

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrCreateCorrelationId(context);
        
        // Add to response headers
        context.Response.Headers.Add(CorrelationIdHeader, correlationId);
        
        // Store for downstream services
        context.Items["CorrelationId"] = correlationId;
        
        // Add to logging scope
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        });

        await _next(context);
    }

    private string GetOrCreateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationIdHeader, out var correlationId) &&
            !string.IsNullOrEmpty(correlationId))
        {
            return correlationId!;
        }

        return Guid.NewGuid().ToString();
    }
}

/// <summary>
/// Extension methods for middleware registration
/// </summary>
public static class MiddlewareExtensions
{
    /// <summary>
    /// Add request timeout and correlation ID middleware
    /// </summary>
    public static IApplicationBuilder UseSchedulerDataMiddleware(this IApplicationBuilder app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<RequestTimeoutMiddleware>();
        
        return app;
    }
}

// ============================================================================
// STARTUP CONFIGURATION
// ============================================================================

namespace Instrument.Data.Startup;

/// <summary>
/// Startup configuration for Scheduler Data services
/// </summary>
public static class StartupConfiguration
{
    /// <summary>
    /// Configure services in Program.cs or Startup.cs
    /// </summary>
    public static void ConfigureSchedulerDataServices(
        IServiceCollection services, 
        IConfiguration configuration)
    {
        // Add Entity Framework context
        services.AddSchedulerDbContext(configuration);
        
        // Add Scheduler Data services
        services.AddSchedulerDataServices(configuration);
        
        // Add HTTP client for external services
        services.AddHttpClient();
        
        // Add memory cache
        services.AddMemoryCache();
        
        // Add distributed cache (Redis recommended for production)
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = configuration.GetConnectionString("Redis");
        });
        
        // Add background services
        services.AddHostedService<HealthCheckBackgroundService>();
        services.AddHostedService<OrchestrationCleanupService>();
        
        // Add logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.AddDebug();
            builder.AddEventSourceLogger();
        });
    }

    /// <summary>
    /// Configure application pipeline
    /// </summary>
    public static void ConfigureSchedulerDataPipeline(
        IApplicationBuilder app, 
        IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();
        
        // Add custom middleware
        app.UseSchedulerDataMiddleware();
        
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        
        // Add health checks
        app.UseHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = WriteHealthCheckResponse
        });
        
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapHealthChecks("/health/ready");
            endpoints.MapHealthChecks("/health/live");
        });
    }

    private static async Task WriteHealthCheckResponse(HttpContext context, HealthReport report)
    {
        var response = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                duration = entry.Value.Duration.TotalMilliseconds,
                description = entry.Value.Description
            }),
            totalDuration = report.TotalDuration.TotalMilliseconds
        };

        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}
