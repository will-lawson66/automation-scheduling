# AssayManager Service & AssaySample Domain Design

## 1. Overview

The AssayManager service is the central orchestrator for managing and executing assay samples in the Scheduler Application. It handles the complete lifecycle of assay samples from creation through execution and completion, coordinating with various subsystems including inventory management, execution planning, and hardware control.

## 2. Core Design Principles

### 2.1 Domain-Driven Design
- **AssaySample** serves as the primary domain entity representing a sample with its associated tests
- **AssayManager** acts as the domain service coordinating the execution lifecycle
- Rich domain models with business logic encapsulated within entities

### 2.2 Event-Driven Architecture
- Status changes trigger events for loose coupling between components
- Asynchronous processing for non-blocking operations
- Event sourcing for audit trails and state reconstruction

### 2.3 Thread Safety
- Thread-safe collections for concurrent access
- Immutable data structures where appropriate
- Proper synchronization for shared resources

### 2.4 Separation of Concerns
- Clear boundaries between inventory management, execution planning, and hardware control
- Dependency injection for testability and flexibility
- Interface-based design for extensibility

## 3. AssaySample Domain Entity

### 3.1 Core Properties

```csharp
public class AssaySample
{
    public Guid Id { get; private set; }
    public Sample Sample { get; private set; }
    public List<Assay> Assays { get; private set; }
    public AssayStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string ErrorMessage { get; private set; }
    public Dictionary<string, object> Metadata { get; private set; }
    public SequenceGroup SequenceGroup { get; private set; }
    public IFlrSampleTestContext FlrContext { get; private set; }
    public int Priority { get; private set; }
    public List<AssayResult> Results { get; private set; }
}
```

### 3.2 Key Responsibilities

#### 3.2.1 State Management
- Maintains current execution status with proper state transitions
- Validates state changes based on business rules
- Provides event notifications for status changes

#### 3.2.2 Inventory Requirements
- Calculates required inventory based on assay specifications
- Validates inventory availability before execution
- Tracks reserved inventory items

#### 3.2.3 Execution Coordination
- Creates appropriate SequenceGroup for hardware execution
- Maintains execution context and metadata
- Collects and validates execution results

#### 3.2.4 Business Logic
```csharp
public class AssaySample
{
    public void SetStatus(AssayStatus newStatus)
    {
        if (!IsValidStatusTransition(Status, newStatus))
            throw new InvalidOperationException($"Invalid status transition from {Status} to {newStatus}");
        
        var oldStatus = Status;
        Status = newStatus;
        
        UpdateTimestamps(newStatus);
        OnStatusChanged?.Invoke(this, new AssayStatusChangedEventArgs(oldStatus, newStatus));
    }
    
    public List<InventoryRequirement> ValidateInventoryRequirements()
    {
        var requirements = new List<InventoryRequirement>();
        
        foreach (var assay in Assays)
        {
            requirements.AddRange(assay.GetInventoryRequirements());
        }
        
        // Add sample-specific requirements
        requirements.AddRange(Sample.GetInventoryRequirements());
        
        return requirements;
    }
    
    public SequenceGroup CreateSequenceGroup()
    {
        if (SequenceGroup != null)
            throw new InvalidOperationException("SequenceGroup already created");
        
        var sequenceGroup = new SequenceGroup(Guid.NewGuid(), $"Sample_{Sample.Id}");
        
        foreach (var assay in Assays)
        {
            var sequences = assay.CreateSequences(Sample);
            sequenceGroup.AddSequences(sequences);
        }
        
        SequenceGroup = sequenceGroup;
        return sequenceGroup;
    }
    
    public TimeSpan GetEstimatedDuration()
    {
        return Assays.Aggregate(TimeSpan.Zero, (total, assay) => total.Add(assay.EstimatedDuration));
    }
    
    public bool CanExecute()
    {
        return Status == AssayStatus.InventoryReserved && 
               Assays.All(a => a.ValidateParameters()) &&
               Sample.ValidateProperties();
    }
}
```

### 3.3 Status Lifecycle

The AssaySample follows a well-defined status lifecycle:

1. **Created** - Initial state when instantiated
2. **Queued** - Added to execution queue in AssayManager
3. **InventoryCheck** - Inventory requirements being validated
4. **InventoryReserved** - Required inventory reserved successfully
5. **InventoryUnavailable** - Insufficient inventory available
6. **InProgress** - Execution started, sequences running
7. **Completed** - All sequences executed successfully
8. **Failed** - Execution failed due to error
9. **Cancelled** - Execution cancelled by user or system

