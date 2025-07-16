// Program.cs - Complete integration example
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class Program
{
    public static async Task Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        
        // Initialize the advanced configuration system
        await InitializeAdvancedConfigurationAsync(host.Services);
        
        await host.RunAsync();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                // Clear default sources to have full control
                config.Sources.Clear();

                // Add schema-validated configurations
                config.AddSchemaValidatedJson(
                    "./config/automation.config.json", 
                    "./schemas/automation.schema.json",
                    new SchemaValidationOptions
                    {
                        StrictValidation = context.HostingEnvironment.IsProduction(),
                        LogValidationErrors = true,
                        ApplyDefaults = true
                    });

                config.AddSchemaValidatedJson(
                    "./config/modules", 
                    "./schemas",
                    new SchemaValidationOptions { StrictValidation = false });

                // Add environment variables and command line
                config.AddEnvironmentVariables("AUTOMATION_");
                config.AddCommandLine(args);
            })
            .ConfigureServices((context, services) =>
            {
                // Register configuration plugin system
                services.AddSingleton<ConfigurationPluginRegistry>();
                services.AddSingleton<PluginBasedConfigurationManager>();
                
                // Register hot reload system
                services.AddHotReloadConfiguration(builder =>
                {
                    // Main automation configuration
                    builder.AddJsonConfiguration("automation", "./config/automation.config.json");
                    
                    // Module configurations
                    builder.AddJsonConfiguration("email", "./config/email.config.json");
                    builder.AddJsonConfiguration("fileprocessing", "./config/fileprocessing.config.json");
                    
                    // Register change handlers
                    builder.AddChangeHandler<EmailConfig>("Email", OnEmailConfigChanged);
                    builder.AddChangeHandler<FileProcessingConfig>("FileProcessing", OnFileProcessingConfigChanged);
                });

                // Register strongly-typed configuration objects
                services.Configure<AutomationConfig>(context.Configuration.GetSection("Automation"));
                services.Configure<EmailConfig>(context.Configuration.GetSection("Email"));
                services.Configure<FileProcessingConfig>(context.Configuration.GetSection("FileProcessing"));

                // Add validation
                services.AddOptions<AutomationConfig>().ValidateDataAnnotations().ValidateOnStart();
                services.AddOptions<EmailConfig>().ValidateDataAnnotations().ValidateOnStart();

                // Register your automation services
                services.AddScoped<IEmailAutomationService, EmailAutomationService>();
                services.AddScoped<IFileProcessingService, FileProcessingService>();
                services.AddHostedService<AutomationOrchestrator>();
            })
            .ConfigureLogging((context, logging) =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.AddDebug();
                
                if (context.HostingEnvironment.IsProduction())
                {
                    logging.AddEventLog();
                }
            });

    private static async Task InitializeAdvancedConfigurationAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        
        try
        {
            // Initialize plugin registry
            var pluginRegistry = scope.ServiceProvider.GetRequiredService<ConfigurationPluginRegistry>();
            
            // Register built-in plugins
            pluginRegistry.RegisterPlugin<JsonConfigurationPlugin>();
            pluginRegistry.RegisterPlugin<DatabaseConfigurationPlugin>();
            pluginRegistry.RegisterPlugin<RestApiConfigurationPlugin>();
            
            // Auto-discover additional plugins
            pluginRegistry.AutoDiscoverPlugins("./plugins");
            
            // Initialize plugin-based configuration manager
            var pluginManager = scope.ServiceProvider.GetRequiredService<PluginBasedConfigurationManager>();
            
            // Load additional configurations via plugins
            var additionalSources = new[]
            {
                new DatabaseConfigurationSource
                {
                    Location = "AutomationDB",
                    Properties = new Dictionary<string, object>
                    {
                        ["connectionString"] = "Server=localhost;Database=AutomationConfig;Trusted_Connection=true;",
                        ["tableName"] = "ModuleSettings"
                    },
                    IsOptional = true
                },
                new RestApiConfigurationSource
                {
                    Location = "https://config-api.company.com/automation",
                    Properties = new Dictionary<string, object>
                    {
                        ["apiKey"] = Environment.GetEnvironmentVariable("CONFIG_API_KEY"),
                        ["refreshInterval"] = 10.0 // minutes
                    },
                    IsOptional = true
                }
            };

            var externalConfig = await pluginManager.LoadConfigurationAsync(additionalSources);
            
            logger.LogInformation("Advanced configuration system initialized successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize advanced configuration system");
            throw;
        }
    }

    // Configuration change handlers
    private static void OnEmailConfigChanged(EmailConfig oldConfig, EmailConfig newConfig)
    {
        Console.WriteLine($"Email configuration changed:");
        
        if (oldConfig?.Smtp?.Server != newConfig?.Smtp?.Server)
        {
            Console.WriteLine($"  SMTP Server: {oldConfig?.Smtp?.Server} -> {newConfig?.Smtp?.Server}");
        }
        
        if (oldConfig?.Processing?.BatchSize != newConfig?.Processing?.BatchSize)
        {
            Console.WriteLine($"  Batch Size: {oldConfig?.Processing?.BatchSize} -> {newConfig?.Processing?.BatchSize}");
        }
    }

    private static void OnFileProcessingConfigChanged(FileProcessingConfig oldConfig, FileProcessingConfig newConfig)
    {
        Console.WriteLine($"File processing configuration changed:");
        
        var oldFolderCount = oldConfig?.WatchFolders?.Length ?? 0;
        var newFolderCount = newConfig?.WatchFolders?.Length ?? 0;
        
        if (oldFolderCount != newFolderCount)
        {
            Console.WriteLine($"  Watch Folders Count: {oldFolderCount} -> {newFolderCount}");
        }
    }
}

