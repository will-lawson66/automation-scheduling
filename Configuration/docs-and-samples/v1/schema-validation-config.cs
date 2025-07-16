using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Schema;
using Newtonsoft.Json.Linq;
using System.Text.Json;

// Schema-based configuration provider with validation
public class SchemaValidatedConfigurationProvider : ConfigurationProvider
{
    private readonly string _configPath;
    private readonly string _schemaPath;
    private readonly SchemaValidationOptions _options;

    public SchemaValidatedConfigurationProvider(string configPath, string schemaPath, SchemaValidationOptions options = null)
    {
        _configPath = configPath;
        _schemaPath = schemaPath;
        _options = options ?? new SchemaValidationOptions();
    }

    public override void Load()
    {
        var configFiles = GetConfigurationFiles();
        var schemas = LoadSchemas();
        var validatedConfigurations = new Dictionary<string, string>();

        foreach (var configFile in configFiles)
        {
            ValidateAndLoadConfiguration(configFile, schemas, validatedConfigurations);
        }

        Data = validatedConfigurations;
    }

    private void ValidateAndLoadConfiguration(string configFile, Dictionary<string, JSchema> schemas, Dictionary<string, string> target)
    {
        try
        {
            var json = File.ReadAllText(configFile);
            var jsonObject = JObject.Parse(json);
            
            // Determine which schema to use
            var schemaName = DetermineSchemaName(configFile, jsonObject);
            
            if (schemas.TryGetValue(schemaName, out var schema))
            {
                // Validate against schema
                var isValid = jsonObject.IsValid(schema, out IList<string> validationErrors);
                
                if (!isValid)
                {
                    var errorMessage = $"Configuration validation failed for {configFile}:\n" +
                                     string.Join("\n", validationErrors.Select(e => $"  - {e}"));
                    
                    if (_options.StrictValidation)
                    {
                        throw new ConfigurationValidationException(errorMessage);
                    }
                    else
                    {
                        Console.WriteLine($"WARNING: {errorMessage}");
                        if (_options.LogValidationErrors)
                        {
                            LogValidationErrors(configFile, validationErrors);
                        }
                    }
                }

                // Apply default values from schema if missing
                ApplySchemaDefaults(jsonObject, schema);
            }

            // Convert to configuration format
            var sectionName = Path.GetFileNameWithoutExtension(configFile).Replace(".config", "");
            FlattenJsonToConfiguration(jsonObject, sectionName, target);
        }
        catch (Exception ex)
        {
            throw new ConfigurationLoadException($"Failed to load configuration from {configFile}", ex);
        }
    }

    private Dictionary<string, JSchema> LoadSchemas()
    {
        var schemas = new Dictionary<string, JSchema>();
        
        if (Directory.Exists(_schemaPath))
        {
            // Load all schema files
            var schemaFiles = Directory.GetFiles(_schemaPath, "*.schema.json");
            
            foreach (var schemaFile in schemaFiles)
            {
                var schemaName = Path.GetFileNameWithoutExtension(schemaFile).Replace(".schema", "");
                var schemaJson = File.ReadAllText(schemaFile);
                var schema = JSchema.Parse(schemaJson);
                schemas[schemaName] = schema;
                
                Console.WriteLine($"Loaded schema: {schemaName}");
            }
        }
        else if (File.Exists(_schemaPath))
        {
            // Single schema file
            var schemaJson = File.ReadAllText(_schemaPath);
            var schema = JSchema.Parse(schemaJson);
            schemas["default"] = schema;
        }

        return schemas;
    }

    private string DetermineSchemaName(string configFile, JObject jsonObject)
    {
        // Check for explicit schema reference
        if (jsonObject.TryGetValue("$schema", out var schemaToken))
        {
            var schemaRef = schemaToken.ToString();
            return Path.GetFileNameWithoutExtension(schemaRef);
        }

        // Infer from filename
        var fileName = Path.GetFileNameWithoutExtension(configFile);
        if (fileName.Contains("."))
        {
            var parts = fileName.Split('.');
            return parts[0]; // e.g., "email.config.json" -> "email"
        }

        return "default";
    }

