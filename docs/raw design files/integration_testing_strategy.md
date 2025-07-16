# Scheduler Application Integration Testing Strategy

## Overview

This document outlines the comprehensive integration testing strategy for the Scheduler Application. The strategy covers testing approaches, test scenarios, automation frameworks, and quality gates to ensure robust system integration and reliability.

## Testing Pyramid Structure

### Unit Tests (70%)
- Individual component behavior validation
- Domain logic verification
- State transition testing
- Error handling scenarios

### Integration Tests (20%)
- Service-to-service communication
- Database integration
- External system integration
- Event flow validation

### End-to-End Tests (10%)
- Complete workflow validation
- User scenario testing
- Performance validation
- Cross-system integration

## Integration Test Categories

### 1. Service Integration Tests

#### AssayManager Integration
```csharp
[TestClass]
public class AssayManagerIntegrationTests
{
    [TestMethod]
    public async Task Should_ProcessCmrWorkflow_EndToEnd()
    {
        // Arrange
        var cmrFile = CreateTestCmrFile();
        var assayManager = CreateAssayManager();
        
        // Act
        var samples = await CreateAssaySamplesFromCmr(cmrFile);
        var addResult = await assayManager.AddAssaySamples(samples);
        var executionResult = await assayManager.StartExecution();
        
        // Assert
        Assert.IsTrue(addResult);
        Assert.IsTrue(executionResult);
        
        // Verify state transitions
        await WaitForCompletion();
        var completedSamples = assayManager.GetAssaySamplesByStatus(AssayStatus.Completed);
        Assert.AreEqual(samples.Count, completedSamples.Count);
    }
}
```

#### SequenceGroupManager Integration
```csharp
[TestMethod]
public async Task Should_ExecuteSequenceGroups_WithHardwareCoordination()
{
    // Test sequence group execution with mocked hardware
    var sequenceGroup = CreateTestSequenceGroup();
    var mockGrpcGateway = CreateMockGrpcGateway();
    
    var manager = new SequenceGroupManager(mockGrpcGateway, logger);
    await manager.AddSequenceGroup(sequenceGroup);
    
    var result = await manager.ExecuteSequenceGroup(sequenceGroup.Id);
    
    Assert.IsTrue(result.IsSuccess);
    Assert.AreEqual(sequenceGroup.Sequences.Count, result.SequenceResults.Count);
}
```

### 2. Database Integration Tests

#### Configuration Persistence
```csharp
[TestMethod]
public async Task Should_PersistAndRetrieveConfigurations()
{
    var configManager = CreateConfigurationManager();
    
    // Set various configuration types
    await configManager.SetConfiguration("TestString", "value");
    await configManager.SetConfiguration("TestInt", 42);
    await configManager.SetConfiguration("TestBool", true);
    
    // Reload from database
    await configManager.ReloadConfiguration();
    
    // Verify persistence
    Assert.AreEqual("value", configManager.GetConfiguration<string>("TestString"));
    Assert.AreEqual(42, configManager.GetConfiguration<int>("TestInt"));
    Assert.IsTrue(configManager.GetConfiguration<bool>("TestBool"));
}
```

#### Inventory Data Integrity
```csharp
[TestMethod]
public async Task Should_MaintainInventoryConsistency_UnderConcurrentAccess()
{
    var inventoryService = CreateInventoryService();
    var article = CreateTestArticle("TestArticle", quantity: 100);
    
    await inventoryService.UpdateInventory(article, 0);
    
    // Simulate concurrent reservations
    var tasks = Enumerable.Range(0, 10)
        .Select(i => inventoryService.ReserveInventory(
            new List<InventoryRequirement> 
            { 
                new("Cartridge", "TestArticle", 5) 
            }))
        .ToArray();
    
    var results = await Task.WhenAll(tasks);
    
    // Verify correct number of successful reservations
    var successCount = results.Count(r => r);
    Assert.AreEqual(10, successCount); // 10 reservations of 5 each = 50 total
    
    var status = inventoryService.GetInventoryStatus();
    Assert.AreEqual(50, status.ReservedQuantity);
}
```

### 3. Event-Driven Integration Tests

#### State Machine Event Handling
```csharp
[TestMethod]
public async Task Should_HandleStateTransitions_BasedOnHalEvents()
{
    var stateManager = CreateStateManager();
    var eventService = CreateHalEventService();
    
    // Initial state should be Initialized
    Assert.AreEqual(EnvironmentState.Initialized, stateManager.GetCurrentState().State);
    
    // Send steady state event
    await eventService.PublishEvent(new HalEvent 
    { 
        Type = HalEventType.SteadyState, 
        Message = "System ready" 
    });
    
    await Task.Delay(100); // Allow event processing
    
    Assert.AreEqual(EnvironmentState.SteadyState, stateManager.GetCurrentState().State);
    Assert.IsTrue(stateManager.IsProcessingAllowed());
}
```