## 4. AssayManager Service

### 4.1 Core Architecture

```csharp
public class AssayManager : IAssayManager, IDisposable
{
    private readonly ConcurrentDictionary<Guid, AssaySample> _assaySamples;
    private readonly AssayExecutionPlanner _executionPlanner;
    private readonly SequenceGroupManager _sequenceGroupManager;
    private readonly IFlrContextFactory _flrContextFactory;
    private readonly IInventoryService _inventoryService;
    private readonly ILogger<AssayManager> _logger;
    private readonly SemaphoreSlim _executionSemaphore;
    
    private IFlrAssayRunContext _flrAssayRunContext;
    private CancellationTokenSource _cancellationTokenSource;
    private Task _executionTask;
}
```

### 4.2 Key Responsibilities

#### 4.2.1 Lifecycle Management
- **Registration**: Add/remove AssaySample instances
- **Status Tracking**: Monitor and update sample status
- **Event Handling**: Process status change events
- **Cleanup**: Remove completed/failed samples

#### 4.2.2 Execution Planning
- **Plan Creation**: Generate optimized execution plans
- **Resource Allocation**: Coordinate with inventory service
- **Priority Management**: Handle sample prioritization
- **Conflict Resolution**: Resolve resource contentions

#### 4.2.3 Inventory Coordination
- **Availability Checking**: Validate inventory before execution
- **Reservation Management**: Reserve/release inventory items
- **Error Handling**: Handle inventory-related failures

#### 4.2.4 Hardware Integration
- **Sequence Group Creation**: Convert samples to executable sequences
- **Execution Monitoring**: Track hardware execution progress
- **Result Processing**: Handle execution results and errors

### 4.3 Core Operations

#### 4.3.1 Sample Management
```csharp
public async Task<bool> AddAssaySamples(IEnumerable<AssaySample> assaySamples)
{
    var addedSamples = new List<AssaySample>();
    
    foreach (var sample in assaySamples)
    {
        if (_assaySamples.TryAdd(sample.Id, sample))
        {
            sample.OnStatusChanged += HandleAssaySampleStatusChanged;
            sample.SetStatus(AssayStatus.Queued);
            addedSamples.Add(sample);
            _logger.LogInformation("Added AssaySample {SampleId} to queue", sample.Id);
        }
        else
        {
            _logger.LogWarning("Failed to add AssaySample {SampleId} - already exists", sample.Id);
        }
    }
    
    if (addedSamples.Count > 0)
    {
        await TriggerInventoryCheck(addedSamples);
    }
    
    return addedSamples.Count == assaySamples.Count();
}

private async Task TriggerInventoryCheck(List<AssaySample> samples)
{
    foreach (var sample in samples)
    {
        try
        {
            var requirements = sample.ValidateInventoryRequirements();
            var checkResult = await _inventoryService.CheckAvailability(requirements);
            
            if (checkResult.IsAvailable)
            {
                await _inventoryService.ReserveInventory(requirements);
                sample.SetStatus(AssayStatus.InventoryReserved);
            }
            else
            {
                sample.SetStatus(AssayStatus.InventoryUnavailable);
                _logger.LogWarning("Inventory unavailable for sample {SampleId}: {MissingItems}", 
                    sample.Id, string.Join(", ", checkResult.MissingItems.Select(i => i.ArticleType)));
            }
        }
        catch (Exception ex)
        {
            sample.SetStatus(AssayStatus.Failed);
            sample.SetErrorMessage($"Inventory check failed: {ex.Message}");
            _logger.LogError(ex, "Inventory check failed for sample {SampleId}", sample.Id);
        }
    }
}
```

