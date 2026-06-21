namespace BetterDiskCleanup.Core.StorageAnalyzer;

/// <summary>
/// Aggregates file sizes by storage file type category.
/// </summary>
public sealed class FileTypeAggregation
{
    private readonly Dictionary<StorageFileTypeCategory, long> _bytes = [];

    /// <summary>
    /// Returns a read-only view of the current aggregation.
    /// </summary>
    public IReadOnlyDictionary<StorageFileTypeCategory, long> Bytes => _bytes;

    /// <summary>
    /// Adds bytes to the specified category.
    /// </summary>
    public void Add(StorageFileTypeCategory category, long sizeBytes)
    {
        _bytes.TryGetValue(category, out var current);
        _bytes[category] = current + sizeBytes;
    }

    /// <summary>
    /// Merges another aggregation into this one.
    /// </summary>
    public void Merge(FileTypeAggregation other)
    {
        foreach (var (cat, bytes) in other._bytes)
        {
            Add(cat, bytes);
        }
    }

    /// <summary>
    /// Returns the total bytes across all categories.
    /// </summary>
    public long TotalBytes => _bytes.Values.Sum();

    /// <summary>
    /// Returns a snapshot of the current breakdown as a plain dictionary.
    /// </summary>
    public Dictionary<StorageFileTypeCategory, long> ToDictionary() => new(_bytes);
}
