namespace BetterDiskCleanup.Core.Analysis;

public sealed class ScanProgress
{
    public int FilesScanned { get; init; }
    public int FoldersScanned { get; init; }
    public long BytesScanned { get; init; }
    public string? CurrentPath { get; init; }
}
