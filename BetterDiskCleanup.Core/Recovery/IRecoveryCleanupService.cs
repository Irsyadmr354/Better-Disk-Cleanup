namespace BetterDiskCleanup.Core.Recovery;

public sealed class RecoveryPurgeResult
{
    public required IReadOnlyList<string> PurgedSessionIds { get; init; }
}

public interface IRecoveryCleanupService
{
    Task<RecoveryPurgeResult> PurgeExpiredSessionsAsync(CancellationToken cancellationToken = default);
}
