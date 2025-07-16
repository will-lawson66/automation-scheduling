namespace Instrument.Scheduler.Cmr;

using Instrument.Scheduler.Cmr.Model;

/// <summary>
///  A test order parser will encapsualte the logic for extraction of test orders
///  from <see cref="ICmrFileSource"/> a test order file
/// </summary>
public interface ICmrFileParser
{
    CmrFile Parse(ICmrFileSource cmrFileSource);
}

