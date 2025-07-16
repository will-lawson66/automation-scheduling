namespace Instrument.Scheduler.Cmr.Parser;

using CsvHelper.TypeConversion;
using Instrument.Scheduler.Cmr.Model;

public class CmrParameterFactory
{
    public const string Delimiter = "=";

    public static ICmrParameter? ConvertFromString(string text)
    {  
        ICmrParameter? parameter = null;

        if (!string.IsNullOrEmpty(text))
        {
            var parts = text.Split(Delimiter);
            var parameterType = Enum.Parse<CmrFileParameterType>(parts[0].Trim(), true);
          
            switch (parameterType)
            {
                case CmrFileParameterType.Version:
                    parameter = new CmrParameter<CmrFileVersion>()
                    {
                        FileParameterType = parameterType,
                        Value = new CmrFileVersion(parts[1].Trim())
                    };
                    break;
                case CmrFileParameterType.Prime:
                case CmrFileParameterType.Waste:
                case CmrFileParameterType.Rinse:
                    parameter = new CmrParameter<bool>()
                    {
                        FileParameterType = parameterType,
                        Value = bool.Parse(parts[1].Trim())
                    };
                    break;
            }
        }

        return parameter;
    }
}
