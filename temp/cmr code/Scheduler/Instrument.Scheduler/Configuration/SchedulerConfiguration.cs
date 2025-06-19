namespace Instrument.Scheduler.Configuration;

public class SchedulerConfiguration
{
    public required string StoragePath { get; set; }

    public required CmrConfiguration CmrConfiguration { get; set; }
}
