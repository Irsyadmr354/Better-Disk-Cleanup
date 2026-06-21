namespace BetterDiskCleanup.Core.StorageAnalyzer;

/// <summary>
/// Progress report emitted during storage analysis.
/// </summary>
public sealed class StorageAnalyzerProgress
{
    public int DirectoriesScanned { get; init; }
    public int FilesScanned { get; init; }
    public long BytesScanned { get; init; }
    public string CurrentPath { get; init; } = string.Empty;
}