#### 4.3.2 Execution Management
```csharp
public async Task<bool> StartExecution()
{
    if (_executionTask != null && !_executionTask.IsCompleted)
    {
        _logger.LogWarning("Execution already in progress");
        return false;
    }
    
    _cancellationTokenSource = new CancellationTokenSource();
    _flrAssayRunContext = await _flrContextFactory.CreateAssayRunContext();
    
    _executionTask = Task.Run(async () => await ExecuteAssaySamples(_cancellationTokenSource.Token));
    
    return true;
}

private async Task ExecuteAssaySamples(CancellationToken cancellationToken)
{
    try
    {
        await _flrAssayRunContext.BeginAssayRun();
        
        while (!cancellationToken.IsCancellationRequested)
        {
            var readySamples = GetReadyForExecution();
            
            if (!readySamples.Any())
            {
                await Task.Delay(1000, cancellationToken); // Wait for samples
                continue;
            }
            
            var executionPlan = await _executionPlanner.CreateExecutionPlan(readySamples);
            
            await ProcessExecutionPlan(executionPlan, cancellationToken);
        }
    }
    catch (OperationCanceledException)
    {
        _logger.LogInformation("Execution cancelled");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Execution failed");
    }
    finally
    {
        await _flrAssayRunContext.EndAssayRun();
    }
}

private async Task ProcessExecutionPlan(AssayExecutionPlan plan, CancellationToken cancellationToken)
{
    foreach (var step in plan.ExecutionSteps)
    {
        if (cancellationToken.IsCancellationRequested) break;
        
        await ProcessExecutionStep(step, cancellationToken);
    }
}

private async Task ProcessExecutionStep(AssayExecutionStep step, CancellationToken cancellationToken)
{
    var tasks = step.AssaySamples.Select(sample => ExecuteAssaySample(sample, cancellationToken));
    
    await Task.WhenAll(tasks);
}

private async Task ExecuteAssaySample(AssaySample sample, CancellationToken cancellationToken)
{
    try
    {
        sample.SetStatus(AssayStatus.InProgress);
        
        // Create FLR context for this sample
        var flrContext = await _flrAssayRunContext.CreateSampleTestContext(sample);
        sample.SetFlrContext(flrContext);
        
        // Create and execute sequence group
        var sequenceGroup = sample.CreateSequenceGroup();
        await _sequenceGroupManager.AddSequenceGroup(sequenceGroup);
        await _sequenceGroupManager.ExecuteSequenceGroup(sequenceGroup.Id);
        
        // Wait for completion
        await WaitForSequenceGroupCompletion(sequenceGroup.Id, cancellationToken);
        
        sample.SetStatus(AssayStatus.Completed);
        
        // Report results to FLR
        await flrContext.ReportResults(sample.Results);
        
        // Release inventory
        var requirements = sample.ValidateInventoryRequirements();
        await _inventoryService.ReleaseInventory(requirements);
        
        _logger.LogInformation("Successfully executed sample {SampleId}", sample.Id);
    }
    catch (Exception ex)
    {
        sample.SetStatus(AssayStatus.Failed);
        sample.SetErrorMessage(ex.Message);
        _logger.LogError(ex, "Failed to execute sample {SampleId}", sample.Id);
        
        // Ensure inventory is released on failure
        try
        {
            var requirements = sample.ValidateInventoryRequirements();
            await _inventoryService.ReleaseInventory(requirements);
        }
        catch (Exception releaseEx)
        {
            _logger.LogError(releaseEx, "Failed to release inventory for failed sample {SampleId}", sample.Id);
        }
    }
}
```

#### 4.3.3 Event Handling
```csharp
private void HandleAssaySampleStatusChanged(object sender, AssayStatusChangedEventArgs e)
{
    var sample = sender as AssaySample;
    
    _logger.LogInformation("Sample {SampleId} status changed from {OldStatus} to {NewStatus}", 
        sample.Id, e.OldStatus, e.NewStatus);
    
    switch (e.NewStatus)
    {
        case AssayStatus.Completed:
            OnAssaySampleCompleted(sample);
            break;
        case AssayStatus.Failed:
            OnAssaySampleFailed(sample);
            break;
        case AssayStatus.Cancelled:
            OnAssaySampleCancelled(sample);
            break;
    }
}

private void OnAssaySampleCompleted(AssaySample sample)
{
    // Schedule for removal after a delay to allow result retrieval
    Task.Delay(TimeSpan.FromMinutes(5)).ContinueWith(_ => 
    {
        _assaySamples.TryRemove(sample.Id, out _);
        sample.OnStatusChanged -= HandleAssaySampleStatusChanged;
    });
}

public async Task HandleSequenceGroupCompletion(Guid sequenceGroupId, SequenceGroupResult result)
{
    var sample = _assaySamples.Values.FirstOrDefault(s => s.SequenceGroup?.Id == sequenceGroupId);
    
    if (sample == null)
    {
        _logger.LogWarning("No sample found for completed sequence group {SequenceGroupId}", sequenceGroupId);
        return;
    }
    
    if (result.IsSuccess)
    {
        // Convert sequence results to assay results
        var assayResults = ConvertSequenceResultsToAssayResults(result.SequenceResults);
        sample.AddResults(assayResults);
        
        sample.SetStatus(AssayStatus.Completed);
    }
    else
    {
        sample.SetStatus(AssayStatus.Failed);
        sample.SetErrorMessage(result.ErrorMessage);
    }
}
```

