namespace Instrument.Scheduler.Model;

using ProtoBuf;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic, SkipConstructor = true)]
public class Diluent
{
    public string Name { get; set; }

    public string Lot { get; set; }

    public string Position { get; set; }
}
