namespace Instrument.Scheduler.Abstraction;


public interface IExecutionPlan<T> : IExecutionPlan where T: IExecutionStep
{
    IEnumerable<T> ExecutionSteps { get; }
}

public interface IExecutionPlan
{
    int ExecutionId { get; }
}

