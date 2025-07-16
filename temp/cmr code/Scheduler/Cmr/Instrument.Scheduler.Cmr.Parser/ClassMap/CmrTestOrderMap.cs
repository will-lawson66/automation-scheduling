using CsvHelper.Configuration;
using Instrument.Scheduler.Cmr.Model;
using Instrument.Scheduler.Data.Cmr.TypeConverters;
using Instrument.Scheduler.Model;

namespace Instrument.Scheduler.Cmr.Parser.ClassMap
{
    internal sealed class CmrTestOrderMap : ClassMap<CmrTestOrder>
    {
        #region Column Keys

        public const string TestMethodKey = "Method";
        public const string TechnologyKey = "Technology";

        #endregion

        public CmrTestOrderMap()
        {
            Map(m => m.TestMethod)
                .Name(TestMethodKey);

            Map(m => m.Technology)
                .Name(TechnologyKey)
                .TypeConverter(new TrimmedEnumConverter<Technology>())
                .TypeConverterOption.EnumIgnoreCase()
                .Default(Technology.ImmunoCap);


            References<SampleMap>(m => m.SampleInformation);
            References<TestMap>(m => m.TestVessel);
            References<WashSolutionMap>(m => m.WashSolution);
            References<SampleDilutionMap>(m => m.SampleDilution);
            References<DiluentMap>(m => m.Diluent);
            References<ConjugateMap>(m => m.Conjugate);
            References<StopSolutionMap>(m => m.StopSolution);
            References<DevelopmentSolutionMap>(m => m.DevelopmentSolution);
        }
    }
}
