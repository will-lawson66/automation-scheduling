// Program.cs - Main application entry point with complete DI setup
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;
using System.Reflection;
using Serilog;
using Prometheus;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace Instrument.Scheduler
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            // Configure Serilog early
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File("logs/scheduler-.log", rollingInterval: RollingInterval.Day)
                .CreateBootstrapLogger();

            try
            {
                Log.Information("Starting Scheduler Application");

                var builder = WebApplication.CreateBuilder(args);

                // Configure Serilog
                builder.Host.UseSerilog((context, services, configuration) => configuration
                    .ReadFrom.Configuration(context.Configuration)
                    .ReadFrom.Services(services)
                    .Enrich.FromLogContext()
                    .WriteTo.Console()
                    .WriteTo.File("logs/scheduler-.log", rollingInterval: RollingInterval.Day)
                    .WriteTo.ApplicationInsights(services.GetService<TelemetryConfiguration>(), TelemetryConverter.Traces));

                // Add services
                ConfigureServices(builder.Services, builder.Configuration);

                var app = builder.Build();

                // Configure pipeline
                await ConfigureApplication(app);

                // Run application
                await app.RunAsync();

                return 0;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application terminated unexpectedly");
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            // Configuration
            services.Configure<SchedulerConfiguration>(configuration.GetSection("Scheduler"));
            services.Configure<DatabaseConfiguration>(configuration.GetSection("Database"));
            services.Configure<GrpcConfiguration>(configuration.GetSection("Grpc"));
            services.Configure<PerformanceMonitoringOptions>(configuration.GetSection("Monitoring"));
            services.Configure<ErrorHandlingOptions>(configuration.GetSection("ErrorHandling"));
            services.Configure<CmrCommandConfiguration>(configuration.GetSection("Cmr"));
            services.Configure<FlrConfiguration>(configuration.GetSection("Flr"));
            services.Configure<InventoryConfiguration>(configuration.GetSection("Inventory"));

            // Database
            ConfigureDatabaseServices(services, configuration);

            // Core Services
            ConfigureCoreServices(services);

            // Domain Services
            ConfigureDomainServices(services);

            // Application Services
            ConfigureApplicationServices(services);

            // Infrastructure Services
            ConfigureInfrastructureServices(services, configuration);

            // API Services
            ConfigureApiServices(services, configuration);

            // Background Services
            ConfigureBackgroundServices(services);

            // Monitoring and Observability
            ConfigureMonitoringServices(services, configuration);

            // Authentication and Authorization
            ConfigureAuthenticationServices(services, configuration);

            // Health Checks
            ConfigureHealthChecks(services, configuration);

            // HTTP Clients
            ConfigureHttpClients(services, configuration);

            // Caching
            ConfigureCaching(services, configuration);
        }

        private static void ConfigureDatabaseServices(IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("Default");
            var databaseProvider = configuration.GetValue<string>("Database:Provider", "SqlServer");

            switch (databaseProvider.ToLowerInvariant())
            {
                case "sqlserver":
                    services.AddDbContext<SchedulingDbContext>(options =>
                        options.UseSqlServer(connectionString, sqlOptions =>
                        {
                            sqlOptions.EnableRetryOnFailure(
                                maxRetryCount: 5,
                                maxRetryDelay: TimeSpan.FromSeconds(30),
                                errorNumbersToAdd: null);
                            sqlOptions.CommandTimeout(120);
                        }));
                    break;

                case "postgresql":
                    services.AddDbContext<SchedulingDbContext>(options =>
                        options.UseNpgsql(connectionString, pgOptions =>
                        {
                            pgOptions.EnableRetryOnFailure(
                                maxRetryCount: 5,
                                maxRetryDelay: TimeSpan.FromSeconds(30));
                            pgOptions.CommandTimeout(120);
                        }));
                    break;

                case "sqlite":
                    services.AddDbContext<SchedulingDbContext>(options =>
                        options.UseSqlite(connectionString));
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported database provider: {databaseProvider}");
            }

            // Repository pattern
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IAssaySampleRepository, AssaySampleRepository>();
            services.AddScoped<ISequenceRepository, SequenceRepository>();
            services.AddScoped<IInventoryRepository, InventoryRepository>();
            services.AddScoped<IConfigurationRepository, ConfigurationRepository>();
            services.AddScoped<IFlrDataRepository, FlrDataRepository>();
        }

        private static void ConfigureCoreServices(IServiceCollection services)
        {
            // Domain Services
            services.AddSingleton<IAssayManager, AssayManager>();
            services.AddSingleton<ISequenceGroupManager, SequenceGroupManager>();
            services.AddSingleton<ISchedulerStateManager, SchedulerStateManager>();
            services.AddSingleton<IConfigurationManager, ConfigurationManager>();

            // Execution Planning
            services.AddScoped<IAssayExecutionPlanner, AssayExecutionPlanner>();
            services.AddScoped<ISequenceBuilder, SequenceBuilder>();
            services.AddScoped<IResourceAllocator, ResourceAllocator>();

            // State Management
            services.AddSingleton<IStateMachine, SchedulerStateMachine>();
            services.AddScoped<IStateTransitionValidator, StateTransitionValidator>();
        }

        private static void ConfigureDomainServices(IServiceCollection services)
        {
            // Inventory Management
            services.AddSingleton<IInventoryService, InventoryService>();
            services.AddScoped<IInventoryValidator, InventoryValidator>();
            services.AddScoped<IInventoryOptimizer, InventoryOptimizer>();

            // CMR Services
            services.AddScoped<ICmrService, CmrService>();
            services.AddScoped<ICmrFileParser, CmrFileParser>();
            services.AddScoped<ICmrValidator, CmrValidator>();
            services.AddScoped<ICmrCommandService, CmrCommandService>();

            // FLR Services
            services.AddSingleton<IFlrService, FlrService>();
            services.AddScoped<IFlrContextFactory, FlrContextFactory>();
            services.AddScoped<IFlrDataProcessor, FlrDataProcessor>();

            // Sequence Services
            services.AddScoped<ISequenceTranslator, SequenceTranslator>();
            services.AddScoped<ISequenceOptimizer, SequenceOptimizer>();
            services.AddScoped<ISequenceValidator, SequenceValidator>();
        }

        private static void ConfigureApplicationServices(IServiceCollection services)
        {
            // Application Services
            services.AddScoped<ISchedulerService, SchedulerService>();
            services.AddScoped<IExecutionCoordinator, ExecutionCoordinator>();
            services.AddScoped<IResourceManager, ResourceManager>();
            services.AddScoped<IWorkflowEngine, WorkflowEngine>();

            // Command and Query Handlers
            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

            // Validation
            services.AddFluentValidation(fv => fv.RegisterValidatorsFromAssemblyContaining<Program>());

            // AutoMapper
            services.AddAutoMapper(typeof(Program).Assembly);
        }

        private static void ConfigureInfrastructureServices(IServiceCollection services, IConfiguration configuration)
        {
            // gRPC Clients
            services.AddGrpcClient<IHardwareExecutionEngineClient>(options =>
            {
                options.Address = new Uri(configuration.GetValue<string>("Grpc:HardwareEngineEndpoint"));
            }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });

            services.AddScoped<IGrpcGateway, GrpcGateway>();

            // Message Bus
            services.AddSingleton<IMessageBus, InMemoryMessageBus>();
            services.AddScoped<IEventBus, EventBus>();
            services.AddScoped<ICommandBus, CommandBus>();
            services.AddScoped<IQueryBus, QueryBus>();

            // File System Services
            services.AddScoped<IFileSystemService, FileSystemService>();
            services.AddScoped<ICmrFileSource, CmrFileSource>();

            // Serialization
            services.AddSingleton<IJsonSerializer, SystemTextJsonSerializer>();
            services.AddSingleton<IXmlSerializer, XmlSerializer>();

            // Time Services
            services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
            services.AddSingleton<IStopwatchProvider, SystemStopwatchProvider>();
        }

        private static void ConfigureApiServices(IServiceCollection services, IConfiguration configuration)
        {
            // Controllers
            services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                    options.JsonSerializerOptions.WriteIndented = true;
                    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                });

            // API Versioning
            services.AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ApiVersionReader = ApiVersionReader.Combine(
                    new UrlSegmentApiVersionReader(),
                    new HeaderApiVersionReader("X-Version"),
                    new QueryStringApiVersionReader("version"));
            });

            services.AddVersionedApiExplorer(setup =>
            {
                setup.GroupNameFormat = "'v'VVV";
                setup.SubstituteApiVersionInUrl = true;
            });

            // Swagger/OpenAPI
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Scheduler API",
                    Version = "v1",
                    Description = "Instrument Scheduler Application API",
                    Contact = new OpenApiContact
                    {
                        Name = "Development Team",
                        Email = "dev@company.com"
                    }
                });

                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "JWT Authorization header using the Bearer scheme",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });

                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });

                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                if (File.Exists(xmlPath))
                {
                    options.IncludeXmlComments(xmlPath);
                }
            });

            // CORS
            services.AddCors(options =>
            {
                options.AddPolicy("DefaultPolicy", policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });
            });

            // Rate limiting
            services.AddRateLimiter(options =>
            {
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: context.User?.Identity?.Name ?? context.Request.Headers.Host.ToString(),
                        factory: partition => new FixedWindowRateLimiterOptions
                        {
                            AutoReplenishment = true,
                            PermitLimit = 100,
                            Window = TimeSpan.FromMinutes(1)
                        }));
            });
        }

        private static void ConfigureBackgroundServices(IServiceCollection services)
        {
            // Background Services
            services.AddHostedService<AssayManager>();
            services.AddHostedService<SequenceGroupManager>();
            services.AddHostedService<SchedulerStateManager>();
            services.AddHostedService<ConfigurationManager>();
            services.AddHostedService<InventoryService>();
            services.AddHostedService<PerformanceMonitoringService>();
            services.AddHostedService<ErrorHandlingService>();

            // Cleanup Services
            services.AddHostedService<DatabaseCleanupService>();
            services.AddHostedService<LogCleanupService>();
            services.AddHostedService<MetricsCleanupService>();

            // Scheduled Tasks
            services.AddScoped<IScheduledTask, InventoryExpirationCheckTask>();
            services.AddScoped<IScheduledTask, PerformanceReportTask>();
            services.AddScoped<IScheduledTask, ErrorStatisticsTask>();
            services.AddSingleton<IScheduledTaskRunner, ScheduledTaskRunner>();
        }

        private static void ConfigureMonitoringServices(IServiceCollection services, IConfiguration configuration)
        {
            // Performance Monitoring
            services.AddSingleton<IPerformanceMonitoringService, PerformanceMonitoringService>();
            services.AddSingleton<IMetricsCollector, MetricsCollector>();

            // Error Handling
            services.AddSingleton<IErrorHandlingService, ErrorHandlingService>();
            services.AddSingleton<IExceptionHandler, GlobalExceptionHandler>();

            // Application Insights
            services.AddApplicationInsightsTelemetry(options =>
            {
                options.InstrumentationKey = configuration["ApplicationInsights:InstrumentationKey"];
                options.EnableQuickPulseMetricStream = true;
                options.EnableAdaptiveSampling = true;
            });

            // Prometheus Metrics
            services.AddSingleton<IMetricServer>(_ => new MetricServer(port: 9090));

            // Custom Telemetry
            services.AddSingleton<ITelemetryInitializer, SchedulerTelemetryInitializer>();
            services.AddSingleton<ITelemetryProcessor, SchedulerTelemetryProcessor>();
        }

        private static void ConfigureAuthenticationServices(IServiceCollection services, IConfiguration configuration)
        {
            var jwtSettings = configuration.GetSection("Jwt");

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.Authority = jwtSettings["Authority"];
                options.Audience = jwtSettings["Audience"];
                options.RequireHttpsMetadata = !IsDevelopment();
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromMinutes(5)
                };
            });

            services.AddAuthorization(options =>
            {
                options.AddPolicy("AdminOnly", policy =>
                    policy.RequireClaim("role", "admin"));
                options.AddPolicy("OperatorOrAdmin", policy =>
                    policy.RequireClaim("role", "operator", "admin"));
                options.AddPolicy("ViewOnly", policy =>
                    policy.RequireClaim("scope", "read"));
            });
        }

        private static void ConfigureHealthChecks(IServiceCollection services, IConfiguration configuration)
        {
            services.AddHealthChecks()
                .AddCheck<SchedulerHealthCheck>("scheduler")
                .AddCheck<DatabaseHealthCheck>("database")
                .AddCheck<GrpcHealthCheck>("grpc-hardware")
                .AddCheck<InventoryHealthCheck>("inventory")
                .AddCheck<ConfigurationHealthCheck>("configuration")
                .AddApplicationInsightsPublisher();

            services.AddHealthChecksUI(options =>
            {
                options.SetEvaluationTimeInSeconds(30);
                options.MaximumHistoryEntriesPerEndpoint(60);
                options.SetApiMaxActiveRequests(1);
                options.AddHealthCheckEndpoint("Scheduler", "/health");
            })
            .AddInMemoryStorage();
        }

        private static void ConfigureHttpClients(IServiceCollection services, IConfiguration configuration)
        {
            // HTTP Client for external services
            services.AddHttpClient("DefaultClient", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.Add("User-Agent", "Scheduler/1.0");
            })
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy());

            // Typed HTTP clients
            services.AddHttpClient<IExternalApiClient, ExternalApiClient>();
            services.AddHttpClient<INotificationClient, NotificationClient>();
        }

        private static void ConfigureCaching(IServiceCollection services, IConfiguration configuration)
        {
            var cacheProvider = configuration.GetValue<string>("Cache:Provider", "Memory");

            switch (cacheProvider.ToLowerInvariant())
            {
                case "memory":
                    services.AddMemoryCache();
                    services.AddSingleton<ICacheService, MemoryCacheService>();
                    break;

                case "redis":
                    services.AddStackExchangeRedisCache(options =>
                    {
                        options.Configuration = configuration.GetConnectionString("Redis");
                        options.InstanceName = "Scheduler";
                    });
                    services.AddSingleton<ICacheService, RedisCacheService>();
                    break;

                default:
                    services.AddMemoryCache();
                    services.AddSingleton<ICacheService, MemoryCacheService>();
                    break;
            }

            services.Decorate<ICacheService, CacheServiceDecorator>();
        }

        private static async Task ConfigureApplication(WebApplication app)
        {
            var logger = app.Services.GetRequiredService<ILogger<Program>>();

            // Exception handling
            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/error");
                app.UseHsts();
            }

            // Security headers
            app.UseSecurityHeaders();

            // Request logging
            app.UseSerilogRequestLogging();

            // HTTPS redirection
            app.UseHttpsRedirection();

            // CORS
            app.UseCors("DefaultPolicy");

            // Rate limiting
            app.UseRateLimiter();

            // Authentication & Authorization
            app.UseAuthentication();
            app.UseAuthorization();

            // Swagger (Development only)
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Scheduler API v1");
                    c.RoutePrefix = "swagger";
                });
            }

            // Health checks
            app.UseHealthChecks("/health", new HealthCheckOptions
            {
                ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
            });

            app.UseHealthChecks("/health/ready", new HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("ready"),
                ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
            });

            app.UseHealthChecks("/health/live", new HealthCheckOptions
            {
                Predicate = _ => false,
                ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
            });

            // Health checks UI
            app.UseHealthChecksUI(options =>
            {
                options.UIPath = "/health-ui";
                options.ApiPath = "/health-api";
            });

            // Metrics
            app.UseMetricServer();
            app.UseHttpMetrics();

            // API Controllers
            app.MapControllers();

            // Custom middleware
            app.UseMiddleware<RequestTrackingMiddleware>();
            app.UseMiddleware<PerformanceMiddleware>();
            app.UseMiddleware<ErrorHandlingMiddleware>();

            // SignalR hubs (for real-time updates)
            app.MapHub<SchedulerHub>("/schedulerHub");

            // Initialize services
            await InitializeServices(app.Services);

            logger.LogInformation("Application configured successfully");
        }

        private static async Task InitializeServices(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            try
            {
                // Initialize database
                var dbContext = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
                await dbContext.Database.MigrateAsync();
                logger.LogInformation("Database migration completed");

                // Initialize configuration
                var configManager = scope.ServiceProvider.GetRequiredService<IConfigurationManager>();
                await configManager.ReloadConfiguration();
                logger.LogInformation("Configuration loaded");

                // Initialize inventory
                var inventoryService = scope.ServiceProvider.GetRequiredService<IInventoryService>();
                await inventoryService.LoadInventoryFromRepository();
                logger.LogInformation("Inventory loaded");

                // Seed test data in development
                if (IsDevelopment())
                {
                    var seeder = scope.ServiceProvider.GetRequiredService<IDataSeeder>();
                    await seeder.SeedAsync();
                    logger.LogInformation("Test data seeded");
                }

                logger.LogInformation("Service initialization completed");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Service initialization failed");
                throw;
            }
        }

        private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        Console.WriteLine($"Retry {retryCount} after {timespan} seconds");
                    });
        }

        private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: 5,
                    durationOfBreak: TimeSpan.FromSeconds(30),
                    onBreak: (exception, timespan) =>
                    {
                        Console.WriteLine($"Circuit breaker opened for {timespan}");
                    },
                    onReset: () =>
                    {
                        Console.WriteLine("Circuit breaker reset");
                    });
        }

        private static bool IsDevelopment()
        {
            return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
        }
    }

    // Configuration Classes
    public class SchedulerConfiguration
    {
        public int MaxConcurrentSamples { get; set; } = 10;
        public TimeSpan ExecutionTimeout { get; set; } = TimeSpan.FromHours(8);
        public TimeSpan MonitoringInterval { get; set; } = TimeSpan.FromSeconds(30);
        public bool EnableDebugMode { get; set; } = false;
        public string InstanceName { get; set; } = "Scheduler";
    }

    public class DatabaseConfiguration
    {
        public string Provider { get; set; } = "SqlServer";
        public int CommandTimeout { get; set; } = 30;
        public int ConnectionRetryCount { get; set; } = 3;
        public bool EnableConnectionPooling { get; set; } = true;
        public bool EnableSensitiveDataLogging { get; set; } = false;
    }

    public class GrpcConfiguration
    {
        public string HardwareEngineEndpoint { get; set; } = "https://localhost:5001";
        public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);
        public int MaxRetryAttempts { get; set; } = 3;
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);
        public bool EnableTls { get; set; } = true;
    }

    public class InventoryConfiguration
    {
        public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromMinutes(15);
        public int LowStockThreshold { get; set; } = 10;
        public int ExpirationWarningDays { get; set; } = 7;
        public bool EnableAutoOrdering { get; set; } = false;
    }

    // Middleware Classes
    public class RequestTrackingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestTrackingMiddleware> _logger;

        public RequestTrackingMiddleware(RequestDelegate next, ILogger<RequestTrackingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var requestId = Guid.NewGuid().ToString();
            context.Items["RequestId"] = requestId;

            using (_logger.BeginScope(new Dictionary<string, object> { ["RequestId"] = requestId }))
            {
                _logger.LogInformation("Starting request {Method} {Path}", context.Request.Method, context.Request.Path);

                await _next(context);

                _logger.LogInformation("Completed request {Method} {Path} with status {StatusCode}",
                    context.Request.Method, context.Request.Path, context.Response.StatusCode);
            }
        }
    }

    public class PerformanceMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IPerformanceMonitoringService _performanceService;

        public PerformanceMiddleware(RequestDelegate next, IPerformanceMonitoringService performanceService)
        {
            _next = next;
            _performanceService = performanceService;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var stopwatch = Stopwatch.StartNew();

            await _next(context);

            stopwatch.Stop();

            // Record performance metrics
            _performanceService.RecordApiCall(
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                stopwatch.Elapsed);
        }
    }

    public class ErrorHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IErrorHandlingService _errorHandlingService;
        private readonly ILogger<ErrorHandlingMiddleware> _logger;

        public ErrorHandlingMiddleware(
            RequestDelegate next, 
            IErrorHandlingService errorHandlingService,
            ILogger<ErrorHandlingMiddleware> logger)
        {
            _next = next;
            _errorHandlingService = errorHandlingService;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var requestId = context.Items["RequestId"]?.ToString() ?? Guid.NewGuid().ToString();
            
            // Handle error through error handling service
            var result = await _errorHandlingService.HandleError(exception, 
                $"HTTP {context.Request.Method} {context.Request.Path}", 
                new { RequestId = requestId });

            // Determine appropriate HTTP status code
            var statusCode = exception switch
            {
                ArgumentException => 400,
                UnauthorizedAccessException => 401,
                NotImplementedException => 501,
                TimeoutException => 408,
                _ => 500
            };

            // Create error response
            var response = new
            {
                error = new
                {
                    id = result.ErrorId,
                    message = "An error occurred while processing your request",
                    type = exception.GetType().Name,
                    requestId = requestId,
                    timestamp = DateTime.UtcNow
                }
            };

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
        }
    }

    // Extension methods for service configuration
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSchedulerServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Add all scheduler-specific services
            return services;
        }

        public static IServiceCollection AddSecurityHeaders(this IServiceCollection services)
        {
            services.AddAntiforgery();
            return services;
        }
    }

    public static class ApplicationBuilderExtensions
    {
        public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
        {
            app.Use(async (context, next) =>
            {
                context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
                context.Response.Headers.Add("X-Frame-Options", "DENY");
                context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
                context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
                
                await next();
            });

            return app;
        }
    }

    // SignalR Hub for real-time updates
    public class SchedulerHub : Hub
    {
        private readonly ILogger<SchedulerHub> _logger;

        public SchedulerHub(ILogger<SchedulerHub> logger)
        {
            _logger = logger;
        }

        public async Task JoinGroup(string groupName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            _logger.LogInformation("Client {ConnectionId} joined group {GroupName}", Context.ConnectionId, groupName);
        }

        public async Task LeaveGroup(string groupName)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
            _logger.LogInformation("Client {ConnectionId} left group {GroupName}", Context.ConnectionId, groupName);
        }

        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation("Client {ConnectionId} connected", Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            _logger.LogInformation("Client {ConnectionId} disconnected", Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
    }
}