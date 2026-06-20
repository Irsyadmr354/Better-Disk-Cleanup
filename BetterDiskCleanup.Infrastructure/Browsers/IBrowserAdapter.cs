using BetterDiskCleanup.Core.Browsers;

namespace BetterDiskCleanup.Infrastructure.Browsers;

/// <summary>
/// Per-engine adapter that knows how to detect a specific browser family
/// and enumerate its profiles from the filesystem.
/// </summary>
internal interface IBrowserAdapter
{
    string BrowserName { get; }
    string Engine { get; }
    string ProcessName { get; }

    /// <summary>
    /// Returns all detected profiles, or an empty list if the browser is not installed.
    /// </summary>
    IReadOnlyList<BrowserProfile> DetectProfiles();
}
