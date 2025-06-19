using CsvHelper.Configuration;
using Instrument.Scheduler.Data.Cmr.TypeConverters;
using Instrument.Scheduler.Model;

namespace Instrument.Scheduler.Cmr.Parser.ClassMap
{
    internal sealed class SampleMap : ClassMap<Sample>
    {
        #region Column Keys

        public const string SampleIdKey = "SampleId";
        public const string SampleTypeKey = "SampleType";
        public const string TubeTypeKey = "TubeType";
        public const string SampleInputKey = "SampleInput";
        public const string SamplePosKey = "SamplePos";
        public const string CartridgeLotKey = "CartridgeLot";

        #endregion

        public SampleMap()
        {
            Map(m => m.Id)
                .Name(SampleIdKey)
                .Optional();

            Map(m => m.Type)
                .Name(SampleTypeKey)
                .TypeConverter(new TrimmedEnumConverter<SampleType>())
                .TypeConverterOption.EnumIgnoreCase()
                .Default(SampleType.Unspecified);

            Map(m => m.TubeType)
               .Name(TubeTypeKey)
               .TypeConverter(new TrimmedEnumConverter<TubeType>())
               .TypeConverterOption.EnumIgnoreCase()
               .Default(TubeType.TUBE_TYPE_1);

            Map(m => m.SampleInputLocation)
              .Name(SampleInputKey)
              .TypeConverter(new TrimmedEnumConverter<SampleInputLocation>())
              .TypeConverterOption.EnumIgnoreCase()
              .Default(SampleInputLocation.Storage);

            Map(m => m.Position)
              .Name(SamplePosKey)
              .Optional()
              .Default(string.Empty);

            Map(m => m.CartridgeLot)
             .Name(CartridgeLotKey)
             .Optional()
             .Default(string.Empty);
        }
    }
}
