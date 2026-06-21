namespace BetterDiskCleanup.Core.Duplicates;

public sealed class DuplicateScanProgress
{
    public int TotalFilesFound { get; init; }
    public int SameSizeCandidates { get; init; }
    public int FilesHashed { get; init; }
    public int DuplicateGroupsFound { get; init; }
    public string? CurrentOperation { get; init; }
}
