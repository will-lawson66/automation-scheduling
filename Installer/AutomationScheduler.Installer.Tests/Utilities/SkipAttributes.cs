using System;
using Xunit;

namespace AutomationScheduler.Installer.Tests.Utilities
{
    /// <summary>
    /// Provides conditional test execution based on runtime conditions
    /// </summary>
    public static class Skip
    {
        public static void If(bool condition, string reason)
        {
            if (condition)
            {
                throw new SkipException(reason);
            }
        }

        public static void IfNot(bool condition, string reason)
        {
            If(!condition, reason);
        }
    }

    /// <summary>
    /// Custom attribute for tests that can be skipped based on conditions
    /// </summary>
    public class SkippableFactAttribute : FactAttribute
    {
        public override string Skip
        {
            get => base.Skip;
            set => base.Skip = value;
        }
    }

    /// <summary>
    /// Custom attribute for theory tests that can be skipped
    /// </summary>
    public class SkippableTheoryAttribute : TheoryAttribute
    {
        public override string Skip
        {
            get => base.Skip;
            set => base.Skip = value;
        }
    }

    /// <summary>
    /// Exception thrown when a test should be skipped
    /// </summary>
    public class SkipException : Exception
    {
        public SkipException(string reason) : base(reason) { }
    }
}