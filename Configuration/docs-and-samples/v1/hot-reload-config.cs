using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text.Json;

// Hot reload configuration manager
public interface IHotReloadConfigurationManager
{
    event Action<string, IConfigurationRoot> ConfigurationChanged;
    event Action<string, Exception> ConfigurationError;
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    IConfigurationRoot GetConfiguration(string name = "default");
    void AddConfigurationSource(string name, IConfigurationBuilder builder);
    Task ReloadConfigurationAsync(string name = "default");
    void RegisterChangeHandler<T>(string sectionName, Action<T, T> changeHandler) where T : class;
}

public class HotReloadConfigurationManager : IHotReloadConfigurationManager, IDisposable
{
    private readonly ILogger<HotReloadConfigurationManager> _logger;
    private readonly ConcurrentDictionary<string, IConfigurationRoot> _configurations = new();
    private readonly ConcurrentDictionary<string, IConfigurationBuilder> _builders = new();
    private readonly ConcurrentDictionary<string, List<FileSystemWatcher>> _watchers = new();
    private readonly ConcurrentDictionary<string, ConfigurationChangeHandlers> _changeHandlers = new();
    private readonly Timer _reloadTimer;
    private readonly SemaphoreSlim _reloadSemaphore = new(1, 1);

    public event Action<string, IConfigurationRoot> ConfigurationChanged;
    public event Action<string, Exception> ConfigurationError;

