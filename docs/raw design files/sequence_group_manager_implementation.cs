// SequenceGroupManager.cs - Core implementation
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

namespace Instrument.Scheduler.Components
{
    public class SequenceGroupManager : ISequenceGroupManager, IHostedService, IDisposable
    {
        private readonly ConcurrentDictionary<Guid, SequenceGroup> _sequenceGroups;
        private readonly ConcurrentDictionary<Guid, HardwareExecutionPlan> _executionPlans;
        private readonly IGrpcGateway _grpcGateway;
        private readonly ILogger<SequenceGroupManager> _logger;
        private readonly SemaphoreSlim _executionSemaphore;
        private readonly Timer _monitoringTimer;
        
        private CancellationTokenSource _cancellationTokenSource;
        private Task _eventProcessingTask;
        private volatile bool _isProcessingEvents;

        public event EventHandler<SequenceGroupCompletedEventArgs> OnSequenceGroupCompleted;
        public event EventHandler<SequenceGroupFailedEventArgs> OnSequenceGroupFailed;
        public event EventHandler<SequenceCompletedEventArgs> OnSequenceCompleted;

        public SequenceGroupManager(
            IGrpcGateway grpcGateway,
            ILogger<SequenceGroupManager> logger)
        {
            _sequenceGroups = new ConcurrentDictionary<Guid, SequenceGroup>();
            _executionPlans = new ConcurrentDictionary<Guid, HardwareExecutionPlan>();
            _grpcGateway = grpcGateway ?? throw new ArgumentNullException(nameof(grpcGateway));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _executionSemaphore = new SemaphoreSlim(1, 1);
            
            // Set up monitoring
            _monitoringTimer = new Timer(MonitorSequenceGroups, null, 
                TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }

        public async Task<bool> AddSequenceGroup(SequenceGroup sequenceGroup)
        {
            if (sequenceGroup == null) throw new ArgumentNullException(nameof(sequenceGroup));

            if (_sequenceGroups.TryAdd(sequenceGroup.Id, sequenceGroup))
            {
                sequenceGroup.Status = SequenceGroupStatus.Queued;
                _logger.LogInformation("Added SequenceGroup {SequenceGroupId} with {SequenceCount} sequences", 
                    sequenceGroup.Id, sequenceGroup.Sequences.Count);
                return true;
            }

            _logger.LogWarning("Failed to add SequenceGroup {SequenceGroupId} - already exists", sequenceGroup.Id);
            return false;
        }

        public async Task<bool> RemoveSequenceGroup(Guid sequenceGroupId)
        {
            if (_sequenceGroups.TryRemove(sequenceGroupId, out var sequenceGroup))
            {
                // Cancel if in progress
                if (sequenceGroup.Status == SequenceGroupStatus.InProgress)
                {
                    await CancelSequenceGroup(sequenceGroupId);
                }

                // Clean up execution plan
                _executionPlans.TryRemove(sequenceGroupId, out _);
                
                _logger.LogInformation("Removed SequenceGroup {SequenceGroupId}", sequenceGroupId);
                return true;
            }

            return false;
        }

        public async Task<ExecutionResult> ExecuteSequenceGroup(Guid sequenceGroupId)
        {
            if (!_sequenceGroups.TryGetValue(sequenceGroupId, out var sequenceGroup))
            {
                return new ExecutionResult(false) 
                { 
                    ErrorMessage = $"SequenceGroup {sequenceGroupId} not found" 
                };
            }

            await _executionSemaphore.WaitAsync();
            try
            {
                _logger.LogInformation("Starting execution of SequenceGroup {SequenceGroupId}", sequenceGroupId);
                
                // Validate sequence group
                var validationResult = sequenceGroup.ValidateConfiguration();
                if (!validationResult.IsValid)
                {
                    var error = $"Validation failed: {string.Join(", ", validationResult.Errors)}";
                    sequenceGroup.Status = SequenceGroupStatus.Failed;
                    return new ExecutionResult(false) { ErrorMessage = error };
                }

                // Create hardware execution plan
                var executionPlan = CreateHardwareExecutionPlan(new[] { sequenceGroup });
                if (!_executionPlans.TryAdd(sequenceGroup.Id, executionPlan))
                {
                    return new ExecutionResult(false) 
                    { 
                        ErrorMessage = "Failed to create execution plan" 
                    };
                }

                // Update status
                sequenceGroup.Status = SequenceGroupStatus.InProgress;
                sequenceGroup.StartedAt = DateTime.UtcNow;

                // Execute sequences
                var result = await ExecuteHardwarePlan(executionPlan);
                
                // Update final status
                if (result.IsSuccess)
                {
                    sequenceGroup.Status = SequenceGroupStatus.Completed;
                    sequenceGroup.CompletedAt = DateTime.UtcNow;
                    sequenceGroup.Results.AddRange(result.SequenceResults);
                    
                    OnSequenceGroupCompleted?.Invoke(this, 
                        new SequenceGroupCompletedEventArgs(sequenceGroupId, result));
                }
                else
                {
                    sequenceGroup.Status = SequenceGroupStatus.Failed;
                    sequenceGroup.CompletedAt = DateTime.UtcNow;
                    
                    OnSequenceGroupFailed?.Invoke(this,
                        new SequenceGroupFailedEventArgs(sequenceGroupId, result.ErrorMessage));
                }

                return result;
            }
            catch (Exception ex)
            {
                sequenceGroup.Status = SequenceGroupStatus.Failed;
                sequenceGroup.CompletedAt = DateTime.UtcNow;
                
                _logger.LogError(ex, "Failed to execute SequenceGroup {SequenceGroupId}", sequenceGroupId);
                
                return new ExecutionResult(false) 
                { 
                    ErrorMessage = $"Execution failed: {ex.Message}" 
                };
            }
            finally
            {
                _executionSemaphore.Release();
            }
        }

        public async Task<bool> CancelSequenceGroup(Guid sequenceGroupId)
        {
            if (!_sequenceGroups.TryGetValue(sequenceGroupId, out var sequenceGroup))
            {
                return false;
            }

            if (sequenceGroup.Status != SequenceGroupStatus.InProgress)
            {
                _logger.LogWarning("Cannot cancel SequenceGroup {SequenceGroupId} - not in progress", sequenceGroupId);
                return false;
            }

            try
            {
                // Cancel with hardware execution engine
                if (_executionPlans.TryGetValue(sequenceGroupId, out var plan))
                {
                    await _grpcGateway.CancelExecution(plan.Id.ToString());
                }

                sequenceGroup.Status = SequenceGroupStatus.Cancelled;
                sequenceGroup.CompletedAt = DateTime.UtcNow;
                
                _logger.LogInformation("Cancelled SequenceGroup {SequenceGroupId}", sequenceGroupId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cancel SequenceGroup {SequenceGroupId}", sequenceGroupId);
                return false;
            }
        }

        public SequenceGroup GetSequenceGroup(Guid sequenceGroupId)
        {
            _sequenceGroups.TryGetValue(sequenceGroupId, out var sequenceGroup);
            return sequenceGroup;
        }

        public IReadOnlyCollection<SequenceGroup> GetAllSequenceGroups()
        {
            return _sequenceGroups.Values.ToList().AsReadOnly();
        }

        public HardwareExecutionPlan CreateHardwareExecutionPlan(IEnumerable<SequenceGroup> sequenceGroups)
        {
            var allSequences = sequenceGroups.SelectMany(sg => sg.Sequences).ToList();
            var executionSteps = new List<SequenceExecutionStep>();
            
            // Group sequences by type and dependencies
            var sequencesByType = allSequences.GroupBy(s => s.Type).ToList();
            
            // Create execution steps with parallelization opportunities
            foreach (var typeGroup in sequencesByType.OrderBy(g => GetSequenceTypeOrder(g.Key)))
            {
                var sequences = typeGroup.ToList();
                var parallelGroups = GroupSequencesForParallelExecution(sequences);
                
                foreach (var parallelGroup in parallelGroups)
                {
                    var step = new SequenceExecutionStep(parallelGroup);
                    executionSteps.Add(step);
                }
            }

            var plan = new HardwareExecutionPlan(executionSteps);
            plan.OptimizeExecution();
            
            var validationResult = plan.ValidatePlan();
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Hardware execution plan validation warnings: {Warnings}", 
                    string.Join(", ", validationResult.Warnings));
            }

            _logger.LogInformation("Created hardware execution plan with {StepCount} steps for {SequenceCount} sequences", 
                executionSteps.Count, allSequences.Count);
            
            return plan;
        }

