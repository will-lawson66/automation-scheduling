namespace Instrument.Scheduler.Abstraction.TestOrder;

using Instrument.Scheduler.Model;

public interface ITestOrder
{
    int Id { get; set; }

    public Sample SampleInformation { get; set; }

    public string TestMethod { get; set; }

}
