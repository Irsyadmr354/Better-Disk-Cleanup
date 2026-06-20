using BetterDiskCleanup.Core.Analysis;

namespace BetterDiskCleanup.Core.Cleanup;

public interface ICleanupExecutor
{
    Task<CleanupReport> ExecuteAsync(
        ScanResult scanResult,
        CancellationToken cancellationToken = default);
}
