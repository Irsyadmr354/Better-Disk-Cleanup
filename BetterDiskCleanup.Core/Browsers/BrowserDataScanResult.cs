using BetterDiskCleanup.Core.Analysis;

namespace BetterDiskCleanup.Core.Browsers;

public sealed class BrowserDataScanResult
{
    public required IReadOnlyList<BrowserProfile> Profiles { get; init; }
    public required IReadOnlyList<BrowserScanEntry> Entries { get; init; }
    public required long TotalSizeBytes { get; init; }
    public required IReadOnlyList<ScanWarning> Warnings { get; init; }

    /// <summary>
    /// Converts the browser scan entries into a standard <see cref="ScanResult"/>
    /// that can be fed into the existing <c>ICleanupSimulator</c> / <c>ICleanupExecutor</c> pipeline.
    /// </summary>
    public ScanResult ToScanResult()
    {
        var items = Entries
            .SelectMany(entry => entry.Files)
            .ToList();

        return new ScanResult
        {
            FileCount = items.Count,
            FolderCount = Entries.Count,
            TotalSizeBytes = items.Sum(item => item.SizeBytes),
            Items = items,
            Warnings = Warnings
        };
    }
}
