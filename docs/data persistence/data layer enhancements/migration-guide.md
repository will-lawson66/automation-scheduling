# Migration Guide: Updating Existing Code

This guide provides step-by-step instructions for migrating existing code to use the enhanced exception handling and CancellationToken patterns.

## Phase 1: Update Exception Handling

### Step 1: Update SchedulerDataExceptions.cs

Replace the existing exception file with the enhanced version:

```csharp
// BEFORE: Basic exception hierarchy
public class SchedulerDataException : Exception
{
    public SchedulerDataException(string message) : base(message) { }
    public SchedulerDataException(string message, Exception innerException) : base(message, innerException) { }
}

// AFTER: Enhanced with correlation and timing
public class SchedulerDataException : Exception
{
    public string? CorrelationId { get; }
    public DateTime Timestamp { get; }

    public SchedulerDataException(string message, string? correlationId = null) : base(message) 
    { 
        CorrelationId = correlationId;
        Timestamp = DateTime.UtcNow;
    }
    // ... additional constructors
}
```

### Step 2: Update Exception Throwing

**BEFORE:**
```csharp
public async Task<Parameter> GetParameterByIdAsync(int id)
{
    try
    {
        return await _repository.GetByIdAsync(id);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error retrieving parameter with ID: {Id}", id);
        throw new StorageProviderException("GetParameter", ex);
    }
}
```

**AFTER:**
```csharp
public async Task<Parameter?> GetParameterByIdAsync(int id, CancellationToken cancellationToken = default)
{
    using var timeoutCts = new CancellationTokenSource(_timeoutOptions.ServiceTimeout);
    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
    
    var correlationId = GetCorrelationId();
    
    try
    {
        linkedCts.Token.ThrowIfCancellationRequested();
        return await _repository.GetByIdAsync(id, linkedCts.Token);
    }
    catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
    {
        throw new GrpcTimeoutException("ParameterService", "GetParameterById", 
            _timeoutOptions.ServiceTimeout, timeoutCts.Elapsed, true, correlationId);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        _logger.LogInformation("GetParameter operation was cancelled for ID: {Id}", id);
        throw;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error retrieving parameter with ID: {Id}", id);
        throw new StorageProviderException("GetParameter", ex, correlationId);
    }
}
```

## Phase 2: Update Repository Layer

### Step 3: Update Repository Interfaces

**BEFORE:**
```csharp
public interface IParameterRepository
{
    Task<Parameter?> GetByIdAsync(int id);
    Task<IEnumerable<Parameter>> GetAllAsync();
    Task AddAsync(Parameter parameter);
    Task UpdateAsync(Parameter parameter);
    Task DeleteAsync(int id);
    Task SaveChangesAsync();
}
```

**AFTER:**
```csharp
public interface IParameterRepository : IRepository<Parameter>
{
    Task<IEnumerable<Parameter>> GetParametersByTypeAsync(ParameterType type, CancellationToken cancellationToken = default);
    Task<Parameter?> GetParameterByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);
}
```

### Step 4: Update Repository Implementations

**BEFORE:**
```csharp
public class ParameterRepository : IParameterRepository
{
    private readonly SchedulerDbContext _context;

    public async Task<Parameter?> GetByIdAsync(int id)
    {
        return await _context.Parameters.FindAsync(id);
    }

    public async Task<IEnumerable<Parameter>> GetAllAsync()
    {
        return await _context.Parameters.ToListAsync();
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
```

**AFTER:**
```csharp
public class ParameterRepository : Repository<Parameter>, IParameterRepository
{
    public ParameterRepository(SchedulerDbContext dbContext, ILogger<ParameterRepository> logger)
        : base(dbContext, logger)
    {
    }

    public async Task<IEnumerable<Parameter>> GetParametersByTypeAsync(ParameterType type, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving parameters of type: {ParameterType}", type);
        
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await _context.Set<Parameter>()
                .Where(p => p.Type == type)
                .ToListAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("GetParametersByType operation was cancelled for type {ParameterType}", type);
            throw;
        }
    }

    public async Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await _context.Set<Parameter>().AnyAsync(p => p.Id == id, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Exists check was cancelled for parameter ID: {Id}", id);
            throw;
        }
    }
}
```

## Phase 3: Update Service Layer

### Step 5: Update Service Interfaces

