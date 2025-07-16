# Scheduler Application Deployment Guide

## Overview

This guide provides comprehensive instructions for deploying the Scheduler Application across different environments. It covers infrastructure requirements, configuration management, deployment procedures, and operational considerations.

## System Requirements

### Hardware Requirements

#### Minimum Requirements
- **CPU**: 4 cores, 2.4 GHz
- **RAM**: 8 GB
- **Storage**: 100 GB SSD
- **Network**: 1 Gbps Ethernet

#### Recommended Requirements
- **CPU**: 8 cores, 3.0 GHz or higher
- **RAM**: 16 GB or higher
- **Storage**: 500 GB NVMe SSD
- **Network**: 10 Gbps Ethernet
- **Redundancy**: RAID 1 for system, RAID 10 for data

#### Production Requirements
- **CPU**: 16 cores, 3.2 GHz or higher
- **RAM**: 32 GB or higher
- **Storage**: 1 TB NVMe SSD with backup
- **Network**: Redundant 10 Gbps connections
- **High Availability**: Clustered deployment

### Software Requirements

#### Operating System
- **Primary**: Windows Server 2019/2022
- **Alternative**: Ubuntu Server 20.04 LTS or later
- **Container**: Docker Desktop or Docker Engine

#### Runtime Dependencies
- **.NET Runtime**: .NET 8.0 or later
- **Database**: SQL Server 2019 or later / PostgreSQL 13+
- **Message Queue**: RabbitMQ 3.8+ or Azure Service Bus
- **Monitoring**: Prometheus + Grafana or Application Insights

## Environment Configurations

### Development Environment

```yaml
# docker-compose.dev.yml
version: '3.8'
services:
  scheduler:
    build: .
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__Default=Server=dev-db;Database=SchedulerDev;
      - Logging__LogLevel__Default=Debug
    ports:
      - "5000:80"
    volumes:
      - ./logs:/app/logs
      - ./config:/app/config
    depends_on:
      - dev-db
      - redis
      
  dev-db:
    image: mcr.microsoft.com/mssql/server:2019-latest
    environment:
      - ACCEPT_EULA=Y
      - SA_PASSWORD=DevPassword123!
    ports:
      - "1433:1433"
    volumes:
      - dev-db-data:/var/opt/mssql
      
  redis:
    image: redis:6-alpine
    ports:
      - "6379:6379"
      
volumes:
  dev-db-data:
```

### Testing Environment

```yaml
# docker-compose.test.yml
version: '3.8'
services:
  scheduler:
    build: 
      context: .
      dockerfile: Dockerfile.test
    environment:
      - ASPNETCORE_ENVIRONMENT=Testing
      - ConnectionStrings__Default=Server=test-db;Database=SchedulerTest;
      - Testing__EnableMockServices=true
    depends_on:
      - test-db
      - mock-hardware
      
  test-db:
    image: mcr.microsoft.com/mssql/server:2019-latest
    environment:
      - ACCEPT_EULA=Y
      - SA_PASSWORD=TestPassword123!
    tmpfs:
      - /var/opt/mssql # Use in-memory storage for faster tests
      
  mock-hardware:
    build: ./test/MockHardware
    ports:
      - "5001:80"
```

### Production Environment

```yaml
# docker-compose.prod.yml
version: '3.8'
services:
  scheduler:
    image: scheduler:${VERSION}
    deploy:
      replicas: 2
      restart_policy:
        condition: on-failure
        max_attempts: 3
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__Default=${DB_CONNECTION_STRING}
      - ApplicationInsights__InstrumentationKey=${AI_KEY}
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - prod-logs:/app/logs
      - prod-config:/app/config:ro
      - ssl-certs:/app/certs:ro
    networks:
      - prod-network
      
  nginx:
    image: nginx:alpine
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./nginx.conf:/etc/nginx/nginx.conf:ro
      - ssl-certs:/etc/ssl/certs:ro
    depends_on:
      - scheduler
    networks:
      - prod-network

networks:
  prod-network:
    driver: overlay

volumes:
  prod-logs:
  prod-config:
  ssl-certs:
```

## Configuration Management

