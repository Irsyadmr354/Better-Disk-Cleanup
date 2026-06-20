namespace BetterDiskCleanup.Core.Recovery;

public interface IRecoveryService
{
    IReadOnlyList<RecoverySessionSummary> ListSessions();

    RecoveryManifest? GetSessionManifest(string sessionId);

    Task<RecoveryRestoreResult> RestoreSessionAsync(
        string sessionId,
        RestoreConflictPolicy conflictPolicy = RestoreConflictPolicy.Skip,
        CancellationToken cancellationToken = default);

    Task<RecoveryRestoreItemResult> RestoreItemAsync(
        string sessionId,
        string itemId,
        RestoreConflictPolicy conflictPolicy = RestoreConflictPolicy.Skip,
        CancellationToken cancellationToken = default);

    Task PurgeSessionAsync(string sessionId, CancellationToken cancellationToken = default);
}
