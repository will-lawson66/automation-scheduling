// Enhanced Process Manager with Event-Driven Elements
// This shows how to evolve your current architecture while maintaining its strengths

namespace Instrument.Scheduler.Enhanced
{
    // Enhanced AssayManager with better event integration
    public class EnhancedAssayManager : IAssayManager, IDisposable
    {
        private readonly ConcurrentDictionary<Guid, AssaySample> _assaySamples;
        private readonly ISequenceGroupManager _sequenceGroupManager;
        private readonly IInventoryService _inventoryService;
        private readonly IFlrService _flrService;
        private readonly IEventBus _eventBus;
        private readonly ILogger<EnhancedAssayManager> _logger;
        private readonly AssayExecutionSaga _executionSaga;

        // Events for cross-cutting concerns and observability
        public event EventHandler<AssayLifecycleEvent> OnAssayLifecycleChanged;

        public EnhancedAssayManager(
            ISequenceGroupManager sequenceGroupManager,
            IInventoryService inventoryService,
            IFlrService flrService,
            IEventBus eventBus,
            ILogger<EnhancedAssayManager> logger)
        {
            _assaySamples = new ConcurrentDictionary<Guid, AssaySample>();
            _sequenceGroupManager = sequenceGroupManager;
            _inventoryService = inventoryService;
            _flrService = flrService;
            _eventBus = eventBus;
            _logger = logger;
            _executionSaga = new AssayExecutionSaga(eventBus, logger);
        }

        public async Task<bool> AddAssaySamples(IEnumerable<AssaySample> samples)
        {
            var sampleList = samples.ToList();
            var addedSamples = new List<AssaySample>();

            foreach (var sample in sampleList)
            {
                if (_assaySamples.TryAdd(sample.Id, sample))
                {
                    sample.OnStatusChanged += HandleAssayStatusChanged;
                    sample.SetStatus(AssayStatus.Queued);
                    addedSamples.Add(sample);

                    // Publish event for cross-cutting concerns (monitoring, audit, etc.)
                    await _eventBus.Publish(new AssayAdded
                    {
                        SampleId = sample.Id,
                        AssayCount = sample.Assays.Count,
                        Priority = sample.Priority,
                        EstimatedDuration = sample.GetEstimatedDuration(),
                        Timestamp = DateTime.UtcNow
                    });
                }
            }

            // Start async inventory check process
            if (addedSamples.Any())
            {
                _ = Task.Run(async () => await ProcessInventoryChecks(addedSamples));
            }

            return addedSamples.Count == sampleList.Count;
        }

        public async Task<ExecutionResult> ExecuteAssaySample(AssaySample sample)
        {
            // Use saga pattern for complex orchestration with compensation
            return await _executionSaga.ExecuteWithCompensation(sample, async (sample, compensations) =>
            {
                // Step 1: Update status and create contexts
                sample.SetStatus(AssayStatus.InProgress);
                compensations.Add(() => sample.SetStatus(AssayStatus.Failed));

                await PublishLifecycleEvent(sample, "ExecutionStarted");

                // Step 2: Create FLR context
                var flrContext = await _flrService.CreateSampleTestContext(sample);
                sample.SetFlrContext(flrContext);
                compensations.Add(() => flrContext?.Dispose());

                // Step 3: Create and execute sequence group
                var sequenceGroup = sample.CreateSequenceGroup();
                await _sequenceGroupManager.AddSequenceGroup(sequenceGroup);
                compensations.Add(() => _sequenceGroupManager.RemoveSequenceGroup(sequenceGroup.Id));

                // Step 4: Execute with hardware
                var executionResult = await _sequenceGroupManager.ExecuteSequenceGroup(sequenceGroup.Id);
                
                if (!executionResult.IsSuccess)
                {
                    throw new ExecutionException($"Hardware execution failed: {executionResult.ErrorMessage}");
                }

                // Step 5: Process results
                var assayResults = ConvertSequenceResults(executionResult.SequenceResults, sample);
                sample.AddResults(assayResults);

                // Step 6: Report to FLR
                await flrContext.ReportResults(assayResults);

                // Step 7: Complete successfully
                sample.SetStatus(AssayStatus.Completed);
                await PublishLifecycleEvent(sample, "ExecutionCompleted");

                return new ExecutionResult(true) { AssayResults = assayResults };
            });
        }