        public ExecutionStatus GetExecutionStatus()
        {
            var totalGroups = _sequenceGroups.Count;
            var statusCounts = _sequenceGroups.Values
                .GroupBy(sg => sg.Status)
                .ToDictionary(g => g.Key, g => g.Count());

            return new ExecutionStatus
            {
                TotalSequenceGroups = totalGroups,
                StatusDistribution = statusCounts,
                ActiveExecutionPlans = _executionPlans.Count,
                IsProcessingEvents = _isProcessingEvents
            };
        }

        private async Task<ExecutionResult> ExecuteHardwarePlan(HardwareExecutionPlan plan)
        {
            try
            {
                plan.Status = ExecutionPlanStatus.InProgress;
                
                // Send execution request to hardware
                var requestId = await _grpcGateway.SendExecutionRequest(plan);
                plan.Metadata["RequestId"] = requestId;
                
                // Wait for completion with timeout
                var timeout = plan.EstimatedDuration.Add(TimeSpan.FromMinutes(10)); // Add buffer
                var result = await WaitForPlanCompletion(plan, timeout);
                
                plan.Status = result.IsSuccess ? ExecutionPlanStatus.Completed : ExecutionPlanStatus.Failed;
                
                return result;
            }
            catch (Exception ex)
            {
                plan.Status = ExecutionPlanStatus.Failed;
                _logger.LogError(ex, "Hardware plan execution failed");
                
                return new ExecutionResult(false) 
                { 
                    ErrorMessage = $"Hardware execution failed: {ex.Message}" 
                };
            }
        }

