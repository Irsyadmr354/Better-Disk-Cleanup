using BetterDiskCleanup.Core.Analysis;

namespace BetterDiskCleanup.Core.Duplicates;

public sealed class DuplicateScanResult
{
    public required IReadOnlyList<DuplicateGroup> Groups { get; init; }
    public required IReadOnlyList<ScanWarning> Warnings { get; init; }
    public required long TotalRecoverableBytes { get; init; }
    public required int TotalDuplicateFiles { get; init; }
}