#### Cross-Service Event Flow
```csharp
[TestMethod]
public async Task Should_PropagateEvents_AcrossServices()
{
    var assayManager = CreateAssayManager();
    var inventoryService = CreateInventoryService();
    var eventBus = CreateEventBus();
    
    var eventReceived = false;
    inventoryService.OnInventoryChanged += (s, e) => eventReceived = true;
    
    // Create sample that requires inventory
    var sample = CreateTestAssaySample();
    await assayManager.AddAssaySamples(new[] { sample });
    
    await Task.Delay(500); // Allow async processing
    
    Assert.IsTrue(eventReceived, "Inventory change event should be triggered");
}
```

### 4. External System Integration Tests

#### gRPC Communication Tests
```csharp
[TestMethod]
public async Task Should_CommunicateWithHardwareEngine_ViaGrpc()
{
    var grpcGateway = CreateGrpcGateway();
    var testServer = CreateTestGrpcServer();
    
    try
    {
        await testServer.StartAsync();
        var connected = await grpcGateway.ConnectToHardwareEngine();
        Assert.IsTrue(connected);
        
        var executionPlan = CreateTestExecutionPlan();
        var requestId = await grpcGateway.SendExecutionRequest(executionPlan);
        Assert.IsNotNull(requestId);
        
        // Verify request received by server
        var serverRequests = testServer.GetReceivedRequests();
        Assert.AreEqual(1, serverRequests.Count);
    }
    finally
    {
        await testServer.StopAsync();
    }
}
```

#### File System Integration
```csharp
[TestMethod]
public async Task Should_ProcessCmrFiles_FromFileSystem()
{
    var cmrService = CreateCmrService();
    var tempDirectory = CreateTempDirectory();
    
    try
    {
        // Create test CMR file
        var testFile = Path.Combine(tempDirectory, "test.csv");
        await File.WriteAllTextAsync(testFile, CreateTestCmrContent());
        
        var parseResult = await cmrService.ParseCmrFile(testFile);
        
        Assert.IsTrue(parseResult.IsSuccess);
        Assert.IsNotNull(parseResult.CmrFile);
        Assert.IsTrue(parseResult.CmrFile.TestOrders.Any());
    }
    finally
    {
        Directory.Delete(tempDirectory, true);
    }
}
```

### 5. Performance Integration Tests

#### Load Testing
```csharp
[TestMethod]
public async Task Should_HandleConcurrentSamples_WithinPerformanceLimits()
{
    var assayManager = CreateAssayManager();
    var samples = CreateTestSamples(50); // 50 concurrent samples
    
    var stopwatch = Stopwatch.StartNew();
    
    var addResult = await assayManager.AddAssaySamples(samples);
    var executionResult = await assayManager.StartExecution();
    
    await WaitForAllSamplesToComplete(assayManager, TimeSpan.FromMinutes(30));
    
    stopwatch.Stop();
    
    Assert.IsTrue(addResult);
    Assert.IsTrue(executionResult);
    Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromMinutes(25), 
        $"Execution took too long: {stopwatch.Elapsed}");
    
    var completedSamples = assayManager.GetAssaySamplesByStatus(AssayStatus.Completed);
    Assert.AreEqual(50, completedSamples.Count);
}
```

#### Memory Usage Testing
```csharp
[TestMethod]
public async Task Should_MaintainMemoryUsage_WithinAcceptableLimits()
{
    var initialMemory = GC.GetTotalMemory(true);
    var assayManager = CreateAssayManager();
    
    // Process multiple batches
    for (int batch = 0; batch < 10; batch++)
    {
        var samples = CreateTestSamples(20);
        await assayManager.AddAssaySamples(samples);
        await ProcessSamples(assayManager);
        
        // Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
    
    var finalMemory = GC.GetTotalMemory(true);
    var memoryIncrease = finalMemory - initialMemory;
    
    // Memory increase should be less than 100MB for 200 samples
    Assert.IsTrue(memoryIncrease < 100 * 1024 * 1024, 
        $"Memory usage increased by {memoryIncrease / (1024 * 1024)}MB");
}
```

## Test Data Management

### Test Data Builder Patterns
```csharp
public class AssaySampleBuilder
{
    private Sample _sample = new Sample("Test_Sample", SampleType.Sample);
    private List<Assay> _assays = new();
    private int _priority = 0;
    
    public AssaySampleBuilder WithSample(string id, SampleType type)
    {
        _sample = new Sample(id, type);
        return this;
    }
    
    public AssaySampleBuilder WithAssay(string name, Technology technology)
    {
        _assays.Add(new Assay(_assays.Count + 1, name, "TestMethod") 
        { 
            Technology = technology 
        });
        return this;
    }
    
    public AssaySampleBuilder WithPriority(int priority)
    {
        _priority = priority;
        return this;
    }
    
    public AssaySample Build()
    {
        return new AssaySample(_sample, _assays, _priority);
    }
}
```

### Database Seeding
```csharp
public class TestDataSeeder
{
    public async Task SeedTestData(IServiceProvider services)
    {
        var inventoryService = services.GetService<IInventoryService>();
        var configManager = services.GetService<IConfigurationManager>();
        
        // Seed inventory
        var articles = CreateTestInventoryArticles();
        foreach (var article in articles)
        {
            await inventoryService.UpdateInventory(article, article.Quantity);
        }
        
        // Seed configuration
        var configs = CreateTestConfigurations();
        foreach (var config in configs)
        {
            await configManager.SetConfiguration(config.Key, config.Value);
        }
    }
}
```

