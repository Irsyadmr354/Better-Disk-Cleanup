namespace BetterDiskCleanup.Core.Recovery;

public sealed record RecoveryManifestItem
{
    public required string ItemId { get; init; }
    public required string OriginalPath { get; init; }
    public required string StagedPath { get; init; }
    public required long SizeBytes { get; init; }
    public required DateTimeOffset StagedAtUtc { get; init; }
    public required string Sha256Hash { get; init; }
    public RecoveryItemStatus Status { get; init; } = RecoveryItemStatus.Staged;
}