**BEFORE:**
```csharp
public interface IParameterService
{
    Task<Parameter?> GetParameterByIdAsync(int id);
    Task<Parameter> CreateParameterAsync(Parameter parameter);
    Task UpdateParameterAsync(Parameter parameter);
    Task DeleteParameterAsync(int id);
    Task<IEnumerable<Parameter>> GetAllParametersAsync();
    void ValidateParameterValue(Parameter parameter, string value);
}
```

**AFTER:**
```csharp
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

### Step 6: Update Service Implementations

**BEFORE:**
```csharp
public class ParameterService : IParameterService
{
    private readonly ILogger<ParameterService> _logger;
    private readonly IParameterRepository _parameterRepository;

    public async Task<Parameter?> GetParameterByIdAsync(int id)
    {
        _logger.LogInformation("Retrieving parameter with ID: {Id}", id);
        try
        {
            return await _parameterRepository.GetByIdAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving parameter with ID: {Id}", id);
            throw new StorageProviderException("GetParameter", ex);
        }
    }
}
```

**AFTER:**
```csharp
public class ParameterService : IParameterService
{
    private readonly ILogger<ParameterService> _logger;
    private readonly IParameterRepository _parameterRepository;
    private readonly TimeoutOptions _timeoutOptions;

    public ParameterService(
        IParameterRepository parameterRepository,
        ILogger<ParameterService> logger,
        IOptions<TimeoutOptions> timeoutOptions)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _parameterRepository = parameterRepository ?? throw new ArgumentNullException(nameof(parameterRepository));
        _timeoutOptions = timeoutOptions?.Value ?? throw new ArgumentNullException(nameof(timeoutOptions));
    }

    public async Task<Parameter?> GetParameterByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        using var timeoutCts = new CancellationTokenSource(_timeoutOptions.ServiceTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        
        _logger.LogInformation("Retrieving parameter with ID: {Id}", id);
        
        try
        {
            linkedCts.Token.ThrowIfCancellationRequested();
            return await _parameterRepository.GetByIdAsync(id, linkedCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
        {
            _logger.LogWarning("GetParameter operation timed out for ID: {Id} after {Timeout}ms", 
                id, _timeoutOptions.ServiceTimeout.TotalMilliseconds);
            throw new GrpcTimeoutException("ParameterService", "GetParameterById", 
                _timeoutOptions.ServiceTimeout, timeoutCts.Elapsed, true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("GetParameter operation was cancelled for ID: {Id}", id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving parameter with ID: {Id}", id);
            throw new StorageProviderException("GetParameter", ex);
        }
    }
}
```

## Phase 4: Update Controllers

### Step 7: Update Controller Methods

**BEFORE:**
```csharp
[HttpGet("{id}")]
public async Task<ActionResult<Parameter>> GetParameter(int id)
{
    try
    {
        var parameter = await _parameterService.GetParameterByIdAsync(id);
        if (parameter == null)
        {
            return NotFound();
        }
        return Ok(parameter);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error getting parameter {Id}", id);
        return StatusCode(500, "Internal server error");
    }
}
```

**AFTER:**
```csharp
[HttpGet("{id}")]
public async Task<ActionResult<Parameter>> GetParameter(int id, CancellationToken cancellationToken = default)
{
    try
    {
        var parameter = await _parameterService.GetParameterByIdAsync(id, cancellationToken);
        if (parameter == null)
        {
            return NotFound();
        }
        return Ok(parameter);
    }
    catch (EntityNotFoundException ex)
    {
        _logger.LogWarning("Parameter not found: {Id}", id);
        return NotFound(new { message = ex.Message, correlationId = ex.CorrelationId });
    }
    catch (GrpcTimeoutException ex)
    {
        _logger.LogWarning("Parameter request timed out: {Id}", id);
        return StatusCode(408, new { message = "Request timed out", correlationId = ex.CorrelationId });
    }
    catch (OperationCanceledException)
    {
        _logger.LogInformation("Parameter request cancelled: {Id}", id);
        return StatusCode(499, "Request cancelled");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error getting parameter {Id}", id);
        var correlationId = ex is SchedulerDataException sdEx ? sdEx.CorrelationId : null;
        return StatusCode(500, new { message = "Internal server error", correlationId });
    }
}
```

## Phase 5: Update Unit Tests

### Step 8: Update Test Methods

**BEFORE:**
```csharp
[Fact]
public async Task GetParameterByIdAsync_WithValidId_ReturnsParameter()
{
    // Arrange
    var parameterId = 1;
    var expectedParameter = new Parameter { Id = parameterId, Name = "TestParameter" };
    
    _mockRepository.Setup(x => x.GetByIdAsync(parameterId))
                  .ReturnsAsync(expectedParameter);

    // Act
    var result = await _service.GetParameterByIdAsync(parameterId);

    // Assert
    Assert.NotNull(result);
    Assert.Equal(expectedParameter.Id, result.Id);
}
```

**AFTER:**
```csharp
[Fact]
public async Task GetParameterByIdAsync_WithValidId_ReturnsParameter()
{
    // Arrange
    var parameterId = 1;
    var expectedParameter = new Parameter { Id = parameterId, Name = "TestParameter" };
    
    _mockRepository.Setup(x => x.GetByIdAsync(parameterId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(expectedParameter);

    // Act
    var result = await _service.GetParameterByIdAsync(parameterId);

    // Assert
    Assert.NotNull(result);
    Assert.Equal(expectedParameter.Id, result.Id);
    Assert.Equal(expectedParameter.Name, result.Name);
    
    _mockRepository.Verify(x => x.GetByIdAsync(parameterId, It.IsAny<CancellationToken>()), Times.Once);
}

[Fact]
public async Task GetParameterByIdAsync_WithCancellation_ThrowsOperationCanceledException()
{
    // Arrange
    var parameterId = 1;
    var cancellationTokenSource = new CancellationTokenSource();
    
    _mockRepository.Setup(x => x.GetByIdAsync(parameterId, It.IsAny<CancellationToken>()))
                  .Returns<int, CancellationToken>((id, ct) => 
                  {
                      ct.ThrowIfCancellationRequested();
                      return Task.FromResult<Parameter?>(null);
                  });

    cancellationTokenSource.Cancel();

    // Act & Assert
    await Assert.ThrowsAsync<OperationCanceledException>(() => 
        _service.GetParameterByIdAsync(parameterId, cancellationTokenSource.Token));
}

[Fact]
public async Task GetParameterByIdAsync_WithTimeout_ThrowsGrpcTimeoutException()
{
    // Arrange
    var parameterId = 1;
    
    _mockRepository.Setup(x => x.GetByIdAsync(parameterId, It.IsAny<CancellationToken>()))
                  .Returns<int, CancellationToken>(async (id, ct) =>
                  {
                      // Simulate operation that takes longer than timeout
                      await Task.Delay(TimeSpan.FromSeconds(10), ct);
                      return new Parameter { Id = id };
                  });

    // Act & Assert
    var exception = await Assert.ThrowsAsync<GrpcTimeoutException>(() => 
        _service.GetParameterByIdAsync(parameterId));
        
    Assert.Equal("ParameterService", exception.ServiceName);
    Assert.Equal("GetParameterById", exception.OperationName);
    Assert.True(exception.IsOperationTimeout);
}
```

## Phase 6: Update Configuration

### Step 9: Update appsettings.json

Add the new configuration sections:

```json
{
  "Timeouts": {
    "DatabaseTimeout": "00:00:20",
    "ServiceTimeout": "00:00:25",
    "RequestTimeout": "00:00:30",
    "GrpcTimeout": "00:00:15",
    "BackgroundOperationTimeout": "00:05:00",
    "ValidationTimeout": "00:00:05",
    "OrchestrationTimeout": "00:10:00",
    "OrchestrationStepTimeout": "00:02:00",
    "HealthCheckTimeout": "00:00:10"
  },
  "RetryPolicy": {
    "MaxAttempts": 3,
    "BaseDelayMs": 1000,
    "BackoffMultiplier": 2.0,
    "MaxDelayMs": 30000,
    "UseJitter": true,
    "JitterPercentage": 0.1
  }
}
```

### Step 10: Update Program.cs or Startup.cs

**BEFORE:**
```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddDbContext<SchedulerDbContext>(options =>
        options.UseSqlServer(connectionString));
    
    services.AddScoped<IParameterService, ParameterService>();
    services.AddScoped<IParameterRepository, ParameterRepository>();
}
```

**AFTER:**
```csharp
public void ConfigureServices(IServiceCollection services)
{
    // Add enhanced configuration and services
    services.AddSchedulerDbContext(Configuration);
    services.AddSchedulerDataServices(Configuration);
    
    // Add middleware support
    services.AddScoped<RequestTimeoutMiddleware>();
    services.AddScoped<CorrelationIdMiddleware>();
}

public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
{
    // Add enhanced middleware and pipeline
    app.UseSchedulerDataMiddleware();
    
    // Other middleware...
    app.UseRouting();
    app.UseEndpoints(endpoints => endpoints.MapControllers());
}
```

## Phase 7: Verification and Testing

### Step 11: Run Migration Tests

Create and run these verification tests:

```csharp
public class MigrationVerificationTests
{
    [Fact]
    public void AllRepositories_Implement_CancellationTokenSupport()
    {
        var repositoryTypes = Assembly.GetAssembly(typeof(IParameterRepository))
            .GetTypes()
            .Where(t => t.IsInterface && t.Name.EndsWith("Repository"))
            .ToList();

        foreach (var repositoryType in repositoryTypes)
        {
            var methods = repositoryType.GetMethods();
            var asyncMethods = methods.Where(m => m.ReturnType.IsGenericType && 
                m.ReturnType.GetGenericTypeDefinition() == typeof(Task<>)).ToList();

            foreach (var method in asyncMethods)
            {
                var hascancellationToken = method.GetParameters()
                    .Any(p => p.ParameterType == typeof(CancellationToken));
                
                Assert.True(hasancellationToken, 
                    $"Method {repositoryType.Name}.{method.Name} should have CancellationToken parameter");
            }
        }
    }

    [Fact]
    public void AllServices_Implement_CancellationTokenSupport()
    {
        var serviceTypes = Assembly.GetAssembly(typeof(IParameterService))
            .GetTypes()
            .Where(t => t.IsInterface && t.Name.EndsWith("Service"))
            .ToList();

        foreach (var serviceType in serviceTypes)
        {
            var methods = serviceType.GetMethods();
            var asyncMethods = methods.Where(m => m.ReturnType.IsGenericType && 
                m.ReturnType.GetGenericTypeDefinition() == typeof(Task<>)).ToList();

            foreach (var method in asyncMethods)
            {
                var hasDefaultCancellationToken = method.GetParameters()
                    .Any(p => p.ParameterType == typeof(CancellationToken) && p.HasDefaultValue);
                
                Assert.True(hasDefaultCancellationToken, 
                    $"Method {serviceType.Name}.{method.Name} should have default CancellationToken parameter");
            }
        }
    }
}
```

## Checklist for Migration

### Before Starting Migration
- [ ] Back up existing code and database
- [ ] Set up feature flags for gradual rollout
- [ ] Create migration branch in source control
- [ ] Review existing test coverage

### During Migration
- [ ] Update exception hierarchy
- [ ] Add configuration classes and options
- [ ] Update repository interfaces and implementations
- [ ] Update service interfaces and implementations
- [ ] Update controller methods
- [ ] Add timeout and cancellation token support
- [ ] Update unit tests with cancellation scenarios
- [ ] Add integration tests for timeout behavior

### After Migration
- [ ] Run full test suite
- [ ] Performance test with cancellation scenarios
- [ ] Load test with concurrent cancellations
- [ ] Update documentation and API specs
- [ ] Train development team on new patterns
- [ ] Monitor error rates and performance in production

### Validation Steps
- [ ] All async methods have CancellationToken parameters
- [ ] All exception types include correlation IDs
- [ ] All timeouts are properly configured
- [ ] All cancellation scenarios are tested
- [ ] Structured logging includes exception context
- [ ] Health checks validate timeout behavior

## Common Pitfalls to Avoid

1. **Don't ignore CancellationToken**: Always check for cancellation in long-running operations
2. **Don't reuse cancelled tokens**: Create new tokens for each operation
3. **Don't forget timeout hierarchies**: Service timeout > Repository timeout > Database timeout
4. **Don't block on async operations**: Never use .Result or .Wait() with CancellationTokens
5. **Don't forget resource cleanup**: Dispose of CancellationTokenSource instances
6. **Don't catch and ignore OperationCanceledException**: Let cancellation exceptions propagate appropriately

This migration guide ensures a systematic approach to updating your codebase while maintaining backward compatibility and achieving the enhanced functionality.