### Environment-Specific Configurations

#### appsettings.Development.json
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft": "Information"
    }
  },
  "Scheduler": {
    "MaxConcurrentSamples": 5,
    "ExecutionTimeoutMinutes": 60,
    "EnableDebugMode": true
  },
  "Database": {
    "CommandTimeout": 30,
    "ConnectionRetryCount": 3
  },
  "Grpc": {
    "HardwareEngineEndpoint": "http://localhost:5001",
    "ConnectionTimeout": "00:00:30"
  }
}
```

#### appsettings.Production.json
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "System": "Warning"
    }
  },
  "Scheduler": {
    "MaxConcurrentSamples": 20,
    "ExecutionTimeoutMinutes": 480,
    "EnableDebugMode": false
  },
  "Database": {
    "CommandTimeout": 120,
    "ConnectionRetryCount": 5,
    "EnableConnectionPooling": true
  },
  "Monitoring": {
    "EnableMetrics": true,
    "MetricsPort": 9090,
    "EnableHealthChecks": true
  },
  "Security": {
    "EnableHttps": true,
    "RequireHttps": true,
    "HstsMaxAge": 31536000
  }
}
```

### Secrets Management

#### Azure Key Vault Configuration
```json
{
  "KeyVault": {
    "VaultUri": "https://scheduler-keyvault.vault.azure.net/",
    "ClientId": "${AZURE_CLIENT_ID}",
    "ClientSecret": "${AZURE_CLIENT_SECRET}"
  }
}
```

#### Environment Variables
```bash
# Database
export DB_CONNECTION_STRING="Server=prod-db-server;Database=SchedulerProd;Integrated Security=true;"
export DB_PASSWORD="$(cat /run/secrets/db_password)"

# Application Insights
export AI_INSTRUMENTATION_KEY="$(cat /run/secrets/ai_key)"

# Certificates
export SSL_CERT_PATH="/app/certs/scheduler.crt"
export SSL_KEY_PATH="/app/certs/scheduler.key"

# Service Discovery
export CONSUL_ENDPOINT="http://consul:8500"
export SERVICE_NAME="scheduler"
export SERVICE_VERSION="${BUILD_NUMBER}"
```

## Database Setup

### Database Migration Scripts

#### Initial Database Setup
```sql
-- CreateDatabase.sql
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'SchedulerProd')
BEGIN
    CREATE DATABASE SchedulerProd
    COLLATE SQL_Latin1_General_CP1_CI_AS;
END

USE SchedulerProd;

-- Enable features
ALTER DATABASE SchedulerProd SET ENABLE_BROKER;
ALTER DATABASE SchedulerProd SET READ_COMMITTED_SNAPSHOT ON;
```

#### User and Permissions Setup
```sql
-- CreateUsers.sql
-- Application user
CREATE LOGIN SchedulerApp WITH PASSWORD = '$(APP_DB_PASSWORD)';
CREATE USER SchedulerApp FOR LOGIN SchedulerApp;

-- Grant permissions
ALTER ROLE db_datareader ADD MEMBER SchedulerApp;
ALTER ROLE db_datawriter ADD MEMBER SchedulerApp;
ALTER ROLE db_ddladmin ADD MEMBER SchedulerApp;

-- Monitoring user (read-only)
CREATE LOGIN SchedulerMonitor WITH PASSWORD = '$(MONITOR_DB_PASSWORD)';
CREATE USER SchedulerMonitor FOR LOGIN SchedulerMonitor;
ALTER ROLE db_datareader ADD MEMBER SchedulerMonitor;
```

### Entity Framework Migrations

#### Migration Commands
```bash
# Generate migration
dotnet ef migrations add InitialCreate --project Scheduling.Data

# Update database
dotnet ef database update --project Scheduling.Data --connection "$DB_CONNECTION_STRING"

# Generate SQL script
dotnet ef migrations script --project Scheduling.Data --output migration.sql
```

## Deployment Procedures

### CI/CD Pipeline

