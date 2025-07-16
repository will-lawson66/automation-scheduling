namespace Instrument.Scheduler.Abstraction;

using Instrument.Scheduler.Abstraction.TestOrder;

public interface IExecutionStep<T> : IExecutionStep where T : ITestOrder
{
    IEnumerable<T> TestOrders { get; set; }
}


public interface IExecutionStep
{
    /// <summary>
    /// Step Identifier
    /// </summary>
    int ExecutionStepIdentifier { get; }
}
