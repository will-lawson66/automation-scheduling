// SchedulerStateManager.cs - State machine implementation for environment control
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Instrument.Scheduler.StateMachine
{
    public class SchedulerStateManager : ISchedulerStateManager, IHostedService, IDisposable
    {
        private readonly IHalEventService _halEventService;
        private readonly ILogger<SchedulerStateManager> _logger;
        private readonly SchedulerStateConfiguration _configuration;
        private readonly Timer _outOfRangeTimer;
        private readonly object _stateLock = new object();
        
        private SchedulerState _currentState;
        private CancellationTokenSource _cancellationTokenSource;
        private Task _eventProcessingTask;

        public event EventHandler<StateChangedEventArgs> OnStateChanged;
        public event EventHandler<TimerExpiredEventArgs> OnTimerExpired;
        public event EventHandler<ProcessingBlockedEventArgs> OnProcessingBlocked;

        public SchedulerStateManager(
            IHalEventService halEventService,
            IOptions<SchedulerStateConfiguration> configuration,
            ILogger<SchedulerStateManager> logger)
        {
            _halEventService = halEventService ?? throw new ArgumentNullException(nameof(halEventService));
            _configuration = configuration?.Value ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Initialize in the Initialized state
            _currentState = new SchedulerState(EnvironmentState.Initialized, "System startup");
            
            // Create timer but don't start it
            _outOfRangeTimer = new Timer(OnOutOfRangeTimerExpired, null, Timeout.Infinite, Timeout.Infinite);
            
            _logger.LogInformation("SchedulerStateManager initialized in {State} state", _currentState.State);
        }

        public SchedulerState GetCurrentState()
        {
            lock (_stateLock)
            {
                return new SchedulerState(_currentState.State, _currentState.Reason)
                {
                    StateEnteredAt = _currentState.StateEnteredAt,
                    TimerStartedAt = _currentState.TimerStartedAt,
                    StateData = new Dictionary<string, object>(_currentState.StateData)
                };
            }
        }

        public bool IsProcessingAllowed()
        {
            lock (_stateLock)
            {
                return _currentState.State == EnvironmentState.SteadyState;
            }
        }

        public async Task HandleHalEvent(HalEvent halEvent)
        {
            if (halEvent == null) throw new ArgumentNullException(nameof(halEvent));

            _logger.LogInformation("Received HAL event: {EventType} - {Message}", 
                halEvent.Type, halEvent.Message);

            lock (_stateLock)
            {
                var newState = DetermineStateTransition(halEvent);
                
                if (newState.HasValue && newState.Value != _currentState.State)
                {
                    TransitionToState(newState.Value, $"HAL Event: {halEvent.Message}");
                }
                else if (newState.HasValue)
                {
                    _logger.LogDebug("HAL event received but no state transition required: {EventType}", halEvent.Type);
                }
                else
                {
                    _logger.LogWarning("Unexpected HAL event in current state {CurrentState}: {EventType}", 
                        _currentState.State, halEvent.Type);
                }
            }
        }

        public bool ForceStateTransition(EnvironmentState newState, string reason = null)
        {
            lock (_stateLock)
            {
                if (!_currentState.CanTransitionTo(newState))
                {
                    _logger.LogWarning("Cannot force transition from {CurrentState} to {NewState} - invalid transition", 
                        _currentState.State, newState);
                    return false;
                }

                var transitionReason = reason ?? $"Forced transition to {newState}";
                TransitionToState(newState, transitionReason);
                
                _logger.LogWarning("Forced state transition to {NewState}: {Reason}", newState, transitionReason);
                return true;
            }
        }

        public async Task<bool> ResetSystem()
        {
            lock (_stateLock)
            {
                _logger.LogInformation("Resetting system to Initialized state");
                
                // Stop any active timers
                StopOutOfRangeTimer();
                
                // Reset to initialized state
                TransitionToState(EnvironmentState.Initialized, "System reset");
            }

            // Notify external systems of reset
            OnProcessingBlocked?.Invoke(this, new ProcessingBlockedEventArgs(
                ProcessingBlockReason.SystemReset, "System has been reset"));

            return true;
        }

        public TimeSpan? GetTimeRemainingInCurrentState()
        {
            lock (_stateLock)
            {
                return _currentState.GetTimeRemaining();
            }
        }

        public Dictionary<string, object> GetStateMetrics()
        {
            lock (_stateLock)
            {
                var metrics = new Dictionary<string, object>
                {
                    ["CurrentState"] = _currentState.State.ToString(),
                    ["TimeInCurrentState"] = _currentState.TimeInState,
                    ["StateEnteredAt"] = _currentState.StateEnteredAt,
                    ["IsTimerActive"] = _currentState.IsTimerActive(),
                    ["ProcessingAllowed"] = IsProcessingAllowed()
                };

                if (_currentState.TimerStartedAt.HasValue)
                {
                    metrics["TimerStartedAt"] = _currentState.TimerStartedAt.Value;
                    metrics["TimeRemainingOnTimer"] = GetTimeRemainingInCurrentState();
                }

                return metrics;
            }
        }

        private EnvironmentState? DetermineStateTransition(HalEvent halEvent)
        {
            return halEvent.Type switch
            {
                HalEventType.SteadyState => HandleSteadyStateEvent(),
                HalEventType.OutOfRange => HandleOutOfRangeEvent(),
                HalEventType.SystemError => HandleSystemErrorEvent(),
                HalEventType.SystemRecovery => HandleSystemRecoveryEvent(),
                _ => null
            };
        }

        private EnvironmentState? HandleSteadyStateEvent()
        {
            return _currentState.State switch
            {
                EnvironmentState.Initialized => EnvironmentState.SteadyState,
                EnvironmentState.OutOfRange => EnvironmentState.SteadyState,
                EnvironmentState.SteadyState => null, // Already in steady state
                EnvironmentState.InvalidTests => null, // Cannot transition from invalid tests without reset
                _ => null
            };
        }

        private EnvironmentState? HandleOutOfRangeEvent()
        {
            return _currentState.State switch
            {
                EnvironmentState.SteadyState => EnvironmentState.OutOfRange,
                EnvironmentState.Initialized => EnvironmentState.OutOfRange,
                EnvironmentState.OutOfRange => null, // Already out of range
                EnvironmentState.InvalidTests => null, // Cannot transition from invalid tests
                _ => null
            };
        }

        private EnvironmentState? HandleSystemErrorEvent()
        {
            // System errors can occur from any state
            return EnvironmentState.OutOfRange;
        }

        private EnvironmentState? HandleSystemRecoveryEvent()
        {
            return _currentState.State switch
            {
                EnvironmentState.OutOfRange => EnvironmentState.SteadyState,
                _ => null
            };
        }

        private void TransitionToState(EnvironmentState newState, string reason)
        {
            var oldState = _currentState.State;
            var previousStateData = new Dictionary<string, object>(_currentState.StateData);
            
            // Create new state
            _currentState = new SchedulerState(newState, reason);
            
            // Handle state-specific logic
            HandleStateEntry(newState, oldState, previousStateData);
            
            // Notify listeners
            OnStateChanged?.Invoke(this, new StateChangedEventArgs(oldState, newState, reason));
            
            _logger.LogInformation("State transition: {OldState} -> {NewState}. Reason: {Reason}", 
                oldState, newState, reason);
        }

        private void HandleStateEntry(EnvironmentState newState, EnvironmentState oldState, 
            Dictionary<string, object> previousStateData)
        {
            switch (newState)
            {
                case EnvironmentState.Initialized:
                    HandleInitializedEntry();
                    break;
                    
                case EnvironmentState.SteadyState:
                    HandleSteadyStateEntry(oldState);
                    break;
                    
                case EnvironmentState.OutOfRange:
                    HandleOutOfRangeEntry();
                    break;
                    
                case EnvironmentState.InvalidTests:
                    HandleInvalidTestsEntry();
                    break;
            }
        }

        private void HandleInitializedEntry()
        {
            StopOutOfRangeTimer();
            _currentState.StateData["ProcessingBlocked"] = true;
            
            OnProcessingBlocked?.Invoke(this, new ProcessingBlockedEventArgs(
                ProcessingBlockReason.SystemInitialized, "System is in initialized state"));
        }

        private void HandleSteadyStateEntry(EnvironmentState oldState)
        {
            StopOutOfRangeTimer();
            _currentState.StateData["ProcessingBlocked"] = false;
            
            _logger.LogInformation("Entering steady state - processing enabled");
            
            // If recovering from out-of-range, log recovery time
            if (oldState == EnvironmentState.OutOfRange)
            {
                _logger.LogInformation("Recovered from out-of-range condition");
            }
        }

        private void HandleOutOfRangeEntry()
        {
            StartOutOfRangeTimer();
            _currentState.StateData["ProcessingBlocked"] = true;
            _currentState.StateData["TimerDuration"] = _configuration.OutOfRangeTimeoutMinutes;
            
            OnProcessingBlocked?.Invoke(this, new ProcessingBlockedEventArgs(
                ProcessingBlockReason.OutOfRange, "Environmental conditions are out of range"));
            
            _logger.LogWarning("Environmental conditions out of range - {TimeoutMinutes} minute timer started", 
                _configuration.OutOfRangeTimeoutMinutes);
        }

        private void HandleInvalidTestsEntry()
        {
            StopOutOfRangeTimer();
            _currentState.StateData["ProcessingBlocked"] = true;
            _currentState.StateData["RequiresManualReset"] = true;
            
            OnProcessingBlocked?.Invoke(this, new ProcessingBlockedEventArgs(
                ProcessingBlockReason.InvalidTests, "All tests invalidated - manual reset required"));
            
            _logger.LogError("Entered invalid tests state - all processing stopped, manual reset required");
        }

        private void StartOutOfRangeTimer()
        {
            _currentState.TimerStartedAt = DateTime.UtcNow;
            var timeoutMs = _configuration.OutOfRangeTimeoutMinutes * 60 * 1000;
            
            _outOfRangeTimer.Change(timeoutMs, Timeout.Infinite);
            
            _logger.LogInformation("Started out-of-range timer for {TimeoutMinutes} minutes", 
                _configuration.OutOfRangeTimeoutMinutes);
        }

        private void StopOutOfRangeTimer()
        {
            _outOfRangeTimer.Change(Timeout.Infinite, Timeout.Infinite);
            
            if (_currentState.TimerStartedAt.HasValue)
            {
                var timerDuration = DateTime.UtcNow - _currentState.TimerStartedAt.Value;
                _currentState.StateData["LastTimerDuration"] = timerDuration;
                _currentState.TimerStartedAt = null;
                
                _logger.LogInformation("Stopped out-of-range timer after {Duration}", timerDuration);
            }
        }

        private void OnOutOfRangeTimerExpired(object state)
        {
            lock (_stateLock)
            {
                if (_currentState.State == EnvironmentState.OutOfRange && _currentState.IsTimerActive())
                {
                    _logger.LogError("Out-of-range timer expired - transitioning to invalid tests state");
                    
                    TransitionToState(EnvironmentState.InvalidTests, 
                        $"Out-of-range timeout expired after {_configuration.OutOfRangeTimeoutMinutes} minutes");
                    
                    OnTimerExpired?.Invoke(this, new TimerExpiredEventArgs(
                        TimeSpan.FromMinutes(_configuration.OutOfRangeTimeoutMinutes)));
                }
            }
        }

        // IHostedService implementation
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("SchedulerStateManager service starting");
            
            _cancellationTokenSource = new CancellationTokenSource();
            
            // Subscribe to HAL events
            _halEventService.OnHalEvent += async (sender, args) => await HandleHalEvent(args.HalEvent);
            
            // Start event processing task
            _eventProcessingTask = Task.Run(async () => await ProcessEvents(_cancellationTokenSource.Token));
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("SchedulerStateManager service stopping");
            
            _cancellationTokenSource?.Cancel();
            
            if (_eventProcessingTask != null)
            {
                await _eventProcessingTask;
            }
            
            // Unsubscribe from events
            _halEventService.OnHalEvent -= async (sender, args) => await HandleHalEvent(args.HalEvent);
        }

        private async Task ProcessEvents(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Additional periodic processing could go here
                    await Task.Delay(5000, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in state manager event processing");
                    await Task.Delay(10000, cancellationToken); // Back off on error
                }
            }
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _eventProcessingTask?.Wait(TimeSpan.FromSeconds(10));
            
            _outOfRangeTimer?.Dispose();
            _cancellationTokenSource?.Dispose();
        }
    }

    // Supporting classes and interfaces
    public interface ISchedulerStateManager
    {
        SchedulerState GetCurrentState();
        bool IsProcessingAllowed();
        Task HandleHalEvent(HalEvent halEvent);
        bool ForceStateTransition(EnvironmentState newState, string reason = null);
        Task<bool> ResetSystem();
        TimeSpan? GetTimeRemainingInCurrentState();
        Dictionary<string, object> GetStateMetrics();
        
        event EventHandler<StateChangedEventArgs> OnStateChanged;
        event EventHandler<TimerExpiredEventArgs> OnTimerExpired;
        event EventHandler<ProcessingBlockedEventArgs> OnProcessingBlocked;
    }

    public class SchedulerState
    {
        public EnvironmentState State { get; private set; }
        public DateTime StateEnteredAt { get; private set; }
        public DateTime? TimerStartedAt { get; set; }
        public string Reason { get; private set; }
        public Dictionary<string, object> StateData { get; private set; }

        public TimeSpan TimeInState => DateTime.UtcNow - StateEnteredAt;

        public SchedulerState(EnvironmentState state, string reason)
        {
            State = state;
            Reason = reason ?? throw new ArgumentNullException(nameof(reason));
            StateEnteredAt = DateTime.UtcNow;
            StateData = new Dictionary<string, object>();
        }

        public bool CanTransitionTo(EnvironmentState newState)
        {
            return State switch
            {
                EnvironmentState.Initialized => newState is EnvironmentState.SteadyState or EnvironmentState.OutOfRange,
                EnvironmentState.SteadyState => newState is EnvironmentState.OutOfRange or EnvironmentState.Initialized,
                EnvironmentState.OutOfRange => newState is EnvironmentState.SteadyState or EnvironmentState.InvalidTests or EnvironmentState.Initialized,
                EnvironmentState.InvalidTests => newState is EnvironmentState.Initialized,
                _ => false
            };
        }

        public TimeSpan? GetTimeRemaining()
        {
            if (!IsTimerActive()) return null;
            
            var elapsed = DateTime.UtcNow - TimerStartedAt.Value;
            var timeout = TimeSpan.FromMinutes(8); // Configuration should be injected
            
            return timeout > elapsed ? timeout - elapsed : TimeSpan.Zero;
        }

        public bool IsTimerActive()
        {
            return TimerStartedAt.HasValue && State == EnvironmentState.OutOfRange;
        }
    }

    public class SchedulerStateConfiguration
    {
        public int OutOfRangeTimeoutMinutes { get; set; } = 8;
        public bool EnableAutomaticRecovery { get; set; } = true;
        public bool LogStateTransitions { get; set; } = true;
        public int StateMetricsIntervalSeconds { get; set; } = 30;
    }

    // Event argument classes
    public class StateChangedEventArgs : EventArgs
    {
        public EnvironmentState OldState { get; }
        public EnvironmentState NewState { get; }
        public string Reason { get; }
        public DateTime Timestamp { get; }

        public StateChangedEventArgs(EnvironmentState oldState, EnvironmentState newState, string reason)
        {
            OldState = oldState;
            NewState = newState;
            Reason = reason;
            Timestamp = DateTime.UtcNow;
        }
    }

    public class TimerExpiredEventArgs : EventArgs
    {
        public TimeSpan TimerDuration { get; }
        public DateTime ExpiredAt { get; }

        public TimerExpiredEventArgs(TimeSpan timerDuration)
        {
            TimerDuration = timerDuration;
            ExpiredAt = DateTime.UtcNow;
        }
    }

    public class ProcessingBlockedEventArgs : EventArgs
    {
        public ProcessingBlockReason Reason { get; }
        public string Message { get; }
        public DateTime BlockedAt { get; }

        public ProcessingBlockedEventArgs(ProcessingBlockReason reason, string message)
        {
            Reason = reason;
            Message = message;
            BlockedAt = DateTime.UtcNow;
        }
    }

    // Enums
    public enum EnvironmentState
    {
        Initialized,
        SteadyState,
        OutOfRange,
        InvalidTests
    }

    public enum ProcessingBlockReason
    {
        SystemInitialized,
        OutOfRange,
        InvalidTests,
        SystemReset,
        SystemError
    }

    public enum HalEventType
    {
        SteadyState,
        OutOfRange,
        SystemError,
        SystemRecovery,
        TemperatureAlert,
        PressureAlert,
        FlowAlert
    }

    public class HalEvent
    {
        public HalEventType Type { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object> Data { get; set; } = new();
    }

    // HAL Event Service interface (would be implemented elsewhere)
    public interface IHalEventService
    {
        event EventHandler<HalEventArgs> OnHalEvent;
    }

    public class HalEventArgs : EventArgs
    {
        public HalEvent HalEvent { get; }

        public HalEventArgs(HalEvent halEvent)
        {
            HalEvent = halEvent;
        }
    }
}