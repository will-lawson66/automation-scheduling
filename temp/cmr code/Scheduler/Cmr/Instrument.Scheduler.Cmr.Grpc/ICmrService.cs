namespace Instrument.Scheduler.Cmr.Grpc;

using System.Threading;
using System.Threading.Tasks;
using Instrument.Scheduler.Cmr.Grpc.Message;
using ProtoBuf.Grpc.Configuration;
using ProtoBuf.Meta;

[Service]
public interface ICmrService
{
    /// <summary>
    /// Stores a new cmr file with the given name (<see cref="IScriptSource.Name"/>) and meta-data.
    /// If a cmr file already exists with the given name, a unique number will appended to the script
    /// name to differentiate it from existing scripts.
    /// </summary>
    /// <param name="request">
    /// The details of the cmr file to create.
    /// </param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to request the cancellation
    /// of the current operation.
    /// </param>
    /// <returns>
    /// An awaitable that will contain the key of the cmr file as stored when completed.
    /// </returns>
    Task<LoadCmrFileResponse> LoadFileAsync(
        LoadCmrFileRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Prepares an execution plan that can be serialized and submitted to the
    /// Scheduler for execution
    /// </summary>
    /// <param name="request">
    ///     Request object with details about the identity of the Cmr file that needs to be prepared
    /// </param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to request the cancellation
    /// of the current operation.
    /// </param>
    /// <returns>
    ///     A response with an execution plan
    /// </returns>
    Task<PrepareCmrFileResponse> PrepareCmrFileAsync(
        PrepareCmrFileRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts an execution of a CMR file specification
    /// </summary>
    /// <param name="request">
    ///     Request object with details about the identity of the Cmr file that needs to be prepared
    /// </param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to request the cancellation
    /// of the current operation.
    /// </param>
    /// <returns>
    ///     A response for the execute command
    /// </returns>
    Task<ExecuteCmrFileResponse> ExecuteCmrFileAsync(
        ExecuteCmrFileRequest request, CancellationToken cancellationToken = default);
}
