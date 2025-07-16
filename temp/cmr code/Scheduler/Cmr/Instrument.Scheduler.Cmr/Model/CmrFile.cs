namespace Instrument.Scheduler.Cmr.Model;

using System.Collections.Generic;
using ProtoBuf;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic, SkipConstructor = true)]
public class CmrFile
{
    public required IReadOnlyCollection<ICmrParameter> Parameters { get; set; }

    public required IReadOnlyCollection<CmrTestOrder> Orders { get; set; }

}
