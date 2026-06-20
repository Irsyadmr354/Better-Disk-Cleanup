namespace BetterDiskCleanup.Core.Recovery;

public sealed class RecoveryOptions
{
    public const string SectionName = "Recovery";

    public int RetentionDays { get; init; } = 30;

    public string StagingFolderName { get; init; } = "BetterDiskCleanup";
}