#### Azure DevOps Pipeline
```yaml
# azure-pipelines.yml
trigger:
  branches:
    include:
      - main
      - develop

variables:
  buildConfiguration: 'Release'
  dockerRegistryServiceConnection: 'SchedulerRegistry'
  imageRepository: 'scheduler'
  containerRegistry: 'schedulerregistry.azurecr.io'

stages:
- stage: Build
  displayName: Build stage
  jobs:
  - job: Build
    displayName: Build
    pool:
      vmImage: 'ubuntu-latest'
    steps:
    - task: Docker@2
      displayName: Build and push image
      inputs:
        command: buildAndPush
        repository: $(imageRepository)
        dockerfile: '**/Dockerfile'
        containerRegistry: $(dockerRegistryServiceConnection)
        tags: |
          $(Build.BuildId)
          latest

- stage: Deploy_Dev
  displayName: Deploy to Development
  dependsOn: Build
  condition: eq(variables['Build.SourceBranch'], 'refs/heads/develop')
  jobs:
  - deployment: Deploy
    displayName: Deploy
    pool:
      vmImage: 'ubuntu-latest'
    environment: 'development'
    strategy:
      runOnce:
        deploy:
          steps:
          - task: DockerCompose@0
            displayName: Run Docker Compose
            inputs:
              action: 'Run services'
              dockerComposeFile: 'docker-compose.dev.yml'

- stage: Deploy_Prod
  displayName: Deploy to Production
  dependsOn: Build
  condition: eq(variables['Build.SourceBranch'], 'refs/heads/main')
  jobs:
  - deployment: Deploy
    displayName: Deploy
    pool:
      vmImage: 'ubuntu-latest'
    environment: 'production'
    strategy:
      runOnce:
        deploy:
          steps:
          - task: KubernetesManifest@0
            displayName: Deploy to Kubernetes
            inputs:
              action: 'deploy'
              manifests: 'k8s/*.yml'
```

### Kubernetes Deployment

#### Deployment Manifest
```yaml
# k8s/deployment.yml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: scheduler
  labels:
    app: scheduler
spec:
  replicas: 3
  selector:
    matchLabels:
      app: scheduler
  template:
    metadata:
      labels:
        app: scheduler
    spec:
      containers:
      - name: scheduler
        image: schedulerregistry.azurecr.io/scheduler:latest
        ports:
        - containerPort: 80
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: ConnectionStrings__Default
          valueFrom:
            secretKeyRef:
              name: scheduler-secrets
              key: db-connection-string
        resources:
          requests:
            memory: "512Mi"
            cpu: "250m"
          limits:
            memory: "2Gi"
            cpu: "1000m"
        livenessProbe:
          httpGet:
            path: /health
            port: 80
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 80
          initialDelaySeconds: 5
          periodSeconds: 5
---
apiVersion: v1
kind: Service
metadata:
  name: scheduler-service
spec:
  selector:
    app: scheduler
  ports:
  - protocol: TCP
    port: 80
    targetPort: 80
  type: LoadBalancer
```

#### ConfigMap and Secrets
```yaml
# k8s/configmap.yml
apiVersion: v1
kind: ConfigMap
metadata:
  name: scheduler-config
data:
  appsettings.Production.json: |
    {
      "Scheduler": {
        "MaxConcurrentSamples": 20,
        "ExecutionTimeoutMinutes": 480
      },
      "Monitoring": {
        "EnableMetrics": true,
        "MetricsPort": 9090
      }
    }
---
apiVersion: v1
kind: Secret
metadata:
  name: scheduler-secrets
type: Opaque
data:
  db-connection-string: <base64-encoded-connection-string>
  ai-instrumentation-key: <base64-encoded-key>
```

### Health Checks Implementation

