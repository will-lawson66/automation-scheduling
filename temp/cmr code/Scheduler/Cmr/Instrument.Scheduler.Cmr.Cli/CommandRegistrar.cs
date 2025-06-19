namespace Instrument.Scheduler.Cmr.Cli;

using System.Diagnostics.CodeAnalysis;
using Instrument.Cli;
using Instrument.Grpc.Client;
using Instrument.Scheduler.Cmr.Grpc;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides a mechanism to plugin the commands to the CLI. 
/// </summary>
[ExcludeFromCodeCoverage(
    Justification = "Requires an actual gRPC channel to be opened. Better covered by SQE or integration testing.")]
public sealed class CommandRegistrar : ICommandInjector
{
    /// <inheritdoc />
    public void RegisterServicesAndCommands(
        IServiceCollection services)
    {
        _ = services
            .AddGrpcService<ICmrService>()
            .AddCliCommandGroup("cmr", "CMR Load, Prepare, Start, Abort operations", null,
                g => g
                .AddCliCommand<LoadCommand>()
                .AddCliCommand<PrepareCommand>()
                .AddCliCommand<ExecuteCommand>()
                /*.AddCliCommand<AbortCommand>()*/
            );
    }
}

