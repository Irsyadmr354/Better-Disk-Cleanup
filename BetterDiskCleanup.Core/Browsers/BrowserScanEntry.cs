using BetterDiskCleanup.Core.Analysis;

namespace BetterDiskCleanup.Core.Browsers;

public sealed class BrowserScanEntry
{
    public required string BrowserName { get; init; }
    public required string ProfileName { get; init; }
    public required BrowserDataType DataType { get; init; }
    public required string DisplayName { get; init; }
    public required long SizeBytes { get; init; }
    public required IReadOnlyList<ScanItem> Files { get; init; }
}
