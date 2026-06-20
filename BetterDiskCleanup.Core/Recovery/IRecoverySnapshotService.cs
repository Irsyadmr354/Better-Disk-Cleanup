namespace BetterDiskCleanup.Core.Recovery;

public interface IRecoverySnapshotService
{
    string BeginSession();

    RecoveryStageResult StageFile(string sessionId, string originalPath);

    void FinalizeSession(string sessionId, IReadOnlyList<RecoveryManifestItem> stagedItems);

    string GetRecoveryRootPath();
}
