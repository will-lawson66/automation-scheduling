using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Instrument.Data.Exceptions;
using Instrument.Data.Services;
using Instrument.Data.Repository;
using Instrument.Data.Entities;
using Instrument.Data.Entities.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Instrument.Data.UT;

// ============================================================================
// ENHANCED EXCEPTION TESTS
// ============================================================================

public class EnhancedExceptionTests
{
    [Fact]
    public void GrpcGatewayException_ConstructsCorrectly_WithAllProperties()
    {
        // Arrange
        var serviceName = "TestService";
        var operationName = "TestOperation";
        var message = "Test gateway exception";
        var duration = TimeSpan.FromMilliseconds(500);
        var attemptCount = 3;
        var correlationId = Guid.NewGuid().ToString();

        // Act
        var exception = new GrpcGatewayException(serviceName, operationName, message, duration, attemptCount, null, correlationId);

        // Assert
        Assert.Equal(serviceName, exception.ServiceName);
        Assert.Equal(operationName, exception.OperationName);
        Assert.Equal(message, exception.Message);
        Assert.Equal(duration, exception.Duration);
        Assert.Equal(attemptCount, exception.AttemptCount);
        Assert.Equal(correlationId, exception.CorrelationId);
        Assert.True(exception.Timestamp <= DateTime.UtcNow);
    }

    [Fact]
    public void GrpcTimeoutException_ConstructsCorrectly_WithTimeoutInformation()
    {
        // Arrange
        var serviceName = "TestService";
        var operationName = "TestOperation";
        var configuredTimeout = TimeSpan.FromSeconds(30);
        var actualDuration = TimeSpan.FromSeconds(35);
        var isOperationTimeout = true;

        // Act
        var exception = new GrpcTimeoutException(serviceName, operationName, configuredTimeout, actualDuration, isOperationTimeout);

        // Assert
        Assert.Equal(serviceName, exception.ServiceName);
        Assert.Equal(operationName, exception.OperationName);
        Assert.Equal(configuredTimeout, exception.ConfiguredTimeout);
        Assert.Equal(actualDuration, exception.Duration);
        Assert.Equal(isOperationTimeout, exception.IsOperationTimeout);
        Assert.Contains("timed out", exception.Message);
        Assert.Contains("30000ms", exception.Message); // Configured timeout in message
        Assert.Contains("35000ms", exception.Message); // Actual duration in message
    }

    [Fact]
    public void OrchestrationStepException_ConstructsCorrectly_WithStepInformation()
    {
        // Arrange
        var stepName = "ValidateRequest";
        var stepOrder = 1;
        var message = "Step validation failed";
        var shouldContinue = false;
        var stepDuration = TimeSpan.FromMilliseconds(100);
        var workflowName = "ConfigurationImport";
        var completedSteps = new List<string> { "InitializeWorkflow" };
        var contextData = new Dictionary<string, object?> { ["UserId"] = 123 };

        // Act
        var exception = new OrchestrationStepException(
            stepName, stepOrder, message, shouldContinue, stepDuration,
            workflowName, completedSteps, contextData);

        // Assert
        Assert.Equal(stepName, exception.StepName);
        Assert.Equal(stepOrder, exception.StepOrder);
        Assert.Equal(message, exception.Message);
        Assert.Equal(shouldContinue, exception.ShouldContinue);
        Assert.Equal(stepDuration, exception.StepDuration);
        Assert.Equal(workflowName, exception.WorkflowName);
        Assert.Equal(completedSteps, exception.CompletedSteps);
        Assert.Equal(contextData, exception.ContextData);
    }

    [Fact]
    public void RetryPolicyException_ConstructsCorrectly_WithRetryInformation()
    {
        // Arrange
        var policyType = "ExponentialBackoff";
        var maxAttempts = 3;
        var actualAttempts = 3;
        var totalDuration = TimeSpan.FromSeconds(7);
        var attemptExceptions = new List<Exception>
        {
            new InvalidOperationException("First attempt"),
            new TimeoutException("Second attempt"),
            new OperationCanceledException("Third attempt")
        };

        // Act
        var exception = new RetryPolicyException(policyType, maxAttempts, actualAttempts, totalDuration, attemptExceptions);

        // Assert
        Assert.Equal(policyType, exception.PolicyType);
        Assert.Equal(maxAttempts, exception.MaxAttempts);
        Assert.Equal(actualAttempts, exception.ActualAttempts);
        Assert.Equal(totalDuration, exception.TotalDuration);
        Assert.Equal(attemptExceptions, exception.AttemptExceptions);
        Assert.Equal(attemptExceptions.Last(), exception.InnerException);
        Assert.Contains("exhausted", exception.Message);
    }

