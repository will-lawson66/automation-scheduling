# Advanced Configuration System - Project Structure

## Directory Structure
```
AutomationSystem/
├── config/
│   ├── automation.config.json          # Main system configuration
│   ├── email.config.json               # Email module configuration
│   ├── fileprocessing.config.json      # File processing configuration
│   ├── modules/                        # Module-specific configs
│   │   ├── reporting.config.json
│   │   ├── scheduling.config.json
│   │   └── notifications.config.json
│   └── environments/                   # Environment overrides
│       ├── development.json
│       ├── staging.json
│       └── production.json
├── schemas/
│   ├── automation.schema.json          # Schema for main config
│   ├── email.schema.json               # Schema for email config
│   ├── fileprocessing.schema.json      # Schema for file processing
│   └── modules/                        # Module schemas
│       ├── reporting.schema.json
│       └── scheduling.schema.json
├── plugins/
│   ├── DatabaseConfigPlugin.dll        # Database configuration plugin
│   ├── VaultConfigPlugin.dll           # HashiCorp Vault plugin
│   └── RedisConfigPlugin.dll           # Redis configuration plugin
├── logs/
├── src/
│   ├── Configuration/
│   │   ├── SchemaValidatedProvider.cs
│   │   ├── HotReloadManager.cs
│   │   └── PluginRegistry.cs
│   ├── Services/
│   └── Models/
└── templates/                          # Configuration templates
    ├── module.template.json
    └── environment.template.json
```

## Example Configuration Files

### automation.config.json
```json
{
  "$schema": "../schemas/automation.schema.json",
  "$metadata": {
    "version": "2.0",
    "lastModified": "2025-07-09T10:00:00Z",
    "dependencies": ["email", "fileprocessing"],
    "environment": "development"
  },
  "system": {
    "name": "AutomationSystem",
    "version": "1.0.0",
    "instanceId": "auto-001",
    "startupDelay": 5000
  },
  "logging": {
    "level": "Information",
    "providers": ["Console", "File", "EventLog"],
    "logPath": "./logs",
    "maxLogFiles": 10,
    "maxLogSizeBytes": 104857600,
    "includeScopes": true,
    "structured": true
  },
  "performance": {
    "maxConcurrentTasks": 10,
    "taskTimeoutSeconds": 300,
    "enableMetrics": true,
    "metricsEndpoint": "http://localhost:9090/metrics",
    "healthCheckInterval": 30000,
    "memoryThresholdMB": 1024
  },
  "security": {
    "requireAuthentication": true,
    "allowedUsers": ["automation@company.com"],
    "allowedRoles": ["AutomationOperator", "Administrator"],
    "sessionTimeoutMinutes": 480,
    "encryptionKey": "${ENCRYPTION_KEY}",
    "auditLogging": true
  },
  "modules": {
    "email": {
      "enabled": true,
      "configPath": "./config/email.config.json",
      "startupOrder": 1
    },
    "fileprocessing": {
      "enabled": true,
      "configPath": "./config/fileprocessing.config.json",
      "startupOrder": 2
    },
    "reporting": {
      "enabled": false,
      "configPath": "./config/modules/reporting.config.json",
      "startupOrder": 3
    }
  }
}
```

