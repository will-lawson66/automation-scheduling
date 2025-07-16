using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Text.Json;

// Core plugin interfaces
public interface IConfigurationPlugin
{
    string Name { get; }
    string Version { get; }
    int Priority { get; }
    string[] SupportedExtensions { get; }
    Task<bool> CanHandleAsync(IConfigurationSource source);
    Task<IConfigurationProvider> CreateProviderAsync(IConfigurationSource source, IConfigurationContext context);
    void Configure(IConfigurationPluginOptions options);
}

public interface IConfigurationSource
{
    string SourceType { get; }
    string Location { get; }
    Dictionary<string, object> Properties { get; }
    bool IsOptional { get; set; }
    bool ReloadOnChange { get; set; }
}

public interface IConfigurationContext
{
    IServiceProvider ServiceProvider { get; }
    ILogger Logger { get; }
    string Environment { get; }
    Dictionary<string, object> SharedData { get; }
}

public interface IConfigurationPluginOptions
{
    Dictionary<string, object> Settings { get; }
    void Set<T>(string key, T value);
    T Get<T>(string key, T defaultValue = default);
}

// Plugin registry and manager
public class ConfigurationPluginRegistry
{
    private readonly List<IConfigurationPlugin> _plugins = new();
    private readonly Dictionary<string, Type> _pluginTypes = new();
    private readonly ILogger<ConfigurationPluginRegistry> _logger;

    public ConfigurationPluginRegistry(ILogger<ConfigurationPluginRegistry> logger)
    {
        _logger = logger;
    }

    public void RegisterPlugin<T>() where T : class, IConfigurationPlugin, new()
    {
        RegisterPlugin(typeof(T));
    }

    public void RegisterPlugin(Type pluginType)
    {
        if (!typeof(IConfigurationPlugin).IsAssignableFrom(pluginType))
        {
            throw new ArgumentException($"Type {pluginType.Name} must implement IConfigurationPlugin");
        }

        var plugin = (IConfigurationPlugin)Activator.CreateInstance(pluginType);
        _plugins.Add(plugin);
        _pluginTypes[plugin.Name] = pluginType;

        _logger.LogInformation("Registered configuration plugin: {PluginName} v{Version}", 
            plugin.Name, plugin.Version);
    }

    public void AutoDiscoverPlugins(string pluginDirectory = null)
    {
        var searchPaths = new List<string>();
        
        if (!string.IsNullOrEmpty(pluginDirectory) && Directory.Exists(pluginDirectory))
        {
            searchPaths.Add(pluginDirectory);
        }
        
        // Add current directory and common plugin locations
        searchPaths.AddRange(new[]
        {
            AppContext.BaseDirectory,
            Path.Combine(AppContext.BaseDirectory, "plugins"),
            Path.Combine(AppContext.BaseDirectory, "config", "plugins")
        });

        foreach (var path in searchPaths.Where(Directory.Exists))
        {
            DiscoverPluginsInDirectory(path);
        }
    }

    private void DiscoverPluginsInDirectory(string directory)
    {
        var assemblies = Directory.GetFiles(directory, "*.dll")
            .Where(f => Path.GetFileName(f).Contains("Config") || Path.GetFileName(f).Contains("Plugin"));

        foreach (var assemblyPath in assemblies)
        {
            try
            {
                var assembly = Assembly.LoadFrom(assemblyPath);
                var pluginTypes = assembly.GetTypes()
                    .Where(t => typeof(IConfigurationPlugin).IsAssignableFrom(t) && 
                               !t.IsInterface && !t.IsAbstract);

                foreach (var type in pluginTypes)
                {
                    RegisterPlugin(type);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load plugin assembly: {AssemblyPath}", assemblyPath);
            }
        }
    }

    public async Task<IConfigurationPlugin> GetPluginAsync(IConfigurationSource source)
    {
        var compatiblePlugins = new List<(IConfigurationPlugin Plugin, bool CanHandle)>();

        foreach (var plugin in _plugins.OrderByDescending(p => p.Priority))
        {
            try
            {
                var canHandle = await plugin.CanHandleAsync(source);
                compatiblePlugins.Add((plugin, canHandle));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Plugin {PluginName} failed to check compatibility with source {SourceType}", 
                    plugin.Name, source.SourceType);
            }
        }

        return compatiblePlugins.FirstOrDefault(p => p.CanHandle).Plugin;
    }

