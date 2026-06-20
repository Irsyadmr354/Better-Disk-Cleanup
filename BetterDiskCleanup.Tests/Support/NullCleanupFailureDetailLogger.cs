using BetterDiskCleanup.Core.Cleanup;

namespace BetterDiskCleanup.Tests.Support;

public sealed class NullCleanupFailureDetailLogger : ICleanupFailureDetailLogger
{
    public string LogFilePath { get; } = "null-cleanup-detail.log";

    public void LogSessionStart(int itemCount)
    {
    }

    public void LogFailure(CleanupFailureDetail detail)
    {
    }

    public void LogSessionEnd(CleanupReport report)
    {
    }
}
