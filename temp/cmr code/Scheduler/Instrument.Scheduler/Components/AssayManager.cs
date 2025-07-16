namespace Instrument.Scheduler.Components;

using Instrument.Scheduler.Model;

public class AssayManager
{
    private readonly List<AssaySample> _assaySamples;

    public AssayManager() {
         _assaySamples = [];
    }

    /// <summary>
    /// Registers assay samples with the 
    /// </summary>
    /// <param name="assaySamples"></param>
    /// <returns></returns>
    public bool AddAssaySamples(IEnumerable<AssaySample> assaySamples)
    {
        _assaySamples.AddRange(assaySamples);
        return true;
    }
}
