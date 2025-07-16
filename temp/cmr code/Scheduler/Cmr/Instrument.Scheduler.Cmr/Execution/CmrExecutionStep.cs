namespace Instrument.Scheduler.Cmr.Execution;

using System.Collections.Generic;
using System.Text.Json;
using Instrument.Scheduler.Cmr.Model;
using Instrument.Scheduler.Abstraction;
using ProtoBuf;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic, SkipConstructor = true)]
public class CmrExecutionStep : IExecutionStep<CmrTestOrder>
{
    private readonly JsonSerializerOptions _serializerOptions = new() { WriteIndented =  true };

    public required IEnumerable<CmrTestOrder> TestOrders { get; set; }

    public int ExecutionStepIdentifier  { get; }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, _serializerOptions);
    }
}
