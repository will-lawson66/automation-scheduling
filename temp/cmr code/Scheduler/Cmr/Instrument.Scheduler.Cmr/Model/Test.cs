namespace Instrument.Scheduler.Cmr.Model;
using ProtoBuf;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic, SkipConstructor = true)]
public class Test
{
    public string TestName { get; set; }

    public string CarrierLot { get; set; }

    public string CarrierPosition { get; set; }
}
