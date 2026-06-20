namespace BetterDiskCleanup.Core.Analysis;

public sealed class ScanResult
{
    public required int FileCount { get; init; }
    public required int FolderCount { get; init; }
    public required long TotalSizeBytes { get; init; }
    public required IReadOnlyList<ScanItem> Items { get; init; }
    public required IReadOnlyList<ScanWarning> Warnings { get; init; }
}
