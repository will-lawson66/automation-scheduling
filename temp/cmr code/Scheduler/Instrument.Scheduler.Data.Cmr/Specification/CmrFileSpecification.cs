using Instrument.Scheduler.Model;
using CsvHelper.TypeConversion;

namespace Instrument.Scheduler.Data.Cmr.Specification
{
    public class CmrFileSpecification
    {
        public int MajorVersion { get; }

        public int MinorVersion { get; }

        public List<CmrColumnSpecification> ColumnSpecificatons { get; private set; }

        public CmrFileSpecification()
        {
            PopulateColumnSpecifications();
        }

        private void PopulateColumnSpecifications()
        {
            ColumnSpecificatons = [
                new CmrColumnSpecification(columnName: "SampleId",propertyName: "SampleId", propertyType: typeof(string))
                        .SetDefaultValue(string.Empty),

                new CmrColumnSpecification(columnName: "SampleType",propertyName: "SampleType", propertyType: typeof(SampleType))
                        .SetTypeConverter(new EnumConverter(typeof(SampleType)))





                ]
            ;
        }
    }
}
