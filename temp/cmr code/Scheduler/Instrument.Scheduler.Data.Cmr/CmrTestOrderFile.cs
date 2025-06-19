namespace Instrument.Scheduler.Data.Cmr
{
    /// <summary>
    /// CMR Test Order file that specifies the source of the CMR Data
    /// </summary>
    public class CmrTestOrderFile : ITestOrderFile
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
