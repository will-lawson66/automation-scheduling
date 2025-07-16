namespace Instrument.Scheduler.Cmr
{
    /// <summary>
    /// File that contains test order information in a format that
    /// a <see cref="ICmrFileParser" is aware of how to parse the source file retrieve T/>
    /// </summary>
    public interface ICmrFileSource
    {
        /// <summary>
        /// File information
        /// </summary>
        FileInfo SourceFile { get; }

        /// <summary>
        /// Delimiter for source file
        /// </summary>
        string Delimiter { get; set; }
    }
}