## Test Environment Setup

### Docker Compose for Integration Testing
```yaml
version: '3.8'
services:
  scheduler-test:
    build: .
    environment:
      - ASPNETCORE_ENVIRONMENT=Testing
      - ConnectionStrings__Default=Server=test-db;Database=SchedulerTest;
    depends_on:
      - test-db
      - test-grpc-server
    
  test-db:
    image: mcr.microsoft.com/mssql/server:2019-latest
    environment:
      - ACCEPT_EULA=Y
      - SA_PASSWORD=TestPassword123!
    
  test-grpc-server:
    build: ./test/MockHardwareServer
    ports:
      - "5000:5000"
```

### Test Configuration
```json
{
  "Scheduler": {
    "MaxConcurrentSamples": 5,
    "ExecutionTimeoutMinutes": 30
  },
  "Database": {
    "ConnectionString": "Server=localhost;Database=SchedulerIntegrationTest;Trusted_Connection=true;"
  },
  "Grpc": {
    "HardwareEngineEndpoint": "http://localhost:5000"
  },
  "Testing": {
    "EnableMockHardware": true,
    "EnableTestDataSeeding": true,
    "FastModeEnabled": true
  }
}
```

## Continuous Integration Pipeline

### Build and Test Stages
```yaml
stages:
  - build
  - unit-tests
  - integration-tests
  - performance-tests
  - deploy

integration-tests:
  stage: integration-tests
  script:
    - docker-compose -f docker-compose.test.yml up -d
    - dotnet test --filter Category=Integration --logger trx
    - docker-compose -f docker-compose.test.yml down
  artifacts:
    reports:
      junit: "**/*.trx"
  coverage: '/Code coverage: \d+\.\d+/'
```

### Quality Gates
- Minimum 80% code coverage for integration tests
- All integration tests must pass
- Performance tests must complete within acceptable time limits
- Memory usage must not exceed thresholds
- No critical security vulnerabilities

## Test Reporting and Metrics

### Test Result Dashboard
- Test execution time trends
- Test failure rate by category
- Code coverage metrics
- Performance benchmark results
- Infrastructure health during testing

### Automated Notifications
- Failed test alerts to development team
- Performance regression notifications
- Weekly test summary reports
- Release readiness assessments

## Mock Services and Test Doubles

### Hardware Execution Engine Mock
```csharp
public class MockHardwareExecutionEngine : IHardwareExecutionEngine
{
    private readonly List<ExecutionRequest> _receivedRequests = new();
    
    public async Task<string> SubmitExecutionRequest(ExecutionPlan plan)
    {
        var requestId = Guid.NewGuid().ToString();
        _receivedRequests.Add(new ExecutionRequest(requestId, plan));
        
        // Simulate execution delay
        await Task.Delay(TimeSpan.FromSeconds(2));
        
        // Send completion events
        foreach (var sequence in plan.Sequences)
        {
            await SendSequenceCompletedEvent(sequence.Id, requestId);
        }
        
        return requestId;
    }
}
```

### FLR Service Stub
```csharp
public class StubFlrService : IFlrService
{
    private readonly List<FlrData> _reportedData = new();
    
    public async Task<bool> ReportAssayData(Guid assayId, FlrData data)
    {
        _reportedData.Add(data);
        return true;
    }
    
    public List<FlrData> GetReportedData() => _reportedData.ToList();
}
```

## Best Practices

### Test Isolation
- Each test should be independent and repeatable
- Use transaction rollback for database tests
- Clean up test data after each test
- Use separate test databases per test run

### Test Naming Conventions
- `Should_[ExpectedBehavior]_When_[Condition]`
- Use descriptive test method names
- Group related tests in nested classes
- Tag tests with appropriate categories

### Test Data Management
- Use builders for complex test objects
- Keep test data minimal and focused
- Use factories for common test scenarios
- Avoid hardcoded test data

### Assertion Guidelines
- Use specific assertions over generic ones
- Include meaningful assertion messages
- Test both positive and negative scenarios
- Verify all important state changes

## Troubleshooting Integration Tests

### Common Issues and Solutions

#### Database Connection Issues
- Verify connection strings in test configuration
- Ensure test database is accessible
- Check for port conflicts in containerized tests

#### Timing-Related Failures
- Use appropriate wait strategies
- Implement retry logic for flaky tests
- Avoid fixed delays, use condition-based waits

#### Resource Cleanup
- Implement proper disposal patterns
- Use using statements for disposable resources
- Clean up background services and timers

#### Mock Configuration
- Ensure mocks are properly configured
- Verify mock expectations are set correctly
- Use strict mocks to catch unexpected calls

## Conclusion

This integration testing strategy provides comprehensive coverage of the Scheduler Application's integration points. By following this strategy, teams can ensure robust system integration, catch integration issues early, and maintain high quality standards throughout the development lifecycle.

The combination of automated testing, proper test data management, and continuous integration provides a solid foundation for reliable system integration testing.