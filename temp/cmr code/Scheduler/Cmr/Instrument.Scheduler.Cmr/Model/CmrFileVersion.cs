namespace Instrument.Scheduler.Cmr.Model;
using ProtoBuf;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic, SkipConstructor = true)]
public class CmrFileVersion
{
    public CmrFileVersion(string semanticVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(semanticVersion);
        Parse(semanticVersion);
    }

    private void Parse(string semanticVersion)
    {
        if (Version.TryParse(semanticVersion, out var systemVersion))
        {
            Major = systemVersion.Major;
            Minor = systemVersion.Minor;
            Patch = systemVersion.Revision;
        }
        else
        {
            throw new ArgumentException("Invalid format for semantic version", nameof(semanticVersion));
        }
    }

    public int Major { get; set; }

    public int Minor { get; set; }

    public int Patch { get; set; }

    public override string ToString()
    {
        return $"{Major}.{Minor}.{Patch}";
    }
}