    [Fact]
    public void ExceptionExtensions_ToStructuredData_ReturnsCorrectData()
    {
        // Arrange
        var exception = new GrpcTimeoutException("TestService", "TestOperation", 
            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(35), true);

        // Act
        var structuredData = exception.ToStructuredData();

        // Assert
        Assert.Equal("GrpcTimeoutException", structuredData["ExceptionType"]);
        Assert.Equal("TestService", structuredData["ServiceName"]);
        Assert.Equal("TestOperation", structuredData["OperationName"]);
        Assert.Equal(35000.0, structuredData["Duration"]);
        Assert.NotNull(structuredData["Timestamp"]);
    }

    [Theory]
    [InlineData(typeof(GrpcTimeoutException), true)]
    [InlineData(typeof(GrpcServiceUnavailableException), true)]
    [InlineData(typeof(TaskCanceledException), true)]
    [InlineData(typeof(TimeoutException), true)]
    [InlineData(typeof(ArgumentException), false)]
    [InlineData(typeof(InvalidOperationException), false)]
    public void ExceptionExtensions_IsTransient_ReturnsCorrectValue(Type exceptionType, bool expectedTransient)
    {
        // Arrange
        Exception exception = exceptionType.Name switch
        {
            nameof(GrpcTimeoutException) => new GrpcTimeoutException("Service", "Operation", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), true),
            nameof(GrpcServiceUnavailableException) => new GrpcServiceUnavailableException("Service", "Operation", TimeSpan.FromSeconds(1)),
            nameof(TaskCanceledException) => new TaskCanceledException(),
            nameof(TimeoutException) => new TimeoutException(),
            nameof(ArgumentException) => new ArgumentException(),
            nameof(InvalidOperationException) => new InvalidOperationException(),
            _ => throw new ArgumentException($"Unsupported exception type: {exceptionType}")
        };

        // Act
        var isTransient = exception.IsTransient();

        // Assert
        Assert.Equal(expectedTransient, isTransient);
    }
}

// ============================================================================
// CANCELLATION TOKEN SERVICE TESTS
// ============================================================================

public class CancellationTokenServiceTests : IDisposable
{
    private readonly Mock<IParameterRepository> _mockRepository;
    private readonly Mock<ILogger<ParameterService>> _mockLogger;
    private readonly Mock<IOptions<TimeoutOptions>> _mockOptions;
    private readonly ParameterService _service;
    private readonly TimeoutOptions _timeoutOptions;

    public CancellationTokenServiceTests()
    {
        _mockRepository = new Mock<IParameterRepository>();
        _mockLogger = new Mock<ILogger<ParameterService>>();
        _mockOptions = new Mock<IOptions<TimeoutOptions>>();
        
        _timeoutOptions = new TimeoutOptions
        {
            ServiceTimeout = TimeSpan.FromSeconds(5),
            DatabaseTimeout = TimeSpan.FromSeconds(3),
            ValidationTimeout = TimeSpan.FromSeconds(1)
        };
        
        _mockOptions.Setup(x => x.Value).Returns(_timeoutOptions);
        _service = new ParameterService(_mockRepository.Object, _mockLogger.Object, _mockOptions.Object);
    }

    public void Dispose()
    {
        _service?.Dispose();
    }

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
    public async Task CreateParameterAsync_WithTimeout_ThrowsGrpcTimeoutException()
    {
        // Arrange
        var parameter = new Parameter { Name = "TestParameter", Type = ParameterType.StringType };
        
        _mockRepository.Setup(x => x.AddAsync(parameter, It.IsAny<CancellationToken>()))
                      .Returns<Parameter, CancellationToken>(async (p, ct) =>
                      {
                          // Simulate operation that takes longer than timeout
                          await Task.Delay(TimeSpan.FromSeconds(10), ct);
                          return p;
                      });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<GrpcTimeoutException>(() => 
            _service.CreateParameterAsync(parameter));
            
        Assert.Equal("ParameterService", exception.ServiceName);
        Assert.Equal("CreateParameter", exception.OperationName);
        Assert.True(exception.IsOperationTimeout);
    }