    public HotReloadConfigurationManager(ILogger<HotReloadConfigurationManager> logger)
    {
        _logger = logger;
        
        // Set up periodic reload check (fallback mechanism)
        _reloadTimer = new Timer(PeriodicReloadCheck, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting hot reload configuration manager");
        
        foreach (var (name, builder) in _builders)
        {
            await LoadAndMonitorConfigurationAsync(name, builder);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stopping hot reload configuration manager");
        
        foreach (var watchers in _watchers.Values)
        {
            foreach (var watcher in watchers)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
        }
        
        _watchers.Clear();
        _reloadTimer?.Dispose();
    }

    public void AddConfigurationSource(string name, IConfigurationBuilder builder)
    {
        _builders[name] = builder;
        _changeHandlers[name] = new ConfigurationChangeHandlers();
        
        _logger.LogDebug("Added configuration source: {Name}", name);
    }

    public IConfigurationRoot GetConfiguration(string name = "default")
    {
        return _configurations.GetValueOrDefault(name);
    }

    public void RegisterChangeHandler<T>(string sectionName, Action<T, T> changeHandler) where T : class
    {
        foreach (var handlers in _changeHandlers.Values)
        {
            handlers.Register(sectionName, changeHandler);
        }
    }

    public async Task ReloadConfigurationAsync(string name = "default")
    {
        await _reloadSemaphore.WaitAsync();
        try
        {
            if (_builders.TryGetValue(name, out var builder))
            {
                await LoadAndMonitorConfigurationAsync(name, builder);
            }
        }
        finally
        {
            _reloadSemaphore.Release();
        }
    }

    private async Task LoadAndMonitorConfigurationAsync(string name, IConfigurationBuilder builder)
    {
        try
        {
            var previousConfig = _configurations.GetValueOrDefault(name);
            var newConfig = builder.Build();
            
            _configurations[name] = newConfig;
            
            // Detect and handle changes
            if (previousConfig != null)
            {
                await HandleConfigurationChangesAsync(name, previousConfig, newConfig);
            }
            
            // Setup file system watchers
            SetupFileSystemWatchers(name, builder);
            
            ConfigurationChanged?.Invoke(name, newConfig);
            _logger.LogInformation("Configuration '{Name}' loaded successfully", name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load configuration '{Name}'", name);
            ConfigurationError?.Invoke(name, ex);
        }
    }

    private async Task HandleConfigurationChangesAsync(string name, IConfigurationRoot oldConfig, IConfigurationRoot newConfig)
    {
        if (!_changeHandlers.TryGetValue(name, out var handlers))
        {
            return;
        }

        var changeDetector = new ConfigurationChangeDetector();
        var changes = changeDetector.DetectChanges(oldConfig, newConfig);
        
        foreach (var change in changes)
        {
            try
            {
                await handlers.HandleChangeAsync(change, oldConfig, newConfig);
                _logger.LogDebug("Handled configuration change: {Section} - {ChangeType}", 
                    change.Section, change.ChangeType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to handle configuration change for section {Section}", change.Section);
            }
        }
    }

    private void SetupFileSystemWatchers(string name, IConfigurationBuilder builder)
    {
        // Dispose existing watchers for this configuration
        if (_watchers.TryGetValue(name, out var existingWatchers))
        {
            foreach (var watcher in existingWatchers)
            {
                watcher.Dispose();
            }
        }

        var watchers = new List<FileSystemWatcher>();
        var filePaths = ExtractFilePathsFromBuilder(builder);

        foreach (var filePath in filePaths.Where(File.Exists))
        {
            var watcher = CreateFileWatcher(filePath, name);
            watchers.Add(watcher);
        }

        _watchers[name] = watchers;
    }

    private FileSystemWatcher CreateFileWatcher(string filePath, string configName)
    {
        var directory = Path.GetDirectoryName(filePath);
        var fileName = Path.GetFileName(filePath);
        
        var watcher = new FileSystemWatcher(directory, fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
            EnableRaisingEvents = true
        };

        // Debounce rapid file changes
        var debouncer = new ChangeDebouncer(TimeSpan.FromMilliseconds(500));
        
        watcher.Changed += async (sender, e) =>
        {
            await debouncer.DebounceAsync(async () =>
            {
                _logger.LogDebug("Configuration file changed: {FilePath}", filePath);
                await ReloadConfigurationAsync(configName);
            });
        };

        watcher.Created += async (sender, e) =>
        {
            await debouncer.DebounceAsync(async () =>
            {
                _logger.LogDebug("Configuration file created: {FilePath}", filePath);
                await ReloadConfigurationAsync(configName);
            });
        };

        return watcher;
    }

    private static List<string> ExtractFilePathsFromBuilder(IConfigurationBuilder builder)
    {
        var filePaths = new List<string>();
        
        foreach (var source in builder.Sources)
        {
            switch (source)
            {
                case JsonConfigurationSource jsonSource when !string.IsNullOrEmpty(jsonSource.Path):
                    filePaths.Add(jsonSource.Path);
                    break;
                case FileConfigurationSource fileSource when !string.IsNullOrEmpty(fileSource.Path):
                    filePaths.Add(fileSource.Path);
                    break;
            }
        }
        
        return filePaths;
    }

    private async void PeriodicReloadCheck(object state)
    {
        foreach (var (name, _) in _builders)
        {
            try
            {
                // Check if any configuration files have been modified
                var lastCheck = DateTime.Now.AddMinutes(-1);
                var filePaths = ExtractFilePathsFromBuilder(_builders[name]);
                
                var hasChanges = filePaths.Any(path => 
                    File.Exists(path) && File.GetLastWriteTime(path) > lastCheck);
                
                if (hasChanges)
                {
                    _logger.LogDebug("Periodic check detected changes in configuration '{Name}'", name);
                    await ReloadConfigurationAsync(name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during periodic configuration check for '{Name}'", name);
            }
        }
    }

    public void Dispose()
    {
        StopAsync().Wait(5000);
        _reloadSemaphore?.Dispose();
    }
}

// Configuration change detection and handling
public class ConfigurationChangeDetector
{
    public List<ConfigurationChange> DetectChanges(IConfigurationRoot oldConfig, IConfigurationRoot newConfig)
    {
        var changes = new List<ConfigurationChange>();
        var allKeys = GetAllConfigurationKeys(oldConfig).Union(GetAllConfigurationKeys(newConfig)).Distinct();

        foreach (var key in allKeys)
        {
            var oldValue = oldConfig[key];
            var newValue = newConfig[key];

            if (oldValue != newValue)
            {
                var changeType = DetermineChangeType(oldValue, newValue);
                var section = GetSectionName(key);
                
                changes.Add(new ConfigurationChange
                {
                    Key = key,
                    Section = section,
                    OldValue = oldValue,
                    NewValue = newValue,
                    ChangeType = changeType,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        return changes;
    }

    private static ConfigurationChangeType DetermineChangeType(string oldValue, string newValue)
    {
        if (oldValue == null && newValue != null) return ConfigurationChangeType.Added;
        if (oldValue != null && newValue == null) return ConfigurationChangeType.Removed;
        return ConfigurationChangeType.Modified;
    }

    private static string GetSectionName(string key)
    {
        var parts = key.Split(':');
        return parts.Length > 1 ? parts[0] : "Root";
    }

    private static IEnumerable<string> GetAllConfigurationKeys(IConfigurationRoot config)
    {
        return GetAllKeys(config.AsEnumerable());
    }

    private static IEnumerable<string> GetAllKeys(IEnumerable<KeyValuePair<string, string>> configData)
    {
        return configData.Select(kvp => kvp.Key);
    }
}

public class ConfigurationChangeHandlers
{
    private readonly Dictionary<string, List<Func<ConfigurationChange, IConfigurationRoot, IConfigurationRoot, Task>>> _handlers = new();

    public void Register<T>(string sectionName, Action<T, T> changeHandler) where T : class
    {
        if (!_handlers.ContainsKey(sectionName))
        {
            _handlers[sectionName] = new List<Func<ConfigurationChange, IConfigurationRoot, IConfigurationRoot, Task>>();
        }

        _handlers[sectionName].Add(async (change, oldConfig, newConfig) =>
        {
            var oldSection = oldConfig.GetSection(sectionName).Get<T>();
            var newSection = newConfig.GetSection(sectionName).Get<T>();
            
            if (oldSection != null || newSection != null)
            {
                changeHandler(oldSection, newSection);
            }
        });
    }

    public async Task HandleChangeAsync(ConfigurationChange change, IConfigurationRoot oldConfig, IConfigurationRoot newConfig)
    {
        if (_handlers.TryGetValue(change.Section, out var sectionHandlers))
        {
            var tasks = sectionHandlers.Select(handler => handler(change, oldConfig, newConfig));
            await Task.WhenAll(tasks);
        }
    }
}

// Supporting classes
public class ConfigurationChange
{
    public string Key { get; set; }
    public string Section { get; set; }
    public string OldValue { get; set; }
    public string NewValue { get; set; }
    public ConfigurationChangeType ChangeType { get; set; }
    public DateTime Timestamp { get; set; }
}

public enum ConfigurationChangeType
{
    Added,
    Modified,
    Removed
}

public class ChangeDebouncer
{
    private readonly TimeSpan _delay;
    private Timer _timer;
    private readonly object _lock = new();

    public ChangeDebouncer(TimeSpan delay)
    {
        _delay = delay;
    }

    public async Task DebounceAsync(Func<Task> action)
    {
        lock (_lock)
        {
            _timer?.Dispose();
            _timer = new Timer(async _ => await action(), null, _delay, Timeout.InfiniteTimeSpan);
        }
    }
}

// Hot reload service registration
public static class HotReloadServiceExtensions
{
    public static IServiceCollection AddHotReloadConfiguration(this IServiceCollection services, 
        Action<HotReloadConfigurationBuilder> configure)
    {
        var builder = new HotReloadConfigurationBuilder(services);
        configure(builder);
        
        services.AddSingleton<IHotReloadConfigurationManager, HotReloadConfigurationManager>();
        services.AddHostedService<HotReloadConfigurationService>();
        
        return services;
    }
}

public class HotReloadConfigurationBuilder
{
    private readonly IServiceCollection _services;
    private readonly List<Action<IHotReloadConfigurationManager>> _configurationActions = new();

    public HotReloadConfigurationBuilder(IServiceCollection services)
    {
        _services = services;
    }

    public HotReloadConfigurationBuilder AddJsonConfiguration(string name, string path, bool optional = true)
    {
        _configurationActions.Add(manager =>
        {
            var builder = new ConfigurationBuilder()
                .AddJsonFile(path, optional, reloadOnChange: true);
            manager.AddConfigurationSource(name, builder);
        });
        
        return this;
    }

    public HotReloadConfigurationBuilder AddChangeHandler<T>(string sectionName, Action<T, T> handler) where T : class
    {
        _configurationActions.Add(manager =>
        {
            manager.RegisterChangeHandler(sectionName, handler);
        });
        
        return this;
    }

    internal void Configure(IHotReloadConfigurationManager manager)
    {
        foreach (var action in _configurationActions)
        {
            action(manager);
        }
    }
}

public class HotReloadConfigurationService : BackgroundService
{
    private readonly IHotReloadConfigurationManager _configManager;
    private readonly HotReloadConfigurationBuilder _builder;

    public HotReloadConfigurationService(
        IHotReloadConfigurationManager configManager, 
        IServiceProvider serviceProvider)
    {
        _configManager = configManager;
        
        // Get builder from DI if configured
        _builder = serviceProvider.GetService<HotReloadConfigurationBuilder>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _builder?.Configure(_configManager);
        await _configManager.StartAsync(stoppingToken);
        
        // Keep service running
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _configManager.StopAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}