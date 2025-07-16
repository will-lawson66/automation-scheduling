namespace Instrument.Scheduler.Cmr.Model;

using System.Text.Json;
using Instrument.Scheduler.Abstraction.TestOrder;
using Instrument.Scheduler.Model;
using ProtoBuf;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic, SkipConstructor = true)]
public class CmrTestOrder : ITestOrder
{
    private readonly JsonSerializerOptions _serializerOptions = new() { WriteIndented = true };

    public int CustomRunId { get; set; }

    public int GroupId { get; set; }

    public int Id { get; set; }

    public Sample SampleInformation { get; set; }

    public string TestMethod { get; set; }

    public Technology Technology { get; set; }

    public Test TestVessel { get; set; }

    public WashSolution WashSolution { get; set; }

    public SampleDilution SampleDilution { get; set; }

    public Diluent Diluent { get; set; }

    public Conjugate Conjugate { get; set; }

    public StopSolution StopSolution { get; set; }

    public DevelopmentSolution DevelopmentSolution { get; set; }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, _serializerOptions);
    }
}
