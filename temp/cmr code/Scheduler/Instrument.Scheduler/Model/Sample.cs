namespace Instrument.Scheduler.Model;
using ProtoBuf;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic, SkipConstructor = true)]
public class Sample
{
    /// <summary>
    /// Sample identification (Barcode on sample tube or reagent).
    /// </summary>
    public string Id { get; set; }

    public SampleType Type { get; set; }

    public TubeType TubeType { get; set; }

    public SampleInputLocation SampleInputLocation { get; set; }

    public string Position { get; set; }

    public string CartridgeLot { get; set; }
}
