namespace Instrument.Scheduler.Cmr.Grpc.Message;

using ProtoBuf;

/// <summary>
/// A gRPC request for preparing an execution plan for  a cmr file into a known location
/// Paired with <see cref="PrepareCmrFileResponse"/>.
/// </summary>
/// <param name="FileName">
/// A human readable key that uniquely identifies the file resource.
/// This is usually the file name
/// </param>
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic, SkipConstructor = true)]
public sealed record PrepareCmrFileRequest(string FileName);
