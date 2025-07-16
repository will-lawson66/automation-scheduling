namespace Instrument.Scheduler.Cmr.Grpc.Message;

using System;
using System.Collections.Generic;
using Instrument.Grpc;
using Instrument.Scheduler.Cmr.Execution;
using ProtoBuf;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic, SkipConstructor = true)]
public sealed record PrepareCmrFileResponse(
    Guid RequestId,
    CmrExecutionPlan? ExecutionPlan,
    IReadOnlyCollection<GrpcErrorContract>? Errors
);
