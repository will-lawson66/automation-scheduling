using CsvHelper.Configuration;
using Instrument.Scheduler.Data.Cmr.TypeConverters;
using Instrument.Scheduler.Model;

namespace Instrument.Scheduler.Data.Cmr.ClassMap
{
    public sealed class SampleMap : ClassMap<Sample>
    {
        public SampleMap()
        {
            Map(m => m.SampleId)
                .Name("SampleId")
                .Optional();
            Map(m => m.SampleType)
                .Name("SampleType")
                .TypeConverter(new TrimmedEnumConverter<SampleType>())
                .TypeConverterOption.EnumIgnoreCase()
                .Default(SampleType.SAMPLE);
                
        }
    }
}
