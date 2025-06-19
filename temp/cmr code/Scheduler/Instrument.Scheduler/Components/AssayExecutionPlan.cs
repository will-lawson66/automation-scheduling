namespace Instrument.Scheduler.Components;
using System.Collections.Generic;
using System.Text.Json;
using Instrument.Scheduler.Abstraction;

public class AssayExecutionPlan : IExecutionPlan<AssayExecutionStep>
{
    private readonly JsonSerializerOptions _serializerOptions;

    public AssayExecutionPlan(IReadOnlyCollection<AssayExecutionStep> executionSteps)
    {
        ExecutionSteps = executionSteps;
        _serializerOptions = new JsonSerializerOptions() { WriteIndented = true };
    }

    public int ExecutionId { get; private set; }

    public IEnumerable<AssayExecutionStep> ExecutionSteps { get; }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, _serializerOptions);
    }
}