    public IEnumerable<IConfigurationPlugin> GetPlugins() => _plugins.AsReadOnly();
}

// Plugin-based configuration manager
public class PluginBasedConfigurationManager
{
    private readonly ConfigurationPluginRegistry _registry;
    private readonly IConfigurationContext _context;
    private readonly ILogger<PluginBasedConfigurationManager> _logger;

    public PluginBasedConfigurationManager(
        ConfigurationPluginRegistry registry,
        IConfigurationContext context,
        ILogger<PluginBasedConfigurationManager> logger)
    {
        _registry = registry;
        _context = context;
        _logger = logger;
    }

    public async Task<IConfiguration> LoadConfigurationAsync(params IConfigurationSource[] sources)
    {
        var builder = new ConfigurationBuilder();
        var loadedProviders = new List<(IConfigurationSource Source, IConfigurationProvider Provider)>();

        foreach (var source in sources)
        {
            try
            {
                var plugin = await _registry.GetPluginAsync(source);
                if (plugin == null)
                {
                    if (!source.IsOptional)
                    {
                        throw new ConfigurationException(
                            $"No compatible plugin found for configuration source: {source.SourceType} at {source.Location}");
                    }
                    
                    _logger.LogWarning("No plugin found for optional source: {SourceType} at {Location}", 
                        source.SourceType, source.Location);
                    continue;
                }

                var provider = await plugin.CreateProviderAsync(source, _context);
                builder.Add(provider);
                loadedProviders.Add((source, provider));

                _logger.LogInformation("Loaded configuration from {SourceType} at {Location} using plugin {PluginName}", 
                    source.SourceType, source.Location, plugin.Name);
            }
            catch (Exception ex)
            {
                if (!source.IsOptional)
                {
                    throw new ConfigurationException(
                        $"Failed to load required configuration from {source.SourceType} at {source.Location}", ex);
                }

                _logger.LogWarning(ex, "Failed to load optional configuration from {SourceType} at {Location}", 
                    source.SourceType, source.Location);
            }
        }

        var configuration = builder.Build();
        
        // Setup change monitoring for providers that support it
        SetupChangeMonitoring(loadedProviders);

        return configuration;
    }

    private void SetupChangeMonitoring(List<(IConfigurationSource Source, IConfigurationProvider Provider)> providers)
    {
        foreach (var (source, provider) in providers.Where(p => p.Source.ReloadOnChange))
        {
            if (provider is IDisposable)
            {
                // Provider handles its own change monitoring
                continue;
            }

            // Setup file system watcher for file-based sources
            if (source.SourceType == "file" && File.Exists(source.Location))
            {
                SetupFileWatcher(source.Location, provider);
            }
        }
    }

