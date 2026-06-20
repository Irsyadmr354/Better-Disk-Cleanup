namespace BetterDiskCleanup.Core.Cleanup;

public sealed class CleanupReport
{
    public required DateTimeOffset StartedAt { get; init; }
    public required DateTimeOffset CompletedAt { get; init; }
    public required int FilesDeleted { get; init; }
    public required long SpaceRecoveredBytes { get; init; }
    public required IReadOnlyList<CleanupMessage> Warnings { get; init; }
    public required IReadOnlyList<CleanupMessage> SkippedInUse { get; init; }
    public required IReadOnlyList<CleanupMessage> Errors { get; init; }
    public string? RecoverySessionId { get; init; }
}
