namespace BetterDiskCleanup.Core.LargeFiles;

public sealed class LargeFileScanProgress
{
    public int FilesFound { get; init; }
    public long BytesScanned { get; init; }
    public int DirectoriesScanned { get; init; }
    public string? CurrentPath { get; init; }
}
