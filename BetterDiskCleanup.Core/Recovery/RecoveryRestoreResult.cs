namespace BetterDiskCleanup.Core.Recovery;

public sealed class RecoveryRestoreResult
{
    public required string SessionId { get; init; }
    public required IReadOnlyList<RecoveryRestoreItemResult> Items { get; init; }
}