// Configuration classes
public class AutomationConfig
{
    public string SystemName { get; set; } = "AutomationSystem";
    public string Version { get; set; } = "1.0.0";
    public LoggingConfig Logging { get; set; } = new();
    public PerformanceConfig Performance { get; set; } = new();
    public SecurityConfig Security { get; set; } = new();
}

public class LoggingConfig
{
    public string Level { get; set; } = "Information";
    public string[] Providers { get; set; } = { "Console", "File" };
    public string LogPath { get; set; } = "./logs";
    public int MaxLogFiles { get; set; } = 10;
    public long MaxLogSizeBytes { get; set; } = 100_000_000; // 100MB
}

public class PerformanceConfig
{
    public int MaxConcurrentTasks { get; set; } = 10;
    public int TaskTimeoutSeconds { get; set; } = 300;
    public bool EnableMetrics { get; set; } = true;
    public string MetricsEndpoint { get; set; } = "http://localhost:9090/metrics";
}

public class SecurityConfig
{
    public string[] AllowedUsers { get; set; } = Array.Empty<string>();
    public string[] AllowedRoles { get; set; } = Array.Empty<string>();
    public bool RequireAuthentication { get; set; } = true;
    public int SessionTimeoutMinutes { get; set; } = 480; // 8 hours
}

// Sample automation service that responds to configuration changes
public class EmailAutomationService : IEmailAutomationService
{
    private readonly IOptionsMonitor<EmailConfig> _config;
    private readonly ILogger<EmailAutomationService> _logger;
    private EmailConfig _currentConfig;

    public EmailAutomationService(
        IOptionsMonitor<EmailConfig> config, 
        ILogger<EmailAutomationService> logger)
    {
        _config = config;
        _logger = logger;
        _currentConfig = _config.CurrentValue;
        
        // Subscribe to configuration changes
        _config.OnChange(OnConfigurationChanged);
    }

    private void OnConfigurationChanged(EmailConfig newConfig, string name)
    {
        _logger.LogInformation("Email service configuration changed, reconfiguring...");
        
        var oldConfig = _currentConfig;
        _currentConfig = newConfig;
        
        // Reconfigure SMTP client if server settings changed
        if (oldConfig.Smtp.Server != newConfig.Smtp.Server ||
            oldConfig.Smtp.Port != newConfig.Smtp.Port)
        {
            ReconfigureSmtpClient();
        }
        
        // Update processing settings
        if (oldConfig.Processing.BatchSize != newConfig.Processing.BatchSize)
        {
            UpdateProcessingBatchSize(newConfig.Processing.BatchSize);
        }
    }

    private void ReconfigureSmtpClient()
    {
        _logger.LogInformation("Reconfiguring SMTP client with server: {Server}:{Port}", 
            _currentConfig.Smtp.Server, _currentConfig.Smtp.Port);
        
        // Implementation: recreate SMTP client with new settings
    }

    private void UpdateProcessingBatchSize(int newBatchSize)
    {
        _logger.LogInformation("Updating email processing batch size to {BatchSize}", newBatchSize);
        
        // Implementation: update internal batch processing logic
    }

    public async Task ProcessEmailBatchAsync(IEnumerable<EmailMessage> messages)
    {
        var batches = messages.Chunk(_currentConfig.Processing.BatchSize);
        
        foreach (var batch in batches)
        {
            await ProcessSingleBatch(batch);
        }
    }

    private async Task ProcessSingleBatch(IEnumerable<EmailMessage> batch)
    {
        // Implementation
        await Task.Delay(100); // Simulate processing
    }
}

// Supporting interfaces and classes
public interface IEmailAutomationService
{
    Task ProcessEmailBatchAsync(IEnumerable<EmailMessage> messages);
}

public interface IFileProcessingService
{
    Task ProcessFilesAsync(string[] filePaths);
}

public class EmailMessage
{
    public string To { get; set; }
    public string Subject { get; set; }
    public string Body { get; set; }
    public string[] Attachments { get; set; } = Array.Empty<string>();
}

// Main automation orchestrator
public class AutomationOrchestrator : BackgroundService
{
    private readonly IEmailAutomationService _emailService;
    private readonly IFileProcessingService _fileService;
    private readonly IOptionsMonitor<AutomationConfig> _config;
    private readonly ILogger<AutomationOrchestrator> _logger;

    public AutomationOrchestrator(
        IEmailAutomationService emailService,
        IFileProcessingService fileService,
        IOptionsMonitor<AutomationConfig> config,
        ILogger<AutomationOrchestrator> logger)
    {
        _emailService = emailService;
        _fileService = fileService;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Automation orchestrator started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Your automation logic here
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                
                _logger.LogDebug("Orchestrator tick completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in automation orchestrator");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
        
        _logger.LogInformation("Automation orchestrator stopped");
    }
}