        private async Task<ExecutionResult> WaitForPlanCompletion(HardwareExecutionPlan plan, TimeSpan timeout)
        {
            var completionSource = new TaskCompletionSource<ExecutionResult>();
            var sequenceResults = new List<SequenceResult>();
            var cancellationToken = new CancellationTokenSource(timeout).Token;

            try
            {
                // Subscribe to sequence events
                await foreach (var sequenceEvent in _grpcGateway.SubscribeToSequenceEvents().WithCancellation(cancellationToken))
                {
                    await ProcessSequenceEvent(sequenceEvent, sequenceResults, plan, completionSource);
                    
                    if (completionSource.Task.IsCompleted)
                        break;
                }
                
                return await completionSource.Task;
            }
            catch (OperationCanceledException)
            {
                return new ExecutionResult(false) 
                { 
                    ErrorMessage = "Execution timed out",
                    SequenceResults = sequenceResults
                };
            }
        }

        private async Task ProcessSequenceEvent(SequenceEvent sequenceEvent, List<SequenceResult> results, 
            HardwareExecutionPlan plan, TaskCompletionSource<ExecutionResult> completionSource)
        {
            switch (sequenceEvent.Type)
            {
                case SequenceEventType.Completed:
                    var result = new SequenceResult(sequenceEvent.SequenceId, sequenceEvent.AssayId)
                    {
                        Status = ResultStatus.Success,
                        Measurements = sequenceEvent.Data,
                        ExecutionTime = sequenceEvent.Duration
                    };
                    results.Add(result);
                    
                    OnSequenceCompleted?.Invoke(this, new SequenceCompletedEventArgs(sequenceEvent.SequenceId, result));
                    
                    // Check if all sequences completed
                    if (AreAllSequencesCompleted(plan, results))
                    {
                        completionSource.SetResult(new ExecutionResult(true) { SequenceResults = results });
                    }
                    break;
                    
                case SequenceEventType.Failed:
                    var failedResult = new SequenceResult(sequenceEvent.SequenceId, sequenceEvent.AssayId)
                    {
                        Status = ResultStatus.Failed,
                        Notes = sequenceEvent.ErrorMessage
                    };
                    results.Add(failedResult);
                    
                    // Fail the entire plan on any sequence failure
                    completionSource.SetResult(new ExecutionResult(false) 
                    { 
                        ErrorMessage = $"Sequence {sequenceEvent.SequenceId} failed: {sequenceEvent.ErrorMessage}",
                        SequenceResults = results
                    });
                    break;
                    
                case SequenceEventType.Progress:
                    // Update progress tracking
                    plan.GetProgress().UpdateProgress(sequenceEvent.ProgressData);
                    break;
            }
        }

