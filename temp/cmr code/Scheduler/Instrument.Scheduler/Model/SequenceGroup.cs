namespace Instrument.Scheduler.Model;

public class SequenceGroup
{
    public int Id { get; set; }

    public required IList<Sequence> Sequences { get; set; }

    public Sequence? Next(int period)
    {
        return null;
    }
}