        // Process Manager: Orchestrates but delegates to appropriate services
        private async Task ProcessInventoryChecks(List<AssaySample> samples)
        {
            foreach (var sample in samples)
            {
                try
                {
                    await PublishLifecycleEvent(sample, "InventoryCheckStarted");

                    var requirements = sample.ValidateInventoryRequirements();
                    var availability = await _inventoryService.CheckAvailability(requirements);

                    if (availability.IsAvailable)
                    {
                        var reserved = await _inventoryService.ReserveInventory(requirements);
                        if (reserved)
                        {
                            sample.SetStatus(AssayStatus.InventoryReserved);
                            await PublishLifecycleEvent(sample, "InventoryReserved");
                        }
                        else
                        {
                            sample.SetStatus(AssayStatus.InventoryUnavailable);
                            await PublishLifecycleEvent(sample, "InventoryReservationFailed");
                        }
                    }
                    else
                    {
                        sample.SetStatus(AssayStatus.InventoryUnavailable);
                        await PublishLifecycleEvent(sample, "InventoryUnavailable", 
                            new { MissingItems = availability.MissingItems });
                    }
                }
                catch (Exception ex)
                {
                    sample.SetStatus(AssayStatus.Failed);
                    sample.SetErrorMessage($"Inventory check failed: {ex.Message}");
                    await PublishLifecycleEvent(sample, "InventoryCheckFailed", new { Error = ex.Message });
                }
            }
        }

        private async Task PublishLifecycleEvent(AssaySample sample, string eventType, object additionalData = null)
        {
            var lifecycleEvent = new AssayLifecycleEvent
            {
                SampleId = sample.Id,
                EventType = eventType,
                Status = sample.Status,
                Timestamp = DateTime.UtcNow,
                Metadata = sample.Metadata,
                AdditionalData = additionalData
            };

            OnAssayLifecycleChanged?.Invoke(this, lifecycleEvent);
            await _eventBus.Publish(lifecycleEvent);
        }

        private void HandleAssayStatusChanged(object sender, AssayStatusChangedEventArgs e)
        {
            _logger.LogInformation("Sample {SampleId} status: {OldStatus} → {NewStatus}", 
                e.SampleId, e.OldStatus, e.NewStatus);

            // Trigger cleanup for terminal states
            if (e.NewStatus.IsTerminal())
            {
                _ = Task.Run(async () => await HandleSampleCompletion(e.SampleId));
            }
        }

        private async Task HandleSampleCompletion(Guid sampleId)
        {
            if (_assaySamples.TryGetValue(sampleId, out var sample))
            {
                // Release inventory
                var requirements = sample.ValidateInventoryRequirements();
                await _inventoryService.ReleaseInventory(requirements);

                // Schedule for removal
                await Task.Delay(TimeSpan.FromMinutes(5));
                await RemoveAssaySample(sampleId);
            }
        }

        public void Dispose()
        {
            foreach (var sample in _assaySamples.Values)
            {
                sample.OnStatusChanged -= HandleAssayStatusChanged;
            }
            _assaySamples.Clear();
            _executionSaga?.Dispose();
        }
    }

    // Saga pattern for complex workflows with compensation
    public class AssayExecutionSaga : IDisposable
    {
        private readonly IEventBus _eventBus;
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<Guid, List<Func<Task>>> _compensations;

        public AssayExecutionSaga(IEventBus eventBus, ILogger logger)
        {
            _eventBus = eventBus;
            _logger = logger;
            _compensations = new ConcurrentDictionary<Guid, List<Func<Task>>>();
        }

