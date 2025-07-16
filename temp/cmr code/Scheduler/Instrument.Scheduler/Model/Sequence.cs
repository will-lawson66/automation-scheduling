namespace Instrument.Scheduler.Model;

public class Sequence
{
    public int Id { get; set; }

    public string Name { get; set; }

    public int Period { get; set; }

    public TimeSpan Offset { get; set; }
}