    private void SetupFileWatcher(string filePath, IConfigurationProvider provider)
    {
        var directory = Path.GetDirectoryName(filePath);
        var fileName = Path.GetFileName(filePath);
        
        var watcher = new FileSystemWatcher(directory, fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };

        watcher.Changed += (sender, e) =>
        {
            try
            {
                // Debounce multiple rapid file changes
                Task.Delay(500).ContinueWith(_ =>
                {
                    provider.Load();
                    _logger.LogInformation("Reloaded configuration from {FilePath}", filePath);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reload configuration from {FilePath}", filePath);
            }
        };
    }
}

// Concrete plugin implementations

// JSON configuration plugin
public class JsonConfigurationPlugin : IConfigurationPlugin
{
    public string Name => "JsonConfiguration";
    public string Version => "1.0.0";
    public int Priority => 100;
    public string[] SupportedExtensions => new[] { ".json" };

    public Task<bool> CanHandleAsync(IConfigurationSource source)
    {
        if (source.SourceType != "file") return Task.FromResult(false);
        
        var extension = Path.GetExtension(source.Location);
        return Task.FromResult(SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase));
    }

    public Task<IConfigurationProvider> CreateProviderAsync(IConfigurationSource source, IConfigurationContext context)
    {
        var provider = new JsonConfigurationProvider(new JsonConfigurationSource
        {
            Path = source.Location,
            Optional = source.IsOptional,
            ReloadOnChange = source.ReloadOnChange
        });

        return Task.FromResult<IConfigurationProvider>(provider);
    }

    public void Configure(IConfigurationPluginOptions options)
    {
        // Plugin-specific configuration if needed
    }
}

// Database configuration plugin
public class DatabaseConfigurationPlugin : IConfigurationPlugin
{
    public string Name => "DatabaseConfiguration";
    public string Version => "1.0.0";
    public int Priority => 80;
    public string[] SupportedExtensions => Array.Empty<string>();

    public Task<bool> CanHandleAsync(IConfigurationSource source)
    {
        return Task.FromResult(source.SourceType == "database");
    }

    public async Task<IConfigurationProvider> CreateProviderAsync(IConfigurationSource source, IConfigurationContext context)
    {
        var connectionString = source.Properties.GetValueOrDefault("connectionString")?.ToString();
        var tableName = source.Properties.GetValueOrDefault("tableName")?.ToString() ?? "Configuration";
        
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new ArgumentException("Database configuration source requires connectionString property");
        }

        return new DatabaseConfigurationProvider(connectionString, tableName);
    }

    public void Configure(IConfigurationPluginOptions options)
    {
        // Configure database-specific options
    }
}

// REST API configuration plugin
public class RestApiConfigurationPlugin : IConfigurationPlugin
{
    private readonly HttpClient _httpClient;

    public RestApiConfigurationPlugin()
    {
        _httpClient = new HttpClient();
    }

    public string Name => "RestApiConfiguration";
    public string Version => "1.0.0";
    public int Priority => 60;
    public string[] SupportedExtensions => Array.Empty<string>();

    public Task<bool> CanHandleAsync(IConfigurationSource source)
    {
        return Task.FromResult(source.SourceType == "restapi" && 
                              Uri.TryCreate(source.Location, UriKind.Absolute, out _));
    }

    public async Task<IConfigurationProvider> CreateProviderAsync(IConfigurationSource source, IConfigurationContext context)
    {
        var apiKey = source.Properties.GetValueOrDefault("apiKey")?.ToString();
        var refreshInterval = TimeSpan.FromMinutes(
            Convert.ToDouble(source.Properties.GetValueOrDefault("refreshInterval", 5.0)));

        return new RestApiConfigurationProvider(_httpClient, source.Location, apiKey, refreshInterval);
    }

    public void Configure(IConfigurationPluginOptions options)
    {
        var timeout = options.Get("timeout", TimeSpan.FromSeconds(30));
        _httpClient.Timeout = timeout;
    }
}

// Supporting classes and configurations
public class ConfigurationException : Exception
{
    public ConfigurationException(string message) : base(message) { }
    public ConfigurationException(string message, Exception innerException) : base(message, innerException) { }
}

public class FileConfigurationSource : IConfigurationSource
{
    public string SourceType => "file";
    public string Location { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new();
    public bool IsOptional { get; set; } = true;
    public bool ReloadOnChange { get; set; } = true;
}

public class DatabaseConfigurationSource : IConfigurationSource
{
    public string SourceType => "database";
    public string Location { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new();
    public bool IsOptional { get; set; } = false;
    public bool ReloadOnChange { get; set; } = false;
}

public class RestApiConfigurationSource : IConfigurationSource
{
    public string SourceType => "restapi";
    public string Location { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new();
    public bool IsOptional { get; set; } = true;
    public bool ReloadOnChange { get; set; } = true;
}