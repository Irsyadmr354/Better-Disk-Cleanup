namespace BetterDiskCleanup.Core.Recovery;

public sealed class RecoveryStageResult
{
    public required bool Success { get; init; }
    public RecoveryManifestItem? Item { get; init; }
    public string? ErrorMessage { get; init; }

    public static RecoveryStageResult Succeeded(RecoveryManifestItem item) =>
        new() { Success = true, Item = item };

    public static RecoveryStageResult Failed(string errorMessage) =>
        new() { Success = false, ErrorMessage = errorMessage };
}
