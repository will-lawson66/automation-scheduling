using CsvHelper.Configuration;
using Instrument.Scheduler.Cmr.Model;

namespace Instrument.Scheduler.Cmr.Parser.ClassMap;

public class TestMap : ClassMap<Test>
{
    #region Column Keys

    public const string TestNameKey = "TestName";
    public const string CarrierLotKey = "CarrierLot";
    public const string CarrierPositionKey = "CarrierPos";

    #endregion


    public TestMap()
    {
        Map(m => m.TestName)
            .Name(TestNameKey);

        Map(m => m.CarrierLot)
            .Name(CarrierLotKey);

        Map(m => m.CarrierPosition)
            .Name(CarrierPositionKey);
    }
}
