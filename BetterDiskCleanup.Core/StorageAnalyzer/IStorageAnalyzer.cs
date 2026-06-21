namespace BetterDiskCleanup.Core.StorageAnalyzer;

/// <summary>
/// Scans a directory tree and builds an in-memory <see cref="FolderNode"/> hierarchy
/// with cumulative sizes, file counts, and per-category breakdowns.
/// </summary>
public interface IStorageAnalyzer
{
    /// <summary>
    /// Analyzes the directory at <paramref name="rootPath"/>, building a full
    /// folder tree with size information.
    /// </summary>
    /// <param name="rootPath">Absolute path to the root directory or drive (e.g. "C:\").</param>
    /// <param name="progress">Receives periodic progress reports during the scan.</param>
    /// <param name="cancellationToken">Allows the caller to cancel the scan.</param>
    /// <returns>The root <see cref="FolderNode"/> representing <paramref name="rootPath"/>.</returns>
    Task<FolderNode> AnalyzeAsync(
        string rootPath,
        IProgress<StorageAnalyzerProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
