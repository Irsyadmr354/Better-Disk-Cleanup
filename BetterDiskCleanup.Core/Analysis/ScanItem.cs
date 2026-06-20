using BetterDiskCleanup.Core.Safety;

namespace BetterDiskCleanup.Core.Analysis;

public sealed class ScanItem
{
    public required string Path { get; init; }
    public required long SizeBytes { get; init; }
    public required DateTime LastModifiedUtc { get; init; }
    public required RiskLevel RiskLevel { get; init; }
}