```csharp
// HealthChecks/SchedulerHealthCheck.cs
public class SchedulerHealthCheck : IHealthCheck
{
    private readonly IAssayManager _assayManager;
    private readonly IConfigurationManager _configManager;
    private readonly ILogger<SchedulerHealthCheck> _logger;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken)
    {
        try
        {
            var data = new Dictionary<string, object>();
            
            // Check AssayManager status
            var assayStatus = _assayManager.GetExecutionStatus();
            data["AssayManagerStatus"] = assayStatus.IsActive ? "Active" : "Inactive";
            data["TotalSamples"] = assayStatus.TotalSequenceGroups;
            
            // Check configuration availability
            var configTest = _configManager.GetConfiguration<int>("Scheduler.MaxConcurrentSamples");
            data["ConfigurationAvailable"] = configTest > 0;
            
            // Check database connectivity
            // Implementation depends on your data layer
            
            return HealthCheckResult.Healthy("Scheduler is healthy", data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return HealthCheckResult.Unhealthy("Scheduler health check failed", ex);
        }
    }
}

// Startup.cs - Health check registration
public void ConfigureServices(IServiceCollection services)
{
    services.AddHealthChecks()
        .AddCheck<SchedulerHealthCheck>("scheduler")
        .AddDbContextCheck<SchedulingDbContext>("database")
        .AddCheck("grpc-hardware", () => CheckGrpcConnection())
        .AddApplicationInsightsPublisher();
}

public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
{
    app.UseHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });
    
    app.UseHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready"),
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });
}
```

## Monitoring and Observability

### Application Insights Setup

```csharp
// Program.cs
public static void Main(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);
    
    // Add Application Insights
    builder.Services.AddApplicationInsightsTelemetry(options =>
    {
        options.InstrumentationKey = builder.Configuration["ApplicationInsights:InstrumentationKey"];
        options.EnableQuickPulseMetricStream = true;
        options.EnableAdaptiveSampling = true;
    });
    
    // Add custom telemetry
    builder.Services.AddSingleton<ITelemetryInitializer, SchedulerTelemetryInitializer>();
    
    var app = builder.Build();
    
    // Configure telemetry
    app.UseApplicationInsightsRequestTelemetry();
    app.UseApplicationInsightsExceptionTelemetry();
    
    app.Run();
}
```

### Prometheus Metrics

```csharp
// Metrics/SchedulerMetrics.cs
public class SchedulerMetrics
{
    private static readonly Counter SamplesProcessed = Metrics
        .CreateCounter("scheduler_samples_processed_total", "Total number of samples processed");
    
    private static readonly Histogram SampleProcessingDuration = Metrics
        .CreateHistogram("scheduler_sample_processing_duration_seconds", "Sample processing duration");
    
    private static readonly Gauge ActiveSamples = Metrics
        .CreateGauge("scheduler_active_samples", "Number of active samples");
    
    public static void IncrementSamplesProcessed() => SamplesProcessed.Inc();
    
    public static void RecordSampleProcessingDuration(double duration) => 
        SampleProcessingDuration.Observe(duration);
    
    public static void SetActiveSamples(double count) => ActiveSamples.Set(count);
}

// Startup.cs
public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
{
    app.UseMetricServer(); // Expose /metrics endpoint
    app.UseHttpMetrics();  // Collect HTTP metrics
}
```

## Security Configuration

### HTTPS Setup

```csharp
// Program.cs - HTTPS configuration
public static void Main(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);
    
    // Configure HTTPS
    builder.Services.AddHttpsRedirection(options =>
    {
        options.RedirectStatusCode = StatusCodes.Status308PermanentRedirect;
        options.HttpsPort = 443;
    });
    
    // Configure HSTS
    builder.Services.AddHsts(options =>
    {
        options.Preload = true;
        options.IncludeSubDomains = true;
        options.MaxAge = TimeSpan.FromDays(365);
    });
    
    var app = builder.Build();
    
    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
    }
    
    app.UseHttpsRedirection();
    app.Run();
}
```

### Authentication and Authorization

```csharp
// Authentication setup
public void ConfigureServices(IServiceCollection services)
{
    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = Configuration["Auth:Authority"];
            options.Audience = Configuration["Auth:Audience"];
            options.RequireHttpsMetadata = !Environment.IsDevelopment();
        });
    
    services.AddAuthorization(options =>
    {
        options.AddPolicy("SchedulerAdmin", policy =>
            policy.RequireClaim("role", "scheduler.admin"));
        options.AddPolicy("SchedulerOperator", policy =>
            policy.RequireClaim("role", "scheduler.operator"));
    });
}
```

## Backup and Recovery

