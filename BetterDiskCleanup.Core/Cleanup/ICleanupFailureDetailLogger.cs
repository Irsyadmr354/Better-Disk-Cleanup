namespace BetterDiskCleanup.Core.Cleanup;

public interface ICleanupFailureDetailLogger
{
    string LogFilePath { get; }

    void LogSessionStart(int itemCount);

    void LogFailure(CleanupFailureDetail detail);

    void LogSessionEnd(CleanupReport report);
}
