namespace BetterDiskCleanup.Core.StorageAnalyzer;

/// <summary>
/// A rectangle produced by the treemap layout algorithm, representing a single FolderNode.
/// Coordinates are in logical units relative to the bounding canvas (0,0 → Width,Height).
/// </summary>
public sealed class TreemapRect
{
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }

    /// <summary>
    /// The folder/file node this rectangle represents.
    /// </summary>
    public FolderNode Node { get; init; } = null!;

    /// <summary>
    /// The dominant file type category used to color this rectangle.
    /// For folders: the category with the largest byte count.
    /// For files: the category matching the file's extension.
    /// </summary>
    public StorageFileTypeCategory Category { get; init; }

    /// <summary>
    /// True when this rectangle is the aggregated "Others" bucket.
    /// </summary>
    public bool IsOthersBucket { get; init; }

    /// <summary>
    /// Percentage of the parent's total size this rectangle represents (0–100).
    /// </summary>
    public double Percentage { get; init; }
}