        private bool AreAllSequencesCompleted(HardwareExecutionPlan plan, List<SequenceResult> results)
        {
            var totalSequences = plan.ExecutionSteps.SelectMany(step => 
                step.ParallelSequences.Concat(step.SerialSequences)).Count();
            
            var completedSequences = results.Count(r => r.Status == ResultStatus.Success);
            
            return completedSequences >= totalSequences;
        }

        private List<List<Sequence>> GroupSequencesForParallelExecution(List<Sequence> sequences)
        {
            var groups = new List<List<Sequence>>();
            var remaining = sequences.ToList();

            while (remaining.Any())
            {
                var parallelGroup = new List<Sequence> { remaining.First() };
                remaining.RemoveAt(0);

                // Find sequences that can run in parallel with the first
                for (int i = remaining.Count - 1; i >= 0; i--)
                {
                    var candidate = remaining[i];
                    if (parallelGroup.All(s => s.CanRunInParallel(candidate)))
                    {
                        parallelGroup.Add(candidate);
                        remaining.RemoveAt(i);
                    }
                }

                groups.Add(parallelGroup);
            }

            return groups;
        }

        private int GetSequenceTypeOrder(SequenceType sequenceType)
        {
            return sequenceType switch
            {
                SequenceType.Initialization => 0,
                SequenceType.SamplePreparation => 1,
                SequenceType.Reaction => 2,
                SequenceType.Detection => 3,
                SequenceType.Cleanup => 4,
                SequenceType.Validation => 5,
                SequenceType.Maintenance => 6,
                _ => 999
            };
        }

