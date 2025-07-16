namespace Instrument.Scheduler.Cmr.Parser.ClassMap;

using CsvHelper.Configuration;
using Instrument.Scheduler.Model;

internal class WashSolutionMap : ClassMap<WashSolution>
{
    #region Column Keys

    public const string WashIdKey = "WashId";
    public const string WashConcentrateLotKey = "WashConcentrateLot";
    public const string WashConcentratePositionKey = "WashConcentratePosition";
    public const string WashAdditiveLotKey = "WashAdditiveLot";
    public const string WashAdditivePositionKey = "WashAdditivePosition";

    #endregion


    public WashSolutionMap()
    {
        Map(m => m.WashId)
            .Name(WashIdKey);

        Map(m => m.WashConcentrateLot)
            .Name(WashConcentrateLotKey);

        Map(m => m.WashConcentratePosition)
            .Name(WashConcentratePositionKey);

        Map(m => m.WashConcentratePosition)
           .Name(WashConcentratePositionKey);

        Map(m => m.WashAdditiveLot)
           .Name(WashAdditiveLotKey);
    }
}