    [Fact]
    public async Task UpdateParameterAsync_WithNonExistentParameter_ThrowsEntityNotFoundException()
    {
        // Arrange
        var parameter = new Parameter { Id = 999, Name = "NonExistent" };
        
        _mockRepository.Setup(x => x.GetByIdAsync(parameter.Id, It.IsAny<CancellationToken>()))
                      .ReturnsAsync((Parameter?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<EntityNotFoundException>(() => 
            _service.UpdateParameterAsync(parameter));
            
        Assert.Equal("Parameter", exception.EntityType);
        Assert.Equal(parameter.Id, exception.EntityId);
    }

    [Fact]
    public async Task ValidateParameterValueAsync_WithInvalidNumericValue_ThrowsValidationException()
    {
        // Arrange
        var parameter = new Parameter 
        { 
            Id = 1, 
            Name = "NumericParam", 
            Type = ParameterType.IntegerType,
            Min = "10",
            Max = "100"
        };
        var invalidValue = "5"; // Below minimum

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(() => 
            _service.ValidateParameterValueAsync(parameter, invalidValue));
            
        Assert.Equal(parameter.Id, exception.ParameterId);
        Assert.Equal(invalidValue, exception.Value);
        Assert.Contains("greater than or equal to 10", exception.Reason);
    }

    [Fact]
    public async Task ValidateParameterValueAsync_WithCancellation_PropagatesCancellation()
    {
        // Arrange
        var parameter = new Parameter { Id = 1, Type = ParameterType.StringType };
        var value = "test";
        var cancellationTokenSource = new CancellationTokenSource();
        
        // Cancel immediately
        cancellationTokenSource.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => 
            _service.ValidateParameterValueAsync(parameter, value, cancellationTokenSource.Token));
    }

    [Fact]
    public async Task ConcurrentOperations_WithCancellationTokens_HandleCorrectly()
    {
        // Arrange
        var parameters = Enumerable.Range(1, 10)
            .Select(i => new Parameter { Id = i, Name = $"Param{i}" })
            .ToList();

        foreach (var param in parameters)
        {
            _mockRepository.Setup(x => x.GetByIdAsync(param.Id, It.IsAny<CancellationToken>()))
                          .ReturnsAsync(param);
        }

        var cancellationTokenSource = new CancellationTokenSource();

        // Act
        var tasks = parameters.Select(p => 
            _service.GetParameterByIdAsync(p.Id, cancellationTokenSource.Token)).ToList();

        // Cancel some operations midway
        _ = Task.Delay(50).ContinueWith(_ => cancellationTokenSource.Cancel());

        var results = await Task.WhenAll(tasks.Select(async task =>
        {
            try
            {
                return await task;
            }
            catch (OperationCanceledException)
            {
                return null; // Expected for cancelled operations
            }
        }));

        // Assert
        // Some operations should complete, others should be cancelled
        Assert.True(results.Any(r => r != null)); // Some completed
        Assert.True(results.Any(r => r == null)); // Some cancelled
    }
}

// ============================================================================
// ORCHESTRATION CANCELLATION TESTS
// ============================================================================

public class OrchestrationCancellationTests
{
    [Fact]
    public async Task ProcessManager_WithCancellation_StopsGracefully()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<ConfigurationImportManager>>();
        var steps = new List<Mock<IOrchestrationStep>>();
        var cancellationTokenSource = new CancellationTokenSource();
        
        // Create steps that check for cancellation
        for (int i = 0; i < 5; i++)
        {
            var mockStep = new Mock<IOrchestrationStep>();
            mockStep.Setup(x => x.StepName).Returns($"Step{i + 1}");
            mockStep.Setup(x => x.ExecuteAsync(It.IsAny<OrchestrationContext>(), It.IsAny<CancellationToken>()))
                   .Returns<OrchestrationContext, CancellationToken>(async (ctx, ct) =>
                   {
                       // Simulate work that respects cancellation
                       await Task.Delay(100, ct);
                       return StepResult.Success();
                   });
            steps.Add(mockStep);
        }

        var manager = new ConfigurationImportManager(
            steps.Select(s => s.Object), 
            mockLogger.Object);
        
        var request = new ConfigurationImportRequest();

        // Act
        var processTask = manager.ExecuteAsync(request, cancellationTokenSource.Token);
        
        // Cancel after a short delay
        _ = Task.Delay(150).ContinueWith(_ => cancellationTokenSource.Cancel());

        var result = await processTask;

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Unexpected error", result.ErrorMessage);
        
        // Verify that not all steps were executed due to cancellation
        var executedSteps = steps.Count(s => 
            s.Invocations.Any(inv => inv.Method.Name == nameof(IOrchestrationStep.ExecuteAsync)));
        Assert.True(executedSteps < steps.Count);
    }

    [Fact]
    public async Task OrchestrationStep_WithLongRunningOperation_SupportsCancellation()
    {
        // Arrange
        var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        var step = new TestCancellableStep();
        var context = new OrchestrationContext();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => 
            step.ExecuteAsync(context, cancellationTokenSource.Token));
    }

    private class TestCancellableStep : IOrchestrationStep
    {
        public string StepName => "TestCancellableStep";

        public async Task<StepResult> ExecuteAsync(OrchestrationContext context, CancellationToken cancellationToken)
        {
            try
            {
                // Simulate long-running work with regular cancellation checks
                for (int i = 0; i < 100; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Delay(10, cancellationToken);
                }
                
                return StepResult.Success();
            }
            catch (OperationCanceledException)
            {
                // Log cancellation and rethrow
                throw;
            }
        }
    }
}

