# Immediate Action Items and Priority Files

## Files to Update Immediately (Week 1)

### 1. **HIGH PRIORITY**: Update `SchedulerDataExceptions.cs`

**File Location**: `Instrument.Data\Exceptions\SchedulerDataExceptions.cs`

**Action**: Replace the entire content with the enhanced exception classes from the "Enhanced Exception Classes Implementation" artifact.

**Impact**: Foundation for all improved error handling throughout the system.

---

### 2. **HIGH PRIORITY**: Create `TimeoutOptions.cs`

**File Location**: `Instrument.Data\Configuration\TimeoutOptions.cs` (new file)

**Action**: Create the configuration class for timeout management.

```csharp
namespace Instrument.Data.Configuration;

public class TimeoutOptions
{
    public const string SectionName = "Timeouts";
    
    public TimeSpan DatabaseTimeout { get; set; } = TimeSpan.FromSeconds(20);
    public TimeSpan ServiceTimeout { get; set; } = TimeSpan.FromSeconds(25);
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan GrpcTimeout { get; set; } = TimeSpan.FromSeconds(15);
    public TimeSpan ValidationTimeout { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan OrchestrationTimeout { get; set; } = TimeSpan.FromMinutes(10);
    public TimeSpan OrchestrationStepTimeout { get; set; } = TimeSpan.FromMinutes(2);
}
```

---

### 3. **HIGH PRIORITY**: Update `appsettings.json`

**File Location**: Root of project

**Action**: Add timeout configuration section:

```json
{
  "Timeouts": {
    "DatabaseTimeout": "00:00:20",
    "ServiceTimeout": "00:00:25", 
    "RequestTimeout": "00:00:30",
    "GrpcTimeout": "00:00:15",
    "ValidationTimeout": "00:00:05",
    "OrchestrationTimeout": "00:10:00",
    "OrchestrationStepTimeout": "00:02:00"
  }
}
```

---

### 4. **HIGH PRIORITY**: Update `IRepository.cs`

**File Location**: `Instrument.Data\IRepository.cs`

**Current Content**: Basic interface without CancellationToken support

**Action**: Replace with enhanced interface:

```csharp
namespace Instrument.Data;

public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);
}
```

---

### 5. **HIGH PRIORITY**: Update `IParameterService.cs`

**File Location**: `Instrument.Data\IParameterService.cs`

**Current Content**: Methods without CancellationToken parameters

**Action**: Add CancellationToken parameters to all async methods:

```csharp
namespace Instrument.Data;

public interface IParameterService
{
    Task<Parameter?> GetParameterByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Parameter> CreateParameterAsync(Parameter parameter, CancellationToken cancellationToken = default);
    Task UpdateParameterAsync(Parameter parameter, CancellationToken cancellationToken = default);
    Task DeleteParameterAsync(int id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Parameter>> GetAllParametersAsync(CancellationToken cancellationToken = default);
    Task ValidateParameterValueAsync(Parameter parameter, string value, CancellationToken cancellationToken = default);
    Task<bool> TryValidateParameterValueAsync(Parameter parameter, string value, CancellationToken cancellationToken = default);
}
```

---

### 6. **MEDIUM PRIORITY**: Update `ParameterService.cs`

**File Location**: `Instrument.Data\Services\ParameterService.cs`

**Current Content**: Service without comprehensive CancellationToken and timeout support

**Action**: Replace with the enhanced service implementation from the "CancellationToken Refactoring Implementation" artifact.

**Key Changes**:
- Add `TimeoutOptions` dependency injection
- Add CancellationToken parameters to all methods
- Implement timeout handling with linked cancellation sources
- Enhanced exception handling with new exception types

---

### 7. **MEDIUM PRIORITY**: Update `ServiceCollectionExtensions.cs`

**File Location**: `Instrument.Data\ServiceCollectionExtensions.cs`

**Current Content**: Basic service registration

