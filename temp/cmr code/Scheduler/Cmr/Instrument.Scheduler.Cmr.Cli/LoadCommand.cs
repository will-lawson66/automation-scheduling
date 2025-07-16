namespace Instrument.Scheduler.Cmr.Cli;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Instrument.Authentication;
using Instrument.Cli;
using Instrument.Cli.Helpers;
using Instrument.Execution.Grpc.Schedule.Cli;
using Instrument.Scheduler.Cmr.Grpc;
using Instrument.Scheduler.Cmr.Grpc.Message;

public class LoadCommand : ICommand
{
    internal const string _keyArgument = "name";
    internal const string _pathArgument = "path";

    private readonly ICmrService _cmrService;

    /// <summary>
    /// Constructs a CLI command that uses <see cref="ICmrService"/> to a push
    /// new cmr files into a known location.
    /// </summary>
    /// <param name="cmrService">
    /// The service to rely on to do the actual push. 
    /// </param>
    public LoadCommand(ICmrService cmrService)
    {
        _cmrService = cmrService;
    }

    /// <inheritdoc />
    public string Name => "load";

    /// <inheritdoc />
    public string HelpSummary => "Loads a cmr file to a known location on the ISW";

    /// <inheritdoc />
    public string? Alias => "l";

    /// <inheritdoc />
    public IEnumerable<string> HelpDetails =>
        [
            "Requires at least a file and a name for the stored file."
        ];

    /// <inheritdoc />
    public IEnumerable<IArgument> Arguments =>
        [
        CommandHelper.CreateBasicArgument(
            argumentType: ArgumentType.String,
            name: _keyArgument,
            description: "Provides a human readable unique name for the stored file."),

        CommandHelper.CreateBasicArgument(
            argumentType: ArgumentType.String,
            name: _pathArgument,
            description: "Provides the full path to the file in the local drives."),
    ];

    /// <inheritdoc />
    public IEnumerable<IOption> Options => [];

    /// <inheritdoc />
    public IEnumerable<string> UsageMessages => new[]
    {
        $"{Name} <{_keyArgument}> <{_pathArgument}>",
    };

    /// <inheritdoc />
    public IEnumerable<string> ExampleMessages =>
    [  
        $"{Name} Example \"C:\\Path\\To\\Cmrfile.csv\"",
    ];

    /// <inheritdoc />
    public async Task<ICommandResult> RunCommandAsync(
            IEnumerable<IArgumentValue> arguments,
            IEnumerable<IOptionValue> options,
            IAuthenticationToken? authenticationContext = null,
            CancellationToken cancellationToken = default)
        {
            var key = arguments.First(value => value.Argument.Name == _keyArgument).Value;
            var path = arguments.First(value => value.Argument.Name == _pathArgument).Value;
            var cmrFileContent = await File.ReadAllTextAsync(path, cancellationToken);

            var request = new LoadCmrFileRequest(FileName: key,
                                                 CmrContent: cmrFileContent);

            var response = await _cmrService.LoadFileAsync(request, cancellationToken);
            return response.ToCliResult();
        }
}
