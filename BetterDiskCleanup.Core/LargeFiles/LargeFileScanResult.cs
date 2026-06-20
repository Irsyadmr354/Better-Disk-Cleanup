using BetterDiskCleanup.Core.Analysis;

namespace BetterDiskCleanup.Core.LargeFiles;

public sealed class LargeFileScanResult
{
    public required IReadOnlyList<LargeFileEntry> Entries { get; init; }
    public required IReadOnlyList<ScanWarning> Warnings { get; init; }
    public required long TotalSizeBytes { get; init; }
}
