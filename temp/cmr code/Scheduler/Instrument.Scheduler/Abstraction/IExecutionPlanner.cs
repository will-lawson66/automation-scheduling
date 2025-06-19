namespace Instrument.Scheduler.Abstraction;

public interface IExecutionPlanner<TStep>
        where TStep : IExecutionPlan
{
    TStep GetExecutionPlan();
}
