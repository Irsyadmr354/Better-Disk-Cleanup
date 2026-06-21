namespace BetterDiskCleanup.Core.StartupManager;

/// <summary>
/// Estimates the startup impact of an entry using transparent heuristics.
/// This is NOT a precise measurement like Task Manager's real data — it's a
/// best-effort classification based on file metadata and signature.
/// </summary>
public interface IStartupImpactEstimator
{
    /// <summary>
    /// Estimate impact for the given executable path.
    /// </summary>
    StartupImpactLevel Estimate(string filePath);
}
