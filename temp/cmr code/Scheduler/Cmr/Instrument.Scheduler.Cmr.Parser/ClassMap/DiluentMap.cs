namespace Instrument.Scheduler.Cmr.Parser.ClassMap;

using CsvHelper.Configuration;
using Instrument.Scheduler.Model;

internal class DiluentMap : ClassMap<Diluent>
{
    #region Column Keys

    public const string NameKey = "Name";
    public const string LotKey = "Lot";
    public const string PositionKey = "Position";

    #endregion

    public DiluentMap()
    {
        Map(m => m.Name)
            .Name(NameKey)
            .Optional();

        Map(m => m.Lot)
            .Name(LotKey);

        Map(m => m.Position)
           .Name(PositionKey);
    }
}
