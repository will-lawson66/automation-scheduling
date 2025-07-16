namespace Instrument.Scheduler.Cmr.Grpc.Message;

using System;
using System.Collections.Generic;
using Instrument.Grpc;
using ProtoBuf;

/// <summary>
/// The gRPC response for for persisting a cmr file into a known location.
/// Paired with the <see cref="PushLocalFileResponse"/>.
/// </summary>
/// <param name="RequestId">
/// A request ID to trace the original request to. 
/// </param>
/// <param name="InsertedKey">
/// The inserted key may differ from the requested key in situations where there
/// already exists a cmr file with that key.
/// </param>
/// <param name="Errors">
/// Any errors encountered trying to fetch the CMR File.
/// </param>
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic, SkipConstructor = true)]
public sealed record LoadCmrFileResponse(
    Guid RequestId,
    string InsertedKey,
    IReadOnlyCollection<GrpcErrorContract>? Errors
);
