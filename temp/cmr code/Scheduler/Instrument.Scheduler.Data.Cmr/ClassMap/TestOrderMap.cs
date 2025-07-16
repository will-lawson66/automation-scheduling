using CsvHelper.Configuration;
using Instrument.Scheduler.Model;

namespace Instrument.Scheduler.Data.Cmr.ClassMap
{
    public sealed class TestOrderMap : ClassMap<TestOrder>
    {
        public TestOrderMap()
        {
            Map(m => m.TestName).Name("TestName");
            Map(m => m.MethodName).Name("Method");
            References<SampleMap>(m => m.SampleInformation);
        }
    }
}
