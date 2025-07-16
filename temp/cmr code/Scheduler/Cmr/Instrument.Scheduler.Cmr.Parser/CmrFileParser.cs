namespace Instrument.Scheduler.Cmr.Parser;

using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Instrument.Scheduler.Cmr;
using Instrument.Scheduler.Cmr.Model;
using Instrument.Scheduler.Cmr.Parser.ClassMap;

public class CmrFileParser : ICmrFileParser
{
    public CmrFile Parse(ICmrFileSource cmrFileSource)
    {
        List<ICmrParameter> parameters = [];
        List<CmrTestOrder> testOrders = [];

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            Delimiter = cmrFileSource.Delimiter,
            IgnoreBlankLines = true,
            TrimOptions = TrimOptions.Trim
        };

        using (var reader = new StreamReader(cmrFileSource.SourceFile.FullName))
        using (var csv = new CsvReader(reader, config))
        {
            // Register class maps
            csv.Context.RegisterClassMap<CmrTestOrderMap>();

            // Process the file
            var currentSection = string.Empty;
            var parameterFactory = new CmrParameterFactory();

            while (csv.Read())
            {
                var isHeader = csv.GetField(0) == CmrFileSections.Parameter || csv.GetField(0) == CmrFileSections.TestOrder;

                if (isHeader)
                {
                    currentSection = csv.GetField(0);

                    if (currentSection == CmrFileSections.TestOrder)
                    {
                        csv.Read();
                        csv.ReadHeader();
                    }

                    continue;
                }

                switch (currentSection)
                {
                    case CmrFileSections.Parameter:
                        var parameter = CmrParameterFactory.ConvertFromString(csv.GetField(0));
                        if (parameter != null)
                        {
                            parameters.Add(parameter);
                        }

                        break;
                    case CmrFileSections.TestOrder:
                        testOrders.Add(csv.GetRecord<CmrTestOrder>());
                        break;
                }
            }
        }

        var cmrFile = new CmrFile() { Orders = testOrders, Parameters = parameters };
        return cmrFile;
    }
}
