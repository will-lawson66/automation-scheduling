namespace Instrument.Scheduler.Repository;

using Instrument.Defaults.Models;

public interface IDefaultsRepository
{
    IEnumerable<Sequence> GetDefaultSequences(int testMethodIdentifier);
}
