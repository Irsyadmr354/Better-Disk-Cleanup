namespace BetterDiskCleanup.Core.Recovery;

public sealed class RecoveryRestoreItemResult
{
    public required string ItemId { get; init; }
    public required string OriginalPath { get; init; }
    public required bool Restored { get; init; }
    public required bool Skipped { get; init; }
    public required bool Renamed { get; init; }
    public string? RestoredPath { get; init; }
    public string? Message { get; init; }
}
