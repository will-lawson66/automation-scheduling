namespace Instrument.Scheduler.Components;

using System.Collections.Generic;
using Instrument.Execution.Grpc.Parameters;
using Instrument.Execution.Grpc.Schedule;
using Instrument.Logger;
using Instrument.Scheduler.Model;

internal class SequenceGroupManager
{
    private readonly IDictionary<int, SequenceGroup> _sequenceGroups;
    private readonly IScheduleExecutionService _scheduleExecutionServiceClient;
    private readonly CancellationTokenSource _cancellationToken;
    private readonly IInstrumentLogger _logger;
    private Task? _streamingTask;

    public SequenceGroupManager(IScheduleExecutionService scheduleExecutionServiceClient,
                                IInstrumentLoggerFactory loggerFactory,   
                                CancellationTokenSource cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scheduleExecutionServiceClient);
        _sequenceGroups = new Dictionary<int, SequenceGroup>();
        _scheduleExecutionServiceClient = scheduleExecutionServiceClient;
        _cancellationToken = cancellationToken;
        _logger = loggerFactory.ForContext<SequenceGroupManager>();
        IsExecuting = false;
    }

    public bool IsExecuting { get; private set; }

    public void StartExecution()
    {
        IsExecuting = true;

        // Start a task for period changes
        SubscribeToPeriodChanges();
    }

    public void AbortExecution()
    {
        IsExecuting = false;
        _cancellationToken.Cancel();
        _streamingTask = null;
    }

    public void RegisterSequenceGroups(IReadOnlyList<SequenceGroup> sequenceGroups)
    {
        foreach (var sequenceGroup in sequenceGroups)
        {
            if (!_sequenceGroups.ContainsKey(sequenceGroup.Id))
            {
                _sequenceGroups[sequenceGroup.Id] = sequenceGroup;
            }
        }
    }

    private void SubscribeToPeriodChanges()
    {
        var periodEventsSubsciber = _scheduleExecutionServiceClient
                                        .SubscribeToPeriodEventsAsync(_cancellationToken.Token);

        _streamingTask =
            Task.Run(async () =>
                                {
                                    try
                                    {
                                        await foreach (var periodEventData in periodEventsSubsciber.WithCancellation(_cancellationToken.Token))
                                        {
                                            SendExecutionEngineRequestsForNextPeriod(currentPeriodEventData: (PeriodUpdatedData)periodEventData);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.Error(ex);
                                    }
                    
                        }, _cancellationToken.Token);
    }

    private async void SendExecutionEngineRequestsForNextPeriod(PeriodUpdatedData currentPeriodEventData)
    {
        if (currentPeriodEventData == null)
        {
            return;
        }

        var nextPeriod = currentPeriodEventData.CurrentPeriod++;

        // 1) From Each Sequence Group, get the next sequences that need to be executed, in the NEXT Period

        var executionRequestRequests = new List<ExecutionRequestRequest>();
        foreach (var sequenceGroup in _sequenceGroups.Values)
        {
            var nextSequence = sequenceGroup.Next(nextPeriod);
            if (nextSequence != null)
            {
                executionRequestRequests.Add(
                        new ExecutionRequestRequest(nextPeriod, nextSequence.Offset, nextSequence.Name, Array.Empty<ParameterValueContract>()));
            }
        }

        await _scheduleExecutionServiceClient
                        .ExecuteScheduleAsync(new ScheduledExecutionRequest(executionRequestRequests), CancellationToken.None);
    }
}
