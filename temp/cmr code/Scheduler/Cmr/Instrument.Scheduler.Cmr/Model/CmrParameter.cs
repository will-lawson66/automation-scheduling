namespace Instrument.Scheduler.Cmr.Model;
using ProtoBuf;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic, SkipConstructor = true)]
public class CmrParameter<T> : ICmrParameter
{
    public required T Value { get; set; }

    public required CmrFileParameterType FileParameterType { get; set; }

    object ICmrParameter.Value => Value;
}