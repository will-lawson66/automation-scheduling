namespace Instrument.Defaults;

using Instrument.Defaults.Models;
using Microsoft.EntityFrameworkCore;


public interface IDefaultsDbContext
{
    DbSet<Unit> Unit { get; set; }

    DbSet<Technology> Technology { get; set; }

    DbSet<Sequence> Sequence { get; set; }

    DbSet<TestMethod> TestMethod { get; set; }

    DbSet<TestMethodSequences> TestMethodSequences { get; set; }

    DbSet<VolumeDefault> VolumeDefaults { get; set; }

}