    private void ApplySchemaDefaults(JObject jsonObject, JSchema schema)
    {
        // Apply default values from schema where properties are missing
        ApplyDefaultsRecursive(jsonObject, schema);
    }

    private void ApplyDefaultsRecursive(JToken token, JSchema schema)
    {
        if (schema.Properties != null && token is JObject obj)
        {
            foreach (var property in schema.Properties)
            {
                if (!obj.ContainsKey(property.Key) && property.Value.Default != null)
                {
                    obj[property.Key] = property.Value.Default;
                }
                else if (obj[property.Key] != null && property.Value.Properties != null)
                {
                    ApplyDefaultsRecursive(obj[property.Key], property.Value);
                }
            }
        }
    }

    private void FlattenJsonToConfiguration(JObject jsonObject, string prefix, Dictionary<string, string> target)
    {
        foreach (var property in jsonObject.Properties())
        {
            var key = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}:{property.Name}";
            
            if (property.Value.Type == JTokenType.Object)
            {
                FlattenJsonToConfiguration((JObject)property.Value, key, target);
            }
            else if (property.Value.Type == JTokenType.Array)
            {
                var array = (JArray)property.Value;
                for (int i = 0; i < array.Count; i++)
                {
                    if (array[i].Type == JTokenType.Object)
                    {
                        FlattenJsonToConfiguration((JObject)array[i], $"{key}:{i}", target);
                    }
                    else
                    {
                        target[$"{key}:{i}"] = array[i].ToString();
                    }
                }
            }
            else
            {
                target[key] = property.Value.ToString();
            }
        }
    }

    private IEnumerable<string> GetConfigurationFiles()
    {
        if (File.Exists(_configPath))
        {
            return new[] { _configPath };
        }

        if (Directory.Exists(_configPath))
        {
            return Directory.GetFiles(_configPath, "*.json", SearchOption.AllDirectories)
                           .Where(f => !Path.GetFileName(f).Contains(".schema."));
        }

        throw new DirectoryNotFoundException($"Configuration path not found: {_configPath}");
    }

    private void LogValidationErrors(string configFile, IList<string> errors)
    {
        var logPath = Path.Combine(Path.GetDirectoryName(configFile), "validation-errors.log");
        var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {configFile}\n" +
                      string.Join("\n", errors.Select(e => $"  {e}")) + "\n\n";
        
        File.AppendAllText(logPath, logEntry);
    }
}

// Configuration options for schema validation
public class SchemaValidationOptions
{
    public bool StrictValidation { get; set; } = true;
    public bool LogValidationErrors { get; set; } = true;
    public bool ApplyDefaults { get; set; } = true;
    public string ValidationLogPath { get; set; }
}

// Custom exceptions
public class ConfigurationValidationException : Exception
{
    public ConfigurationValidationException(string message) : base(message) { }
    public ConfigurationValidationException(string message, Exception innerException) : base(message, innerException) { }
}

public class ConfigurationLoadException : Exception
{
    public ConfigurationLoadException(string message) : base(message) { }
    public ConfigurationLoadException(string message, Exception innerException) : base(message, innerException) { }
}

// Extension method for easy integration
public static class SchemaValidationExtensions
{
    public static IConfigurationBuilder AddSchemaValidatedJson(
        this IConfigurationBuilder builder,
        string configPath,
        string schemaPath,
        SchemaValidationOptions options = null)
    {
        var source = new SchemaValidatedConfigurationSource
        {
            ConfigPath = configPath,
            SchemaPath = schemaPath,
            Options = options ?? new SchemaValidationOptions()
        };

        builder.Add(source);
        return builder;
    }
}

public class SchemaValidatedConfigurationSource : IConfigurationSource
{
    public string ConfigPath { get; set; }
    public string SchemaPath { get; set; }
    public SchemaValidationOptions Options { get; set; }

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new SchemaValidatedConfigurationProvider(ConfigPath, SchemaPath, Options);
    }
}