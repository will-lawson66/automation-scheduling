namespace Instrument.Scheduler.Model;

using ProtoBuf;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic, SkipConstructor = true)]
public class StopSolution
{
    public string Lot { get; set; }

    public string Position { get; set; }
}