### Database Backup Strategy

```sql
-- Full backup script
BACKUP DATABASE SchedulerProd 
TO DISK = '/backup/SchedulerProd_Full_$(Date).bak'
WITH FORMAT, INIT, COMPRESSION, CHECKSUM;

-- Transaction log backup
BACKUP LOG SchedulerProd 
TO DISK = '/backup/SchedulerProd_Log_$(DateTime).trn'
WITH COMPRESSION, CHECKSUM;
```

### Application Data Backup

```bash
#!/bin/bash
# backup-script.sh

# Configuration files
tar -czf "config-backup-$(date +%Y%m%d).tar.gz" /app/config/

# CMR library
tar -czf "cmr-backup-$(date +%Y%m%d).tar.gz" /app/data/cmr/

# Logs (last 30 days)
find /app/logs -name "*.log" -mtime -30 | tar -czf "logs-backup-$(date +%Y%m%d).tar.gz" -T -

# Upload to cloud storage
aws s3 cp *.tar.gz s3://scheduler-backups/$(date +%Y/%m/%d)/
```

## Troubleshooting Guide

### Common Issues

#### Database Connection Issues
```bash
# Check database connectivity
sqlcmd -S server -U user -P password -Q "SELECT 1"

# Check connection pool
netstat -an | grep :1433

# Review connection string configuration
dotnet user-secrets list --project Scheduling.Application
```

#### Performance Issues
```bash
# Check resource usage
docker stats scheduler_container

# Review logs for performance issues
docker logs scheduler_container | grep -i "slow\|timeout\|performance"

# Check database performance
-- Query to find slow queries
SELECT TOP 10 
    total_elapsed_time/execution_count AS avg_time,
    total_elapsed_time,
    execution_count,
    SUBSTRING(st.text, (qs.statement_start_offset/2)+1,
        ((CASE qs.statement_end_offset 
          WHEN -1 THEN DATALENGTH(st.text)
         ELSE qs.statement_end_offset
         END - qs.statement_start_offset)/2) + 1) AS statement_text
FROM sys.dm_exec_query_stats qs
CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) st
ORDER BY total_elapsed_time/execution_count DESC;
```

#### Memory Leaks
```bash
# Monitor memory usage
docker exec scheduler_container cat /proc/meminfo

# Generate memory dump
dotnet-dump collect -p $(pgrep -f "Scheduling.Application")

# Analyze with dotnet-dump
dotnet-dump analyze core_dump_file
```

## Maintenance Procedures

### Regular Maintenance Tasks

#### Weekly Tasks
- Review system performance metrics
- Check disk space usage
- Verify backup completion
- Update security patches

#### Monthly Tasks
- Database maintenance (index rebuild, statistics update)
- Log rotation and archival
- Security vulnerability assessment
- Capacity planning review

#### Quarterly Tasks
- Disaster recovery testing
- Performance baseline review
- Configuration audit
- Documentation updates

### Maintenance Scripts

```bash
#!/bin/bash
# maintenance.sh

echo "Starting maintenance tasks..."

# Database maintenance
echo "Running database maintenance..."
sqlcmd -S $DB_SERVER -U $DB_USER -P $DB_PASSWORD -Q "
EXEC sp_updatestats;
EXEC sp_recompile;
DBCC FREEPROCCACHE;
"

# Log cleanup
echo "Cleaning up old logs..."
find /app/logs -name "*.log" -mtime +30 -delete

# Docker cleanup
echo "Cleaning up Docker resources..."
docker system prune -f
docker volume prune -f

# Health check
echo "Running health checks..."
curl -f http://localhost/health || echo "Health check failed!"

echo "Maintenance completed."
```

## Conclusion

This deployment guide provides a comprehensive approach to deploying the Scheduler Application across different environments. Following these procedures ensures a reliable, secure, and maintainable deployment that can scale with organizational needs.

Key success factors:
- Automated deployment pipelines
- Comprehensive monitoring and alerting
- Regular backup and recovery testing
- Proactive maintenance procedures
- Clear troubleshooting documentation

Regular review and updates of deployment procedures ensure continued effectiveness and alignment with evolving requirements.