**Action**: Enhance with configuration support:

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSchedulerDataServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure timeout options
        services.Configure<TimeoutOptions>(configuration.GetSection(TimeoutOptions.SectionName));
        
        // Register existing services with enhanced dependencies
        services.AddScoped<IParameterRepository, ParameterRepository>();
        services.AddScoped<IParameterService, ParameterService>();
        // ... other service registrations
        
        return services;
    }
}
```

---

## Files to Update in Week 2

### 8. **MEDIUM PRIORITY**: Update Repository Base Class

**File Location**: `Instrument.Data\Repository\Repository.cs` (if it exists) or create new base

**Action**: Implement the enhanced base repository from the refactoring artifact with comprehensive CancellationToken support.

---

### 9. **MEDIUM PRIORITY**: Update `ParameterRepository.cs`

**File Location**: `Instrument.Data\Repository\ParameterRepository.cs`

**Action**: Inherit from enhanced base repository and add CancellationToken support to all Entity Framework operations.

---

### 10. **LOW PRIORITY**: Update Unit Tests

**File Location**: `Instrument.Data.UT\ParameterServiceTests.cs`

**Action**: Add cancellation and timeout test scenarios from the "Enhanced Unit Tests" artifact.

---

## Critical Integration Points

### GrpcGateway Integration

**Files to Monitor**:
- `Instrument.Data\Grpc\GrpcGateway.cs` - Already has good CancellationToken support
- `Instrument.Data\Grpc\ExponentialBackoffRetryPolicy.cs` - Update to use new exception types

**Action**: Update exception throwing to use new `GrpcTimeoutException` and `GrpcServiceUnavailableException` types.

### Orchestration Integration  

**Files to Monitor**:
- `Instrument.Data\Orchestration\ConfigurationImport\ConfigurationImportManager.cs`
- `Instrument.Data\Orchestration\IProcessManager.cs`

**Action**: Ensure proper exception handling with new `OrchestrationException` types.

---

## Validation Checklist

After implementing the immediate changes, verify:

### ✅ **Configuration Validation**
```bash
# Test configuration loading
dotnet run --environment Development
# Check logs for timeout configuration loading
```

### ✅ **Interface Compatibility**
```csharp
// Verify interfaces compile with new signatures
dotnet build
```

### ✅ **Exception Handling**
```csharp
// Test new exception types
var ex = new GrpcTimeoutException("TestService", "TestOp", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), true);
Assert.NotNull(ex.ServiceName);
```

### ✅ **Dependency Injection**
```csharp
// Verify TimeoutOptions injection
var timeoutOptions = serviceProvider.GetRequiredService<IOptions<TimeoutOptions>>();
Assert.NotNull(timeoutOptions.Value);
```

---

## Deployment Strategy

### Phase 1 Deployment (This Week)
1. **Deploy exception updates** - Low risk, improves diagnostics
2. **Deploy configuration classes** - No breaking changes
3. **Deploy interface updates** - Backward compatible with default parameters

### Phase 2 Deployment (Next Week)  
1. **Deploy service implementations** - Behind feature flag
2. **Deploy repository updates** - Gradual rollout
3. **Monitor performance** - Watch for timeout issues

### Phase 3 Deployment (Week 3)
1. **Enable all features** - Full rollout
2. **Remove feature flags** - Clean up temporary code
3. **Performance optimization** - Tune timeout values

---

## Monitoring and Alerts

### Key Metrics to Watch
- **Exception rates by type** - Monitor new exception patterns  
- **Request timeout frequency** - Adjust timeout values if needed
- **Cancellation token usage** - Verify proper cancellation propagation
- **Database connection timeout** - Monitor EF Core timeout behavior

### Alerts to Configure
- High rate of `GrpcTimeoutException`
- Increase in `OrchestrationException` occurrences  
- Unusual patterns in `OperationCanceledException`
- Performance degradation after updates

---

## Risk Mitigation

### Rollback Plan
1. **Immediate rollback**: Revert to previous Docker image/deployment
2. **Partial rollback**: Disable new exception types via feature flag
3. **Configuration rollback**: Revert timeout configurations to defaults

### Testing Strategy
1. **Unit tests**: All existing tests must pass
2. **Integration tests**: Test timeout and cancellation scenarios
3. **Load tests**: Verify performance under concurrent cancellations
4. **Smoke tests**: Basic functionality validation

### Communication Plan
1. **Team notification**: Changes to interfaces and behavior
2. **Operations briefing**: New monitoring and alerting
3. **Documentation updates**: API documentation and troubleshooting guides

---

## Next Steps

1. **Start with the HIGH PRIORITY files** listed above
2. **Test each change incrementally** - don't update everything at once
3. **Monitor application behavior** after each deployment
4. **Gather team feedback** on new patterns and conventions
5. **Plan Phase 2 updates** based on Phase 1 results

The goal is to achieve maximum improvement with minimal risk by focusing on the foundational changes first, then building upon them systematically.
