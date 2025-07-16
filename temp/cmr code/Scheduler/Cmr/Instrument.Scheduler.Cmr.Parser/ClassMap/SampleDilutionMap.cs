namespace Instrument.Scheduler.Cmr.Parser.ClassMap;

using CsvHelper.Configuration;
using Instrument.Scheduler.Model;

internal class SampleDilutionMap : ClassMap<SampleDilution>
{
    #region Column Keys

    public const string PreDilutionFactorKey = "PreDilutionFactor";
    public const string InstrumentDilutionFactorKey = "InstrumentDilutionFactor";
    public const string DilutionMethodKey = "DilutionMethod";

    #endregion

    public SampleDilutionMap()
    {
        Map(m => m.PreDilutionFactor)
            .Name(PreDilutionFactorKey);

        Map(m => m.InstrumentDilutionFactor)
            .Name(InstrumentDilutionFactorKey);

        Map(m => m.DilutionMethod)
           .Name(DilutionMethodKey);
    }
}


