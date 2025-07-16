namespace Instrument.Scheduler.Cmr.Parser
{
    using Instrument.Scheduler.Cmr;

    /// <summary>
    /// CMR Test Order file that specifies the source of the CMR Data
    /// </summary>
    public class CmrTestOrderFile : ICmrFileSource
    {
        public const string DefaultCmrFileDelimiter = ";";

        public CmrTestOrderFile(string delimiter, FileInfo sourceFileInformation)
        {
            Delimiter = delimiter;
            SourceFile = sourceFileInformation;
        }

        public CmrTestOrderFile(FileInfo sourceFileInformation) : this(DefaultCmrFileDelimiter, sourceFileInformation)
        {

        }

        public FileInfo SourceFile { get; }

        public string Delimiter { get; set; }
    }
}