### email.config.json
```json
{
  "$schema": "../schemas/email.schema.json",
  "$metadata": {
    "version": "1.5",
    "dependencies": [],
    "tags": {
      "module": "email",
      "critical": true
    }
  },
  "smtp": {
    "server": "smtp.company.com",
    "port": 587,
    "security": "tls",
    "authentication": {
      "username": "automation@company.com",
      "passwordSource": "environment",
      "passwordKey": "EMAIL_PASSWORD"
    },
    "connectionTimeout": 30000,
    "sendTimeout": 60000,
    "retryPolicy": {
      "maxRetries": 3,
      "retryDelay": 5000,
      "backoffMultiplier": 2.0
    },
    "connectionPooling": {
      "enabled": true,
      "maxPoolSize": 5,
      "idleTimeout": 300000
    }
  },
  "templates": {
    "basePath": "./templates/email",
    "defaultTemplate": "default.html",
    "cacheEnabled": true,
    "cacheTimeoutMinutes": 30,
    "supportedFormats": ["html", "text", "markdown"]
  },
  "processing": {
    "batchSize": 50,
    "maxConcurrentSends": 5,
    "queueProcessingInterval": 10000,
    "deadLetterQueue": {
      "enabled": true,
      "maxRetries": 5,
      "retentionDays": 7
    },
    "throttling": {
      "enabled": true,
      "messagesPerMinute": 100,
      "burstLimit": 200
    }
  },
  "monitoring": {
    "trackDelivery": true,
    "trackOpens": false,
    "trackClicks": false,
    "webhookUrl": "https://webhook.company.com/email-events"
  }
}
```

### fileprocessing.config.json
```json
{
  "$schema": "../schemas/fileprocessing.schema.json",
  "$metadata": {
    "version": "2.1",
    "dependencies": ["automation"],
    "tags": {
      "module": "fileprocessing",
      "performance": "high"
    }
  },
  "watchFolders": [
    {
      "name": "invoices",
      "path": "C:\\Automation\\Input\\Invoices",
      "pattern": "*.pdf",
      "includeSubdirectories": true,
      "filters": {
        "minFileSize": 1024,
        "maxFileAge": 24,
        "excludePatterns": ["*.tmp", "*.lock", "~*"]
      },
      "processingRules": {
        "priority": "high",
        "processor": "pdf",
        "autoArchive": true
      }
    },
    {
      "name": "reports", 
      "path": "C:\\Automation\\Input\\Reports",
      "pattern": "*.{xlsx,csv}",
      "includeSubdirectories": false,
      "filters": {
        "minFileSize": 512,
        "maxFileAge": 48,
        "requiredInFilename": ["report", "data"]
      },
      "processingRules": {
        "priority": "medium",
        "processor": "excel",
        "autoArchive": true,
        "notifyOnCompletion": true
      }
    }
  ],
  "processing": {
    "outputPath": "C:\\Automation\\Output",
    "archivePath": "C:\\Automation\\Archive",
    "errorPath": "C:\\Automation\\Errors",
    "tempPath": "C:\\Automation\\Temp",
    "batchSize": 100,
    "parallelism": 4,
    "maxFileSize": 104857600,
    "processingTimeout": 300000,
    "cleanupInterval": 3600000,
    "processors": [
      {
        "type": "pdf",
        "enabled": true,
        "priority": 1,
        "settings": {
          "extractText": true,
          "extractImages": false,
          "ocrEnabled": true,
          "ocrLanguage": "eng",
          "qualityThreshold": 0.8
        }
      },
      {
        "type": "excel",
        "enabled": true,
        "priority": 2,
        "settings": {
          "readAllSheets": true,
          "ignoreEmptyRows": true,
          "headerRow": 1,
          "dateFormat": "MM/dd/yyyy",
          "numberFormat": "en-US"
        }
      },
      {
        "type": "csv",
        "enabled": true,
        "priority": 3,
        "settings": {
          "delimiter": ",",
          "hasHeaders": true,
          "encoding": "UTF-8",
          "skipEmptyLines": true,
          "trimWhitespace": true
        }
      }
    ]
  },
  "notifications": {
    "enabled": true,
    "channels": ["email", "webhook"],
    "events": ["processing_complete", "processing_error", "large_batch"],
    "emailRecipients": ["operations@company.com"],
    "webhookUrl": "https://webhook.company.com/file-events"
  },
  "archiving": {
    "enabled": true,
    "strategy": "date-based",
    "retentionDays": 90,
    "compressionEnabled": true,
    "compressionLevel": 6
  }
}
```

## Environment-Specific Overrides

