namespace Instrument.Scheduler;

using System;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// The base exception to be used by the Execution Framework to ensure
/// exceptions specifically thrown by the Execution Framework are uniquely
/// identifiable.  
/// </summary>
[ExcludeFromCodeCoverage(
    Justification = "Inherits from the generic .NET Exception class with no additions to behavior.")]
public class SchedulerException : Exception
{
    /// <inheritdoc />
    public SchedulerException(string message)
        : base(message)
    {
    }

    /// <inheritdoc />
    public SchedulerException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

