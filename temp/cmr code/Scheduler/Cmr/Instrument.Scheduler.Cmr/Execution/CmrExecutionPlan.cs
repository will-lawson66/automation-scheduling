namespace Instrument.Scheduler.Cmr.Execution;

using System.Collections.Generic;
using System.Text.Json;
using Instrument.Scheduler.Abstraction;
using ProtoBuf;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic, SkipConstructor = true)]
public class CmrExecutionPlan : IExecutionPlan<CmrExecutionStep>
{
    private readonly JsonSerializerOptions _serializerOptions;

    public CmrExecutionPlan(IEnumerable<CmrExecutionStep> executionSteps)
    {
        ExecutionSteps = executionSteps;
        _serializerOptions = new JsonSerializerOptions() { WriteIndented = true};
    }

    public int ExecutionId { get; private set; }

    public IEnumerable<CmrExecutionStep> ExecutionSteps { get; }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, _serializerOptions);
    }
}
