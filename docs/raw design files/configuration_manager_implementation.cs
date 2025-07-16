// ConfigurationManager.cs - Centralized configuration management
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Instrument.Scheduler.Configuration
{
    public class ConfigurationManager : IConfigurationManager, IHostedService, IDisposable
    {
        private readonly ConcurrentDictionary<string, ConfigurationItem> _configurations;
        private readonly IConfigurationRepository _repository;
        private readonly ConfigurationManagerOptions _options;
        private readonly ILogger<ConfigurationManager> _logger;
        private readonly Timer _refreshTimer;
        private readonly object _lockObject = new object();

        public event EventHandler<ConfigurationChangedEventArgs> OnConfigurationChanged;
        public event EventHandler<ConfigurationValidationEventArgs> OnConfigurationValidationFailed;

        public ConfigurationManager(
            IConfigurationRepository repository,
            IOptions<ConfigurationManagerOptions> options,
            ILogger<ConfigurationManager> logger)
        {
            _configurations = new ConcurrentDictionary<string, ConfigurationItem>();
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Set up refresh timer if auto-refresh is enabled
            if (_options.AutoRefreshEnabled)
            {
                _refreshTimer = new Timer(AutoRefreshConfigurations, null,
                    _options.RefreshInterval, _options.RefreshInterval);
            }
        }

        public T GetConfiguration<T>(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Configuration key cannot be null or empty", nameof(key));

            if (_configurations.TryGetValue(key, out var configItem))
            {
                try
                {
                    return ConvertValue<T>(configItem.Value);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to convert configuration value for key '{Key}' to type {Type}", 
                        key, typeof(T).Name);
                    
                    // Return default value if conversion fails
                    return GetDefaultValue<T>(key);
                }
            }

            _logger.LogWarning("Configuration key '{Key}' not found, returning default value", key);
            return GetDefaultValue<T>(key);
        }

        public async Task<bool> SetConfiguration<T>(string key, T value)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Configuration key cannot be null or empty", nameof(key));

            try
            {
                _logger.LogInformation("Setting configuration '{Key}' to value: {Value}", key, value);

                // Create or update configuration item
                var configItem = _configurations.GetOrAdd(key, k => new ConfigurationItem(k, value, DetermineConfigurationType<T>()));
                
                var oldValue = configItem.Value;
                configItem.UpdateValue(value);

                // Validate the new configuration
                var validationResult = ValidateConfiguration(key, value);
                if (!validationResult.IsValid)
                {
                    _logger.LogError("Configuration validation failed for key '{Key}': {Errors}", 
                        key, string.Join(", ", validationResult.Errors));
                    
                    // Revert to old value
                    configItem.UpdateValue(oldValue);
                    
                    OnConfigurationValidationFailed?.Invoke(this, 
                        new ConfigurationValidationEventArgs(key, value, validationResult));
                    
                    return false;
                }

                // Persist to repository
                await _repository.SaveConfiguration(configItem);

                // Notify listeners
                OnConfigurationChanged?.Invoke(this, 
                    new ConfigurationChangedEventArgs(key, oldValue, value, ChangeSource.Manual));

                _logger.LogInformation("Successfully set configuration '{Key}'", key);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set configuration '{Key}'", key);
                return false;
            }
        }

        public Dictionary<string, object> GetAllConfigurations()
        {
            return _configurations.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Value
            );
        }

        public Dictionary<string, ConfigurationItem> GetAllConfigurationItems()
        {
            return _configurations.ToDictionary(
                kvp => kvp.Key,
                kvp => new ConfigurationItem(kvp.Value.Key, kvp.Value.Value, kvp.Value.Type)
                {
                    Description = kvp.Value.Description,
                    IsRequired = kvp.Value.IsRequired,
                    LastModified = kvp.Value.LastModified,
                    ModifiedBy = kvp.Value.ModifiedBy,
                    ValidationRule = kvp.Value.ValidationRule
                }
            );
        }

        public ValidationResult ValidateConfiguration(string key, object value)
        {
            var result = new ValidationResult(true);

            if (string.IsNullOrWhiteSpace(key))
            {
                result.AddError("Configuration key cannot be null or empty");
                return result;
            }

            // Get configuration item for validation rules
            if (_configurations.TryGetValue(key, out var configItem))
            {
                // Apply custom validation rule if present
                if (configItem.ValidationRule != null)
                {
                    var ruleResult = configItem.ValidationRule.Validate(value);
                    if (!ruleResult.IsValid)
                    {
                        foreach (var error in ruleResult.Errors)
                        {
                            result.AddError(error);
                        }
                    }
                }
            }

            // Apply built-in validation rules
            ApplyBuiltInValidationRules(key, value, result);

            return result;
        }

        public async Task<bool> ReloadConfiguration()
        {
            try
            {
                _logger.LogInformation("Reloading configuration from repository");

                lock (_lockObject)
                {
                    var configurationItems = _repository.GetAllConfigurations().Result;
                    var reloadedCount = 0;

                    foreach (var item in configurationItems)
                    {
                        var oldValue = _configurations.TryGetValue(item.Key, out var existing) ? existing.Value : null;
                        
                        _configurations.AddOrUpdate(item.Key, item, (k, v) => item);
                        
                        // Notify if value changed
                        if (!Equals(oldValue, item.Value))
                        {
                            OnConfigurationChanged?.Invoke(this,
                                new ConfigurationChangedEventArgs(item.Key, oldValue, item.Value, ChangeSource.Reload));
                        }

                        reloadedCount++;
                    }

                    _logger.LogInformation("Successfully reloaded {Count} configurations", reloadedCount);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reload configuration");
                return false;
            }
        }

        public async Task<bool> ResetToDefaults()
        {
            try
            {
                _logger.LogInformation("Resetting configurations to default values");

                var defaultConfigurations = GetDefaultConfigurations();
                var resetCount = 0;

                foreach (var defaultConfig in defaultConfigurations)
                {
                    var success = await SetConfiguration(defaultConfig.Key, defaultConfig.Value);
                    if (success)
                    {
                        resetCount++;
                    }
                }

                _logger.LogInformation("Reset {Count} configurations to default values", resetCount);
                return resetCount == defaultConfigurations.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reset configurations to defaults");
                return false;
            }
        }

        public async Task<bool> ImportConfiguration(Dictionary<string, object> configurations)
        {
            if (configurations == null) throw new ArgumentNullException(nameof(configurations));

            try
            {
                _logger.LogInformation("Importing {Count} configurations", configurations.Count);

                var importedCount = 0;
                var errors = new List<string>();

                foreach (var kvp in configurations)
                {
                    try
                    {
                        var success = await SetConfiguration(kvp.Key, kvp.Value);
                        if (success)
                        {
                            importedCount++;
                        }
                        else
                        {
                            errors.Add($"Failed to set configuration '{kvp.Key}'");
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Error setting configuration '{kvp.Key}': {ex.Message}");
                    }
                }

                if (errors.Any())
                {
                    _logger.LogWarning("Import completed with errors: {Errors}", string.Join(", ", errors));
                }

                _logger.LogInformation("Successfully imported {ImportedCount} of {TotalCount} configurations", 
                    importedCount, configurations.Count);

                return importedCount == configurations.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import configurations");
                return false;
            }
        }

        public async Task<Dictionary<string, object>> ExportConfiguration()
        {
            try
            {
                _logger.LogInformation("Exporting configuration");

                var exportData = new Dictionary<string, object>();

                foreach (var kvp in _configurations)
                {
                    exportData[kvp.Key] = kvp.Value.Value;
                }

                _logger.LogInformation("Exported {Count} configurations", exportData.Count);
                return exportData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export configuration");
                return new Dictionary<string, object>();
            }
        }

        public ConfigurationValidationReport ValidateAllConfigurations()
        {
            var report = new ConfigurationValidationReport();

            foreach (var kvp in _configurations)
            {
                var validationResult = ValidateConfiguration(kvp.Key, kvp.Value.Value);
                
                if (!validationResult.IsValid)
                {
                    report.AddFailure(kvp.Key, validationResult.Errors);
                }
                else
                {
                    report.AddSuccess(kvp.Key);
                }

                if (validationResult.Warnings.Any())
                {
                    report.AddWarnings(kvp.Key, validationResult.Warnings);
                }
            }

            // Check for missing required configurations
            var requiredConfigurations = GetRequiredConfigurations();
            foreach (var required in requiredConfigurations)
            {
                if (!_configurations.ContainsKey(required))
                {
                    report.AddFailure(required, new[] { "Required configuration is missing" });
                }
            }

            return report;
        }

        private T ConvertValue<T>(object value)
        {
            if (value == null) return default(T);
            if (value is T directValue) return directValue;

            var targetType = typeof(T);
            var nullableType = Nullable.GetUnderlyingType(targetType);
            
            if (nullableType != null)
            {
                targetType = nullableType;
            }

            // Handle JSON strings for complex types
            if (value is string jsonString && (targetType.IsClass || targetType.IsArray))
            {
                try
                {
                    return JsonSerializer.Deserialize<T>(jsonString);
                }
                catch
                {
                    // Fall through to standard conversion
                }
            }

            return (T)Convert.ChangeType(value, targetType);
        }

        private T GetDefaultValue<T>(string key)
        {
            var defaultConfigurations = GetDefaultConfigurations();
            
            if (defaultConfigurations.TryGetValue(key, out var defaultValue))
            {
                try
                {
                    return ConvertValue<T>(defaultValue);
                }
                catch
                {
                    // Fall through to type default
                }
            }

            return default(T);
        }

        private ConfigurationType DetermineConfigurationType<T>()
        {
            var type = typeof(T);

            if (type == typeof(string)) return ConfigurationType.String;
            if (type == typeof(int) || type == typeof(long) || type == typeof(short)) return ConfigurationType.Integer;
            if (type == typeof(double) || type == typeof(float) || type == typeof(decimal)) return ConfigurationType.Decimal;
            if (type == typeof(bool)) return ConfigurationType.Boolean;
            if (type == typeof(TimeSpan)) return ConfigurationType.TimeSpan;
            if (type == typeof(DateTime)) return ConfigurationType.DateTime;
            if (type.IsArray || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))) return ConfigurationType.Array;
            
            return ConfigurationType.Object;
        }

        private void ApplyBuiltInValidationRules(string key, object value, ValidationResult result)
        {
            // Apply type-specific validation rules
            switch (key.ToLowerInvariant())
            {
                case var k when k.Contains("timeout"):
                    ValidateTimeoutValue(value, result);
                    break;
                case var k when k.Contains("port"):
                    ValidatePortValue(value, result);
                    break;
                case var k when k.Contains("path"):
                    ValidatePathValue(value, result);
                    break;
                case var k when k.Contains("url"):
                    ValidateUrlValue(value, result);
                    break;
                case var k when k.Contains("email"):
                    ValidateEmailValue(value, result);
                    break;
            }

            // Apply general validation rules
            if (value is string stringValue)
            {
                if (string.IsNullOrWhiteSpace(stringValue) && IsRequiredConfiguration(key))
                {
                    result.AddError($"Required configuration '{key}' cannot be empty");
                }
            }

            if (value is int intValue)
            {
                if (intValue < 0 && key.ToLowerInvariant().Contains("count"))
                {
                    result.AddError($"Count values cannot be negative: {key}");
                }
            }
        }

        private void ValidateTimeoutValue(object value, ValidationResult result)
        {
            if (value is TimeSpan timeout)
            {
                if (timeout.TotalMilliseconds < 0)
                {
                    result.AddError("Timeout values cannot be negative");
                }
                if (timeout.TotalHours > 24)
                {
                    result.AddWarning("Timeout value is very large (>24 hours)");
                }
            }
        }

        private void ValidatePortValue(object value, ValidationResult result)
        {
            if (value is int port)
            {
                if (port < 1 || port > 65535)
                {
                    result.AddError("Port number must be between 1 and 65535");
                }
            }
        }

        private void ValidatePathValue(object value, ValidationResult result)
        {
            if (value is string path)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    result.AddError("Path cannot be empty");
                    return;
                }

                try
                {
                    Path.GetFullPath(path);
                }
                catch
                {
                    result.AddError($"Invalid file path: {path}");
                }
            }
        }

        private void ValidateUrlValue(object value, ValidationResult result)
        {
            if (value is string url)
            {
                if (!Uri.TryCreate(url, UriKind.Absolute, out _))
                {
                    result.AddError($"Invalid URL format: {url}");
                }
            }
        }

        private void ValidateEmailValue(object value, ValidationResult result)
        {
            if (value is string email)
            {
                if (!IsValidEmail(email))
                {
                    result.AddError($"Invalid email format: {email}");
                }
            }
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        private bool IsRequiredConfiguration(string key)
        {
            var requiredKeys = GetRequiredConfigurations();
            return requiredKeys.Contains(key, StringComparer.OrdinalIgnoreCase);
        }

        private Dictionary<string, object> GetDefaultConfigurations()
        {
            return new Dictionary<string, object>
            {
                // Scheduler defaults
                ["Scheduler.MaxConcurrentSamples"] = 10,
                ["Scheduler.ExecutionTimeoutMinutes"] = 480, // 8 hours
                ["Scheduler.MonitoringIntervalSeconds"] = 30,
                
                // AssayManager defaults
                ["AssayManager.MaxRetryCount"] = 3,
                ["AssayManager.RetryDelaySeconds"] = 30,
                ["AssayManager.SampleCleanupDelayMinutes"] = 5,
                
                // SequenceGroupManager defaults
                ["SequenceGroupManager.MaxParallelSequences"] = 4,
                ["SequenceGroupManager.SequenceTimeoutMinutes"] = 60,
                
                // Inventory defaults
                ["Inventory.AutoRefreshIntervalMinutes"] = 15,
                ["Inventory.LowStockThreshold"] = 10,
                ["Inventory.ExpirationWarningDays"] = 7,
                
                // State management defaults
                ["StateManager.OutOfRangeTimeoutMinutes"] = 8,
                ["StateManager.EnableAutomaticRecovery"] = true,
                
                // FLR defaults
                ["Flr.DataRetentionDays"] = 365,
                ["Flr.MinimumQualityScore"] = 0.6,
                ["Flr.EnableRealTimeProcessing"] = true,
                
                // CMR defaults
                ["Cmr.MaxFileSizeMB"] = 100,
                ["Cmr.AllowOverwrite"] = false,
                ["Cmr.LibraryPath"] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Scheduler", "CMR"),
                
                // gRPC defaults
                ["Grpc.ConnectionTimeoutSeconds"] = 30,
                ["Grpc.MaxRetryAttempts"] = 3,
                ["Grpc.HeartbeatIntervalSeconds"] = 60
            };
        }

        private List<string> GetRequiredConfigurations()
        {
            return new List<string>
            {
                "Scheduler.MaxConcurrentSamples",
                "StateManager.OutOfRangeTimeoutMinutes",
                "Cmr.LibraryPath",
                "Grpc.ConnectionTimeoutSeconds"
            };
        }

        private void AutoRefreshConfigurations(object state)
        {
            Task.Run(async () =>
            {
                try
                {
                    await ReloadConfiguration();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during automatic configuration refresh");
                }
            });
        }

        // IHostedService implementation
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("ConfigurationManager service starting");
            
            // Load initial configuration
            await ReloadConfiguration();
            
            // Set default values for missing configurations
            var defaultConfigurations = GetDefaultConfigurations();
            foreach (var defaultConfig in defaultConfigurations)
            {
                if (!_configurations.ContainsKey(defaultConfig.Key))
                {
                    await SetConfiguration(defaultConfig.Key, defaultConfig.Value);
                }
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("ConfigurationManager service stopping");
            // Ensure all pending changes are saved
        }

        public void Dispose()
        {
            _refreshTimer?.Dispose();
        }
    }

    // Supporting classes and interfaces
    public interface IConfigurationManager
    {
        T GetConfiguration<T>(string key);
        Task<bool> SetConfiguration<T>(string key, T value);
        Dictionary<string, object> GetAllConfigurations();
        Dictionary<string, ConfigurationItem> GetAllConfigurationItems();
        ValidationResult ValidateConfiguration(string key, object value);
        Task<bool> ReloadConfiguration();
        Task<bool> ResetToDefaults();
        Task<bool> ImportConfiguration(Dictionary<string, object> configurations);
        Task<Dictionary<string, object>> ExportConfiguration();
        ConfigurationValidationReport ValidateAllConfigurations();
        
        event EventHandler<ConfigurationChangedEventArgs> OnConfigurationChanged;
        event EventHandler<ConfigurationValidationEventArgs> OnConfigurationValidationFailed;
    }

    public class ConfigurationItem
    {
        public string Key { get; private set; }
        public object Value { get; private set; }
        public ConfigurationType Type { get; private set; }
        public string Description { get; set; }
        public DateTime LastModified { get; private set; }
        public string ModifiedBy { get; set; }
        public bool IsRequired { get; set; }
        public ValidationRule ValidationRule { get; set; }

        public ConfigurationItem(string key, object value, ConfigurationType type)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            Value = value;
            Type = type;
            LastModified = DateTime.UtcNow;
            ModifiedBy = Environment.UserName;
        }

        public ValidationResult Validate()
        {
            var result = new ValidationResult(true);

            if (IsRequired && Value == null)
            {
                result.AddError($"Required configuration '{Key}' cannot be null");
                return result;
            }

            if (ValidationRule != null)
            {
                var ruleResult = ValidationRule.Validate(Value);
                if (!ruleResult.IsValid)
                {
                    foreach (var error in ruleResult.Errors)
                    {
                        result.AddError(error);
                    }
                }
            }

            return result;
        }

        public bool UpdateValue(object newValue)
        {
            if (Equals(Value, newValue))
                return false;

            Value = newValue;
            LastModified = DateTime.UtcNow;
            return true;
        }
    }

    public class ConfigurationManagerOptions
    {
        public bool AutoRefreshEnabled { get; set; } = true;
        public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromMinutes(5);
        public bool ValidateOnStartup { get; set; } = true;
        public bool CreateDefaultsIfMissing { get; set; } = true;
        public string ConfigurationSource { get; set; } = "Database";
    }

    public class ConfigurationValidationReport
    {
        public List<string> SuccessfulConfigurations { get; } = new();
        public Dictionary<string, List<string>> FailedConfigurations { get; } = new();
        public Dictionary<string, List<string>> ConfigurationWarnings { get; } = new();
        
        public bool IsValid => !FailedConfigurations.Any();
        public int TotalConfigurations => SuccessfulConfigurations.Count + FailedConfigurations.Count;

        public void AddSuccess(string key)
        {
            SuccessfulConfigurations.Add(key);
        }

        public void AddFailure(string key, IEnumerable<string> errors)
        {
            FailedConfigurations[key] = errors.ToList();
        }

        public void AddWarnings(string key, IEnumerable<string> warnings)
        {
            ConfigurationWarnings[key] = warnings.ToList();
        }
    }

    // Event argument classes
    public class ConfigurationChangedEventArgs : EventArgs
    {
        public string Key { get; }
        public object OldValue { get; }
        public object NewValue { get; }
        public ChangeSource Source { get; }
        public DateTime ChangedAt { get; }

        public ConfigurationChangedEventArgs(string key, object oldValue, object newValue, ChangeSource source)
        {
            Key = key;
            OldValue = oldValue;
            NewValue = newValue;
            Source = source;
            ChangedAt = DateTime.UtcNow;
        }
    }

    public class ConfigurationValidationEventArgs : EventArgs
    {
        public string Key { get; }
        public object Value { get; }
        public ValidationResult ValidationResult { get; }

        public ConfigurationValidationEventArgs(string key, object value, ValidationResult validationResult)
        {
            Key = key;
            Value = value;
            ValidationResult = validationResult;
        }
    }

    // Enums
    public enum ConfigurationType
    {
        String,
        Integer,
        Decimal,
        Boolean,
        TimeSpan,
        DateTime,
        Array,
        Object
    }

    public enum ChangeSource
    {
        Manual,
        Reload,
        Default,
        Import
    }

    // Abstract validation rule class
    public abstract class ValidationRule
    {
        public abstract ValidationResult Validate(object value);
    }

    // Concrete validation rule implementations
    public class RangeValidationRule : ValidationRule
    {
        public double MinValue { get; set; }
        public double MaxValue { get; set; }

        public RangeValidationRule(double minValue, double maxValue)
        {
            MinValue = minValue;
            MaxValue = maxValue;
        }

        public override ValidationResult Validate(object value)
        {
            var result = new ValidationResult(true);

            if (value != null && double.TryParse(value.ToString(), out var doubleValue))
            {
                if (doubleValue < MinValue || doubleValue > MaxValue)
                {
                    result.AddError($"Value must be between {MinValue} and {MaxValue}");
                }
            }

            return result;
        }
    }

    public class RegexValidationRule : ValidationRule
    {
        public string Pattern { get; set; }
        public string ErrorMessage { get; set; }

        public RegexValidationRule(string pattern, string errorMessage = null)
        {
            Pattern = pattern;
            ErrorMessage = errorMessage ?? $"Value does not match required pattern: {pattern}";
        }

        public override ValidationResult Validate(object value)
        {
            var result = new ValidationResult(true);

            if (value != null)
            {
                var stringValue = value.ToString();
                if (!System.Text.RegularExpressions.Regex.IsMatch(stringValue, Pattern))
                {
                    result.AddError(ErrorMessage);
                }
            }

            return result;
        }
    }
}