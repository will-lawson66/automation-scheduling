namespace Instrument.Scheduler.Cmr.Parser.ClassMap;

using CsvHelper.Configuration;
using Instrument.Scheduler.Model;

internal sealed class StopSolutionMap : ClassMap<StopSolution>
{
    #region Column Keys

    public const string LotKey = "Lot";
    public const string PositionKey = "Position";

    #endregion

    public StopSolutionMap()
    {

        Map(m => m.Lot)
            .Name(LotKey);

        Map(m => m.Position)
           .Name(PositionKey);
    }
}
