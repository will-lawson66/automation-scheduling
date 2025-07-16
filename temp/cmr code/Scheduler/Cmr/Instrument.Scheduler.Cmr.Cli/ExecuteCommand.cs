namespace Instrument.Scheduler.Cmr.Cli;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Instrument.Authentication;
using Instrument.Cli;
using Instrument.Cli.Helpers;
using Instrument.Scheduler.Cmr.Grpc;
using Instrument.Scheduler.Cmr.Grpc.Message;

public class ExecuteCommand : ICommand
{
    internal const string _keyArgument = "name";

    private readonly ICmrService _cmrService;

    /// <summary>
    /// Constructs a CLI command that uses <see cref="ICmrService"/> to start
    /// an assay run, from a known cmr file
    /// </summary>
    /// <param name="cmrService">
    /// The CMR Service
    /// </param>
    public ExecuteCommand(ICmrService cmrService)
    {
        _cmrService = cmrService;
    }

    public string Name => "execute";

    public string HelpSummary =>
        "This will start an assay run for a known cmr file";

    public string? Alias => "e";

    public IEnumerable<string> HelpDetails =>
        [
            "Requires the file name for the stored Cmr file"
        ];

    public IEnumerable<IArgument> Arguments => [
        CommandHelper.CreateBasicArgument(
            argumentType: ArgumentType.String,
            name: _keyArgument,
            description: "Cmr key or file name to generate the execution plan")
    ];

    public IEnumerable<IOption> Options => [];

    public IEnumerable<string> UsageMessages => new[]
        {
            $"{Name} <{_keyArgument}>",
        };

    public IEnumerable<string> ExampleMessages =>
        [
            $"{Name} Example",
        ];

    public async Task<ICommandResult> RunCommandAsync(IEnumerable<IArgumentValue> arguments, IEnumerable<IOptionValue> options, IAuthenticationToken? authenticationContext = null, CancellationToken cancellationToken = default)
    {
        var key = arguments.First(value => value.Argument.Name == _keyArgument).Value;
        var request = new ExecuteCmrFileRequest(FileName: key);
        var response = await _cmrService.ExecuteCmrFileAsync(request, cancellationToken);
        return response.ToCliResult();
    }
}
