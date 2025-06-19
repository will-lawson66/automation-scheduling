namespace Instrument.Scheduler.Model;

using ProtoBuf;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic, SkipConstructor = true)]
public class SampleDilution
{
    public string PreDilutionFactor { get; set; }

    public string InstrumentDilutionFactor  { get; set;}

    public string DilutionMethod { get; set; }
}
