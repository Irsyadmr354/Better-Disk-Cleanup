namespace BetterDiskCleanup.Core.StorageAnalyzer;

/// <summary>
/// Categorizes files by their extension into broad storage types.
/// </summary>
public enum StorageFileTypeCategory
{
    Video,
    Audio,
    Image,
    Document,
    Archive,
    Executable,
    System,
    Other
}
