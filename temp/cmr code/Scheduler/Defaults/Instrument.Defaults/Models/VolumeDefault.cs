namespace Instrument.Defaults.Models;

public class VolumeDefault
{
    public int Id { get; set; }

    public int TechnologyId { get; set; }

    public int SampleVolume { get; set; }

    public int ConjugateVolume { get; set; }

    public int DevelopmentVolume { get; set; }

    public int StopSolutionVolume { get; set; }
}