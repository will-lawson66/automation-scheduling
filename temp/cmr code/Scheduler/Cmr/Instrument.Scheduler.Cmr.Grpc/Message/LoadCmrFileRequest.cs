namespace Instrument.Scheduler.Cmr.Grpc.Message;

using ProtoBuf;


/// <summary>
/// A gRPC request for persisting a cmr file into a known location
/// Paired with <see cref="LoadCmrFileResponse"/>.
/// </summary>
/// <param name="FileName">
/// A human readable key that uniquely identifies the file resource.
/// This is usually the file name
/// </param>
/// <param name="Path">
/// The file path to load the script from. 
/// </param>
/// <param name="Description">
/// A description of the file.
/// </param>
/// <param name="HelpText">
/// Additional help information about the cmr file.
/// </param>
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic, SkipConstructor = true)]
public sealed record LoadCmrFileRequest(
    string FileName,
    string CmrContent
);
