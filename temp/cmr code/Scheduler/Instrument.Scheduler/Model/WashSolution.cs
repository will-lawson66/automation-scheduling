namespace Instrument.Scheduler.Model;
using ProtoBuf;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic, SkipConstructor = true)]
public class WashSolution
{
    public string WashId { get; set; }

    public string WashConcentrateLot { get; set; }

    public string WashConcentratePosition { get; set; }

    public string WashAdditiveLot { get; set; }

    public string WashAdditivePosition { get; set; }

}