### environments/production.json
```json
{
  "logging": {
    "level": "Warning",
    "providers": ["File", "EventLog", "Splunk"]
  },
  "performance": {
    "maxConcurrentTasks": 20,
    "enableMetrics": true,
    "metricsEndpoint": "https://metrics.company.com/automation"
  },
  "security": {
    "requireAuthentication": true,
    "sessionTimeoutMinutes": 240,
    "auditLogging": true
  },
  "Email": {
    "smtp": {
      "server": "smtp-prod.company.com",
      "connectionPooling": {
        "maxPoolSize": 10
      }
    },
    "processing": {
      "batchSize": 100,
      "maxConcurrentSends": 10
    }
  },
  "FileProcessing": {
    "processing": {
      "parallelism": 8,
      "batchSize": 200
    }
  }
}
```

## Usage in Different Console Applications

### EmailProcessor.exe - Program.cs
```csharp
public static async Task Main(string[] args)
{
    var host = Host.CreateDefaultBuilder(args)
        .ConfigureAppConfiguration((context, config) =>
        {
            config.Sources.Clear();
            
            // Load with schema validation and hot reload
            config.AddSchemaValidatedJson(
                "./config/automation.config.json", 
                "./schemas/automation.schema.json");
            config.AddSchemaValidatedJson(
                "./config/email.config.json", 
                "./schemas/email.schema.json");
                
            config.AddEnvironmentVariables("EMAIL_");
            config.AddCommandLine(args);
        })
        .ConfigureServices((context, services) =>
        {
            services.Configure<EmailConfig>(context.Configuration.GetSection("Email"));
            services.AddHotReloadConfiguration(builder =>
            {
                builder.AddJsonConfiguration("email", "./config/email.config.json");
                builder.AddChangeHandler<EmailConfig>("Email", (old, current) =>
                {
                    Console.WriteLine("Email configuration updated - restarting processing");
                });
            });
            
            services.AddHostedService<EmailProcessorService>();
        })
        .Build();

    await host.RunAsync();
}
```

### FileProcessor.exe - Program.cs  
```csharp
public static async Task Main(string[] args)
{
    var host = Host.CreateDefaultBuilder(args)
        .ConfigureAppConfiguration((context, config) =>
        {
            config.Sources.Clear();
            
            config.AddSchemaValidatedJson(
                "./config/automation.config.json", 
                "./schemas/automation.schema.json");
            config.AddSchemaValidatedJson(
                "./config/fileprocessing.config.json", 
                "./schemas/fileprocessing.schema.json");
                
            // Plugin-based external configuration
            var pluginManager = new PluginBasedConfigurationManager(/*...*/);
            var externalConfig = await pluginManager.LoadConfigurationAsync(
                new DatabaseConfigurationSource 
                { 
                    Location = "ConfigDB",
                    Properties = new() { ["table"] = "FileProcessingSettings" }
                });
            
            config.AddConfiguration(externalConfig);
        })
        .Build();

    await host.RunAsync();
}
```

## Benefits Summary

### Schema-Based Validation
- **Early Error Detection**: Catch configuration errors before runtime
- **Documentation**: Schema serves as living documentation
- **Default Values**: Automatically apply sensible defaults
- **IDE Support**: IntelliSense and validation in VS Code/Visual Studio

### Plugin-Based Architecture  
- **Extensibility**: Add new configuration sources without code changes
- **Modularity**: Each source type handled by specialized plugins
- **Testability**: Mock configuration sources easily
- **Legacy Integration**: Support legacy configuration formats

### Hot Reload Capabilities
- **Zero Downtime**: Update configuration without restarting
- **Real-time Adaptation**: Services respond immediately to changes
- **Development Productivity**: Instant feedback during development
- **Operational Flexibility**: Tune performance parameters live

This advanced configuration system provides enterprise-grade capabilities while maintaining simplicity for common scenarios. The combination of these patterns gives you maximum flexibility for complex automation scenarios while ensuring reliability and maintainability.