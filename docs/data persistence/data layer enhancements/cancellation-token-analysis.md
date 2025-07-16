# CancellationToken Usage Analysis and Best Practices

## Current State Assessment

### ✅ **Good Usage Found**
- **GrpcGateway**: Properly uses CancellationToken with timeout handling and linked cancellation sources
- **ProcessManager/Orchestration**: Interface includes CancellationToken parameters
- **RetryPolicy**: Accepts and propagates cancellation tokens correctly
- **Unit Tests**: Include cancellation testing scenarios

### ❌ **Missing Usage**
- **Service Layer**: Methods like `ParameterService` don't accept CancellationTokens
- **Repository Layer**: Database operations lack cancellation support
- **Entity Framework**: No CancellationToken propagation to EF operations
- **Background Operations**: No cancellation support in long-running operations

### ⚠️ **Inconsistent Patterns**
- Some async methods accept CancellationToken, others don't
- No standard pattern for default token handling
- Missing timeout coordination between different layers
- No cancellation token scoping strategy

## Recommended CancellationToken Strategy

### 1. **Comprehensive Layer Integration**

All async operations should support cancellation:
- Controllers → Services → Repositories → Database
- Background services and long-running operations
- External service calls (gRPC, HTTP, etc.)
- File I/O and network operations

### 2. **Token Scoping Strategy**

#### **Request-Scoped Tokens**
- HTTP request cancellation (client disconnect)
- User-initiated cancellation (UI actions)
- Request timeout enforcement

#### **Operation-Scoped Tokens**
- Individual operation timeouts
- Retry policy timeout boundaries
- Circuit breaker integration

#### **Service-Scoped Tokens**
- Application shutdown tokens
- Health check timeouts
- Background service lifecycle

#### **Composite Tokens**
- Linked cancellation for multiple scopes
- Timeout + request + shutdown combinations
- Hierarchical cancellation structures

### 3. **Implementation Patterns**

#### **Default Parameter Pattern**
```csharp
public async Task<T> OperationAsync(CancellationToken cancellationToken = default)
```

#### **Timeout Integration Pattern**
```csharp
using var timeoutCts = new CancellationTokenSource(timeout);
using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
    cancellationToken, 
    timeoutCts.Token);
```

#### **Repository Pattern Extension**
```csharp
public async Task<T> GetByIdAsync(int id, CancellationToken cancellationToken = default)
{
    return await _context.Entities
        .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
}
```

## Implementation Priorities

### **Phase 1: Critical Operations (High Priority)**
1. **Database Operations**: Add CancellationToken to all EF operations
2. **External Service Calls**: Ensure all gRPC/HTTP calls support cancellation
3. **File I/O Operations**: Configuration loading, data import/export
4. **Long-Running Operations**: Batch processing, data migration

### **Phase 2: Service Layer Integration (Medium Priority)**
1. **Service Methods**: Update all service interfaces and implementations
2. **Validation Operations**: Add cancellation to complex validation logic
3. **Business Logic**: Support cancellation in multi-step operations
4. **Caching Operations**: Add cancellation to cache read/write operations

### **Phase 3: Infrastructure and Optimization (Lower Priority)**
1. **Background Services**: Implement graceful shutdown with cancellation
2. **Health Checks**: Add timeout and cancellation support
3. **Metrics Collection**: Cancel long-running metric calculations
4. **Log Processing**: Support cancellation in log aggregation

## Best Practices Implementation

### 1. **Cancellation Handling**

#### **Proper Exception Handling**
```csharp
try
{
    await SomeOperationAsync(cancellationToken);
}
catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
{
    // Expected cancellation - log and rethrow
    _logger.LogInformation("Operation was cancelled");
    throw;
}
```

#### **Resource Cleanup**
```csharp
using var registration = cancellationToken.Register(() => 
{
    // Cleanup resources when cancelled
    CleanupResources();
});
```

### 2. **Timeout Management**

#### **Cascading Timeouts**
- HTTP Request: 30 seconds
- Service Operation: 25 seconds  
- Database Query: 20 seconds
- Individual Steps: 15 seconds

#### **Timeout Configuration**
```csharp
public class TimeoutOptions
{
    public TimeSpan DatabaseTimeout { get; set; } = TimeSpan.FromSeconds(20);
    public TimeSpan ServiceTimeout { get; set; } = TimeSpan.FromSeconds(25);
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
```

### 3. **Monitoring and Observability**

#### **Cancellation Metrics**
- Operation cancellation rates by type
- Timeout frequency analysis
- Resource cleanup success rates
- User-initiated vs system-initiated cancellations

#### **Logging Integration**
```csharp
_logger.LogDebug("Operation {OperationName} starting with timeout {Timeout}ms", 
    operationName, timeout.TotalMilliseconds);

if (cancellationToken.IsCancellationRequested)
{
    _logger.LogInformation("Operation {OperationName} was cancelled before completion", 
        operationName);
}
```

## Testing Strategy

### **Unit Tests**
- Test cancellation at each layer
- Verify timeout behavior
- Test resource cleanup
- Validate exception propagation

### **Integration Tests**
- End-to-end cancellation flows
- Cross-service cancellation propagation
- Database connection cancellation
- External service timeout handling

### **Performance Tests**
- Resource usage during cancellation
- Cleanup performance under load
- Timeout accuracy under various conditions
- Memory leaks in cancellation scenarios

## Migration Strategy

### **Backward Compatibility**
- Add CancellationToken parameters with default values
- Maintain existing method signatures where possible
- Gradual migration without breaking changes
- Deprecation warnings for old patterns

### **Implementation Order**
1. Repository layer (foundation)
2. Service layer (business logic)
3. Controller layer (presentation)
4. Background services (infrastructure)

### **Validation and Testing**
- Automated tests for all new cancellation paths
- Performance regression testing
- Manual testing of user scenarios
- Load testing with cancellation patterns

## Common Pitfalls to Avoid

### **Anti-Patterns**
1. **Ignoring CancellationToken**: Not checking for cancellation in long operations
2. **Synchronous Blocking**: Using `.Result` or `.Wait()` with cancellation tokens
3. **Token Reuse**: Reusing cancelled tokens across operations
4. **Missing Cleanup**: Not disposing resources when operations are cancelled
5. **Improper Linking**: Creating token hierarchies that don't properly propagate

### **Performance Considerations**
- Token registration overhead in hot paths
- Memory allocation in linked token creation
- Disposal patterns for token sources
- Thread synchronization in cancellation callbacks

## Tools and Utilities

### **Extension Methods**
```csharp
public static async Task<T> WithTimeout<T>(this Task<T> task, TimeSpan timeout, CancellationToken cancellationToken = default)
{
    using var timeoutCts = new CancellationTokenSource(timeout);
    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
    
    return await task.WaitAsync(linkedCts.Token);
}
```

### **Configuration Integration**
```csharp
services.Configure<TimeoutOptions>(configuration.GetSection("Timeouts"));
services.AddScoped<ICancellationTokenProvider, RequestScopedCancellationTokenProvider>();
```

### **Middleware Integration**
```csharp
public class CancellationTokenMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var timeout = context.Request.Headers.TryGetValue("Request-Timeout", out var timeoutHeader) 
            ? TimeSpan.FromSeconds(int.Parse(timeoutHeader)) 
            : TimeSpan.FromSeconds(30);
            
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            context.RequestAborted, 
            timeoutCts.Token);
            
        context.Items["CancellationToken"] = linkedCts.Token;
        await next(context);
    }
}
```