        public async Task<T> ExecuteWithCompensation<T>(
            AssaySample sample, 
            Func<AssaySample, List<Func<Task>>, Task<T>> operation)
        {
            var compensations = new List<Func<Task>>();
            var sagaId = Guid.NewGuid();
            
            _compensations.TryAdd(sagaId, compensations);

            try
            {
                await _eventBus.Publish(new SagaStarted 
                { 
                    SagaId = sagaId, 
                    SampleId = sample.Id, 
                    Operation = "AssayExecution" 
                });

                var result = await operation(sample, compensations);

                await _eventBus.Publish(new SagaCompleted 
                { 
                    SagaId = sagaId, 
                    SampleId = sample.Id 
                });

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Saga {SagaId} failed for sample {SampleId}", sagaId, sample.Id);
                
                await _eventBus.Publish(new SagaFailed 
                { 
                    SagaId = sagaId, 
                    SampleId = sample.Id, 
                    Error = ex.Message 
                });

                await CompensateAll(compensations, sagaId);
                throw;
            }
            finally
            {
                _compensations.TryRemove(sagaId, out _);
            }
        }

        private async Task CompensateAll(List<Func<Task>> compensations, Guid sagaId)
        {
            await _eventBus.Publish(new SagaCompensationStarted { SagaId = sagaId });

            // Execute compensations in reverse order
            for (int i = compensations.Count - 1; i >= 0; i--)
            {
                try
                {
                    await compensations[i]();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Compensation {Index} failed for saga {SagaId}", i, sagaId);
                }
            }

            await _eventBus.Publish(new SagaCompensationCompleted { SagaId = sagaId });
        }

        public void Dispose()
        {
            // Clean up any remaining compensations
            foreach (var kvp in _compensations)
            {
                _ = Task.Run(async () => await CompensateAll(kvp.Value, kvp.Key));
            }
            _compensations.Clear();
        }
    }

    // Enhanced event system for observability and loose coupling
    public interface IEventBus
    {
        Task Publish<T>(T @event) where T : class;
        Task Subscribe<T>(Func<T, Task> handler) where T : class;
    }

    // Domain events for different concerns
    public class AssayLifecycleEvent
    {
        public Guid SampleId { get; set; }
        public string EventType { get; set; }
        public AssayStatus Status { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object> Metadata { get; set; }
        public object AdditionalData { get; set; }
    }

    public class AssayAdded
    {
        public Guid SampleId { get; set; }
        public int AssayCount { get; set; }
        public int Priority { get; set; }
        public TimeSpan EstimatedDuration { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class SagaStarted
    {
        public Guid SagaId { get; set; }
        public Guid SampleId { get; set; }
        public string Operation { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class SagaCompleted
    {
        public Guid SagaId { get; set; }
        public Guid SampleId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class SagaFailed
    {
        public Guid SagaId { get; set; }
        public Guid SampleId { get; set; }
        public string Error { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class SagaCompensationStarted
    {
        public Guid SagaId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class SagaCompensationCompleted
    {
        public Guid SagaId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    // Extension methods
    public static class AssayStatusExtensions
    {
        public static bool IsTerminal(this AssayStatus status)
        {
            return status is AssayStatus.Completed or AssayStatus.Failed or AssayStatus.Cancelled;
        }
    }
}

/*
Key Benefits of This Enhanced Process Manager Approach:

1. **Maintains Orchestration Control**: The AssayManager still orchestrates the flow
   but delegates specific responsibilities to appropriate services.

2. **Adds Event-Driven Observability**: Events are used for cross-cutting concerns
   like monitoring, auditing, and notifications without complicating the core flow.

3. **Implements Saga Pattern**: Complex operations have proper compensation logic
   for error recovery while maintaining transactional consistency.

4. **Balances Coupling**: Core business flow uses direct calls for consistency,
   while non-critical concerns use events for loose coupling.

5. **Enhances Testability**: Each component can be tested independently while
   the process manager orchestrates the integration.

This approach gives you the benefits of event-driven architecture for 
observability and loose coupling while maintaining the reliability and 
simplicity of the process manager pattern for your core business logic.
*/