## 5. Integration Points

### 5.1 Inventory Service Integration
- **Availability Checking**: Validate required articles before execution
- **Reservation Management**: Reserve inventory during execution
- **Release Coordination**: Release inventory after completion/failure

### 5.2 Sequence Group Manager Integration
- **Hardware Coordination**: Convert samples to executable sequences
- **Execution Monitoring**: Track sequence execution progress
- **Result Processing**: Handle sequence completion and errors

### 5.3 FLR Integration
- **Context Management**: Create and manage FLR contexts
- **Data Reporting**: Report execution data and results
- **Error Handling**: Report execution errors and failures

### 5.4 CMR Service Integration
- **Sample Registration**: Receive parsed CMR samples
- **Status Reporting**: Provide execution status updates
- **Error Reporting**: Report execution errors and failures

## 6. Error Handling & Recovery

### 6.1 Error Categories
- **Validation Errors**: Invalid sample data or configuration
- **Inventory Errors**: Insufficient or unavailable inventory
- **Execution Errors**: Hardware or sequence execution failures
- **System Errors**: Infrastructure or service failures

### 6.2 Recovery Strategies
- **Retry Logic**: Automatic retry for transient failures
- **Compensation**: Rollback operations for partial failures
- **Graceful Degradation**: Continue execution with reduced functionality
- **Manual Intervention**: Pause execution for manual resolution

### 6.3 Monitoring & Alerting
- **Health Checks**: Regular system health monitoring
- **Performance Metrics**: Execution timing and throughput
- **Error Rates**: Track error frequencies and types
- **Resource Utilization**: Monitor inventory and hardware usage

## 7. Performance Considerations

### 7.1 Scalability
- **Concurrent Execution**: Support multiple simultaneous samples
- **Resource Pooling**: Efficient use of limited hardware resources
- **Load Balancing**: Distribute execution across available resources

### 7.2 Memory Management
- **Object Lifecycle**: Proper cleanup of completed samples
- **Memory Pools**: Reuse objects to reduce GC pressure
- **Streaming**: Process large datasets without loading entirely into memory

### 7.3 Throughput Optimization
- **Batch Processing**: Group similar operations for efficiency
- **Pipeline Optimization**: Minimize wait times between operations
- **Caching**: Cache frequently accessed data and configurations

## 8. Testing Strategy

### 8.1 Unit Testing
- **Domain Logic**: Test business rules and state transitions
- **Service Logic**: Test service orchestration and coordination
- **Error Handling**: Test error scenarios and recovery

### 8.2 Integration Testing
- **Service Integration**: Test interactions between services
- **Database Integration**: Test data persistence and retrieval
- **Hardware Integration**: Test hardware control and monitoring

### 8.3 Performance Testing
- **Load Testing**: Test under expected load conditions
- **Stress Testing**: Test beyond normal operating conditions
- **Endurance Testing**: Test long-running operations

## 9. Configuration & Deployment

### 9.1 Configuration Management
- **Environment-Specific**: Different configurations for dev/test/prod
- **Runtime Configuration**: Support for configuration changes without restart
- **Validation**: Validate configuration at startup

### 9.2 Dependency Injection
- **Service Registration**: Register all dependencies in DI container
- **Lifetime Management**: Appropriate lifetimes for different services
- **Testing Support**: Easy mocking and testing with DI

### 9.3 Monitoring & Logging
- **Structured Logging**: Use structured logging for better analysis
- **Correlation IDs**: Track requests across service boundaries
- **Metrics Collection**: Collect and export performance metrics

## 10. Future Enhancements

### 10.1 Advanced Scheduling
- **Predictive Scheduling**: Use historical data for better planning
- **Dynamic Priorities**: Adjust priorities based on business rules
- **Resource Optimization**: Advanced algorithms for resource allocation

### 10.2 Real-time Monitoring
- **Dashboard Integration**: Real-time execution monitoring
- **Alerting**: Proactive alerting for issues
- **Analytics**: Advanced analytics for performance optimization

### 10.3 Extensibility
- **Plugin Architecture**: Support for custom execution logic
- **Event Streaming**: Integration with event streaming platforms
- **API Enhancements**: RESTful APIs for external integration