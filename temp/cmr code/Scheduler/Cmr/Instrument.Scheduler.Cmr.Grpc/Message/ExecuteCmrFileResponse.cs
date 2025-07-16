namespace Instrument.Scheduler.Cmr.Grpc.Message;

using System;
using System.Collections.Generic;
using Instrument.Grpc;
using ProtoBuf;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic, SkipConstructor = true)]
public sealed record ExecuteCmrFileResponse(
    Guid RequestId,
    IReadOnlyCollection<GrpcErrorContract>? Errors
);