namespace BetterDiskCleanup.Core.StartupManager;

/// <summary>
/// Scans registry, startup folders, and scheduled tasks to discover startup entries.
/// </summary>
public interface IStartupScanner
{
    /// <summary>
    /// Discovers all startup entries from all sources.
    /// </summary>
    Task<IReadOnlyList<StartupEntry>> ScanAllAsync();
}