        private void MonitorSequenceGroups(object state)
        {
            try
            {
                var statusCounts = _sequenceGroups.Values
                    .GroupBy(sg => sg.Status)
                    .ToDictionary(g => g.Key, g => g.Count());

                _logger.LogInformation("SequenceGroup status: {StatusCounts}",
                    string.Join(", ", statusCounts.Select(kvp => $"{kvp.Key}:{kvp.Value}")));

                // Check for stuck sequence groups
                var stuckGroups = _sequenceGroups.Values
                    .Where(sg => sg.Status == SequenceGroupStatus.InProgress &&
                                sg.StartedAt.HasValue &&
                                DateTime.UtcNow - sg.StartedAt.Value > TimeSpan.FromHours(1))
                    .ToList();

                if (stuckGroups.Any())
                {
                    _logger.LogWarning("Found {Count} potentially stuck sequence groups", stuckGroups.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in sequence group monitoring");
            }
        }

        // IHostedService implementation
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("SequenceGroupManager service starting");
            
            // Connect to hardware gateway
            var connected = await _grpcGateway.ConnectToHardwareEngine();
            if (!connected)
            {
                _logger.LogError("Failed to connect to Hardware Execution Engine");
                throw new InvalidOperationException("Cannot start without hardware connection");
            }

            // Start event processing
            _cancellationTokenSource = new CancellationTokenSource();
            _eventProcessingTask = Task.Run(async () => await ProcessEvents(_cancellationTokenSource.Token));
            _isProcessingEvents = true;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("SequenceGroupManager service stopping");
            
            _cancellationTokenSource?.Cancel();
            _isProcessingEvents = false;
            
            // Cancel all in-progress sequence groups
            var inProgressGroups = _sequenceGroups.Values
                .Where(sg => sg.Status == SequenceGroupStatus.InProgress)
                .ToList();

            foreach (var group in inProgressGroups)
            {
                await CancelSequenceGroup(group.Id);
            }

            if (_eventProcessingTask != null)
            {
                await _eventProcessingTask;
            }
        }

        private async Task ProcessEvents(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Additional event processing logic would go here
                    await Task.Delay(1000, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in event processing");
                    await Task.Delay(5000, cancellationToken); // Back off on error
                }
            }
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _eventProcessingTask?.Wait(TimeSpan.FromSeconds(30));
            
            _monitoringTimer?.Dispose();
            _executionSemaphore?.Dispose();
            _cancellationTokenSource?.Dispose();
            
            _sequenceGroups.Clear();
            _executionPlans.Clear();
        }
    }

    // Supporting classes and interfaces
    public interface ISequenceGroupManager
    {
        Task<bool> AddSequenceGroup(SequenceGroup sequenceGroup);
        Task<bool> RemoveSequenceGroup(Guid sequenceGroupId);
        Task<ExecutionResult> ExecuteSequenceGroup(Guid sequenceGroupId);
        Task<bool> CancelSequenceGroup(Guid sequenceGroupId);
        SequenceGroup GetSequenceGroup(Guid sequenceGroupId);
        IReadOnlyCollection<SequenceGroup> GetAllSequenceGroups();
        HardwareExecutionPlan CreateHardwareExecutionPlan(IEnumerable<SequenceGroup> sequenceGroups);
        ExecutionStatus GetExecutionStatus();
        
        event EventHandler<SequenceGroupCompletedEventArgs> OnSequenceGroupCompleted;
        event EventHandler<SequenceGroupFailedEventArgs> OnSequenceGroupFailed;
        event EventHandler<SequenceCompletedEventArgs> OnSequenceCompleted;
    }

    public class SequenceGroupCompletedEventArgs : EventArgs
    {
        public Guid SequenceGroupId { get; }
        public ExecutionResult Result { get; }
        public DateTime CompletedAt { get; }

        public SequenceGroupCompletedEventArgs(Guid sequenceGroupId, ExecutionResult result)
        {
            SequenceGroupId = sequenceGroupId;
            Result = result;
            CompletedAt = DateTime.UtcNow;
        }
    }

    public class SequenceGroupFailedEventArgs : EventArgs
    {
        public Guid SequenceGroupId { get; }
        public string ErrorMessage { get; }
        public DateTime FailedAt { get; }

        public SequenceGroupFailedEventArgs(Guid sequenceGroupId, string errorMessage)
        {
            SequenceGroupId = sequenceGroupId;
            ErrorMessage = errorMessage;
            FailedAt = DateTime.UtcNow;
        }
    }

    public class SequenceCompletedEventArgs : EventArgs
    {
        public Guid SequenceId { get; }
        public SequenceResult Result { get; }

        public SequenceCompletedEventArgs(Guid sequenceId, SequenceResult result)
        {
            SequenceId = sequenceId;
            Result = result;
        }
    }

    public class ExecutionStatus
    {
        public int TotalSequenceGroups { get; set; }
        public Dictionary<SequenceGroupStatus, int> StatusDistribution { get; set; }
        public int ActiveExecutionPlans { get; set; }
        public bool IsProcessingEvents { get; set; }
    }
}