namespace Instrument.Scheduler.Cmr.Cli;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Instrument.Authentication;
using Instrument.Cli;

public class AbortCommand : ICommand
{
    public string Name => throw new NotImplementedException();

    public string HelpSummary => throw new NotImplementedException();

    public string? Alias => throw new NotImplementedException();

    public IEnumerable<string> HelpDetails => throw new NotImplementedException();

    public IEnumerable<IArgument> Arguments => throw new NotImplementedException();

    public IEnumerable<IOption> Options => throw new NotImplementedException();

    public IEnumerable<string> UsageMessages => throw new NotImplementedException();

    public IEnumerable<string> ExampleMessages => throw new NotImplementedException();

    public Task<ICommandResult> RunCommandAsync(IEnumerable<IArgumentValue> arguments, IEnumerable<IOptionValue> options, IAuthenticationToken? authenticationContext = null, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
