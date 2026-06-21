namespace BetterDiskCleanup.Core.StorageAnalyzer;

/// <summary>
/// Represents a node in the folder tree — either a directory or a file.
/// For directories, SizeBytes is the cumulative size (own files + all children recursively).
/// </summary>
public sealed class FolderNode
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;

    /// <summary>
    /// Total size in bytes. For directories: sum of all descendant file sizes.
    /// For files: the file's own size.
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// Total number of files contained (recursive for directories, 1 for files).
    /// </summary>
    public int FileCount { get; set; }

    /// <summary>
    /// True if this node represents a file rather than a directory.
    /// </summary>
    public bool IsFile { get; set; }

    /// <summary>
    /// File extension (including the dot), e.g. ".mp4". Empty for directories.
    /// </summary>
    public string Extension { get; set; } = string.Empty;

    /// <summary>
    /// Child nodes (subdirectories and files). Empty for file nodes.
    /// </summary>
    public List<FolderNode> Children { get; set; } = [];

    /// <summary>
    /// Aggregated size per file type category (only populated on directory nodes).
    /// </summary>
    public Dictionary<StorageFileTypeCategory, long> FileTypeBreakdown { get; set; } = [];

    /// <summary>
    /// Reference to parent node, null for the root.
    /// </summary>
    public FolderNode? Parent { get; set; }
}
