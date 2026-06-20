namespace BetterDiskCleanup.Core.Recovery;

public sealed class RecoverySessionSummary
{
    public required string SessionId { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public required DateTimeOffset ExpiresAtUtc { get; init; }
    public required RecoverySessionStatus Status { get; init; }
    public required int FileCount { get; init; }
    public required long TotalSizeBytes { get; init; }
}
