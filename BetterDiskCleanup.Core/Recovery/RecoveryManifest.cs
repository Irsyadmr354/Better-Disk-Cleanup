namespace BetterDiskCleanup.Core.Recovery;

public sealed record RecoveryManifest
{
    public required string SessionId { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public required DateTimeOffset ExpiresAtUtc { get; init; }
    public required RecoverySessionStatus Status { get; init; }
    public required IReadOnlyList<RecoveryManifestItem> Items { get; init; }
}
