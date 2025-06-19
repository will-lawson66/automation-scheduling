using CsvHelper.Configuration;
using CsvHelper;
using CsvHelper.TypeConversion;

namespace Instrument.Scheduler.Data.Cmr.TypeConverters
{
    public class TrimmedEnumConverter<T> : EnumConverter
    {
        public TrimmedEnumConverter() : base(typeof(T)) { }

        public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
        {
            return base.ConvertFromString(text.Trim(), row, memberMapData);
        }
    }
}
