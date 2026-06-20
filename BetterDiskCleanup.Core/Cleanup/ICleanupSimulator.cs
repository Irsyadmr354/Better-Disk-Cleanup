using BetterDiskCleanup.Core.Analysis;

namespace BetterDiskCleanup.Core.Cleanup;

public interface ICleanupSimulator
{
    Task<CleanupSimulationResult> SimulateAsync(
        ScanResult scanResult,
        CancellationToken cancellationToken = default);
}
