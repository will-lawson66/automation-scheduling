namespace Instrument.Scheduler.Cmr.Parser;

using System.Collections.Generic;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Instrument.Scheduler.Data.Cmr.ClassMap;
using Instrument.Scheduler.Data.Cmr.Specification;
using Instrument.Scheduler.Model;

public class CmrFileParser : ICmrFileParser
{
    /// <summary>
    /// 
    /// </summary>
    private const string CommentsPrefix = ";";

    private readonly CmrTestOrderFile _source;

    public CmrFileParser(CmrTestOrderFile cmrTestOrderFile)
    {
        if (!cmrTestOrderFile.SourceFile.Exists)
        {
            throw new SchedulerException(SchedulerErrorMessages.ParserFileNotFound);
        }

        _source = cmrTestOrderFile;
    }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    /// <returns></returns>
    public IEnumerable<TestOrder> GetTestOrders()
    {
        List<TestOrder> testOrders = [];


        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            Delimiter = _source.Delimiter,
            IgnoreBlankLines = true,
            TrimOptions = TrimOptions.Trim
        };

        using (var reader = new StreamReader(_source.SourceFile.FullName))
        using (var csv = new CsvReader(reader, config))
        {
            // Register class maps
            csv.Context.RegisterClassMap<TestOrderMap>();

            while(csv.Read())
            {
                switch(csv.GetField(0))
                {
                    case CmrFileSections.Parameter:
                        break;
                    case CmrFileSections.TestOrder:
                        csv.Read();
                        csv.ReadHeader(); // Read Header

                        while(csv.Read())
                        {
                            testOrders.Add(csv.GetRecord<TestOrder>());
                        }

                        break;
                }
            }
        }

        return testOrders;
    }
}