// ============================================================================
// GRPC GATEWAY CANCELLATION TESTS
// ============================================================================

public class GrpcGatewayCancellationTests : IDisposable
{
    private readonly Mock<IRetryPolicy> _mockRetryPolicy;
    private readonly Mock<ILogger<GrpcGateway>> _mockLogger;
    private readonly Mock<IExecutionConfigurationOperationFactory> _mockOperationFactory;
    private readonly GrpcGateway _gateway;

    public GrpcGatewayCancellationTests()
    {
        _mockRetryPolicy = new Mock<IRetryPolicy>();
        _mockLogger = new Mock<ILogger<GrpcGateway>>();
        _mockOperationFactory = new Mock<IExecutionConfigurationOperationFactory>();

        var options = new GrpcGatewayOptions
        {
            DefaultTimeoutSeconds = 30,
            MaxConcurrentRequests = 5
        };

        var optionsMock = new Mock<IOptions<GrpcGatewayOptions>>();
        optionsMock.Setup(x => x.Value).Returns(options);

        _gateway = new GrpcGateway(
            _mockRetryPolicy.Object,
            _mockLogger.Object,
            optionsMock.Object,
            _mockOperationFactory.Object
        );
    }

    public void Dispose()
    {
        _gateway?.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellationToken_PropagatesCancellation()
    {
        // Arrange
        var request = new TestRequest { Data = "test" };
        var mockOperation = new Mock<IGrpcOperation<TestRequest, TestResponse>>();
        var cancellationTokenSource = new CancellationTokenSource();

        mockOperation.Setup(x => x.ServiceName).Returns("TestService");
        mockOperation.Setup(x => x.OperationName).Returns("TestOperation");
        mockOperation.Setup(x => x.ExecuteAsync(request))
                    .Returns<TestRequest>(async req =>
                    {
                        await Task.Delay(1000, cancellationTokenSource.Token);
                        return new TestResponse { Result = "success" };
                    });

        _mockRetryPolicy.Setup(x => x.ExecuteAsync(It.IsAny<Func<CancellationToken, Task<TestResponse>>>(), It.IsAny<CancellationToken>()))
                      .Returns<Func<CancellationToken, Task<TestResponse>>, CancellationToken>((func, ct) => func(ct));

        // Act
        var executeTask = _gateway.ExecuteAsync(mockOperation.Object, request, cancellationTokenSource.Token);
        
        // Cancel after short delay
        cancellationTokenSource.CancelAfter(TimeSpan.FromMilliseconds(100));

        var result = await executeTask;

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("canceled", result.ErrorMessage?.ToLower() ?? "");
    }

    [Fact]
    public async Task ExecuteAsync_WithTimeoutAndCancellation_HandlesCompositeScenario()
    {
        // Arrange
        var request = new TestRequest { Data = "test" };
        var mockOperation = new Mock<IGrpcOperation<TestRequest, TestResponse>>();
        var cancellationTokenSource = new CancellationTokenSource();

        mockOperation.Setup(x => x.ServiceName).Returns("TestService");
        mockOperation.Setup(x => x.OperationName).Returns("TestOperation");
        mockOperation.Setup(x => x.Timeout).Returns(TimeSpan.FromSeconds(1)); // Short timeout
        mockOperation.Setup(x => x.ExecuteAsync(request))
                    .Returns<TestRequest>(async req =>
                    {
                        // Operation that takes longer than timeout
                        await Task.Delay(TimeSpan.FromSeconds(2), cancellationTokenSource.Token);
                        return new TestResponse { Result = "success" };
                    });

        _mockRetryPolicy.Setup(x => x.ExecuteAsync(It.IsAny<Func<CancellationToken, Task<TestResponse>>>(), It.IsAny<CancellationToken>()))
                      .Returns<Func<CancellationToken, Task<TestResponse>>, CancellationToken>((func, ct) => func(ct));

        // Act
        var result = await _gateway.ExecuteAsync(mockOperation.Object, request, cancellationTokenSource.Token);

        // Assert - Should timeout before user cancellation
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
    }

    // Test data classes
    public sealed class TestRequest
    {
        public string Data { get; set; } = string.Empty;
    }

    public sealed class TestResponse
    {
        public string Result { get; set; } = string.Empty;
    }
}
