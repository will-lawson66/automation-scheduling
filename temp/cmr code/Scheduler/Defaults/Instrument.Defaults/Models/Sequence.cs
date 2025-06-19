namespace Instrument.Defaults.Models;
using System;

public class Sequence
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }

    public DateTime Created { get; set; }

    public bool IsActive { get; set; }
}
