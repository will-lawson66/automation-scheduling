namespace Instrument.Scheduler.Components;

using System.Collections.Generic;
using System.Text.Json;

using Instrument.Scheduler.Abstraction;
using Instrument.Scheduler.Abstraction.TestOrder;

public class AssayExecutionStep : IExecutionStep<ITestOrder>
{
    private readonly JsonSerializerOptions _serializerOptions = new() { WriteIndented = true };

    public required IEnumerable<ITestOrder> TestOrders { get; set; }

    public int ExecutionStepIdentifier { get; }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, _serializerOptions);
    }
}
