using System.Security;
using BetterDiskCleanup.Core.StorageAnalyzer;
using Microsoft.Extensions.Logging;

namespace BetterDiskCleanup.Infrastructure.StorageAnalyzer;

/// <summary>
/// BFS-based folder size analyzer that builds an in-memory tree.
/// Reports progress periodically and supports cancellation.
/// </summary>
public sealed class FolderSizeAnalyzer : IStorageAnalyzer
{
    private readonly ILogger<FolderSizeAnalyzer> _logger;

    /// <summary>
    /// Number of directories processed between progress reports.
    /// </summary>
    private const int ProgressReportInterval = 50;

    public FolderSizeAnalyzer(ILogger<FolderSizeAnalyzer> logger)
    {
        _logger = logger;
    }

    public async Task<FolderNode> AnalyzeAsync(
        string rootPath,
        IProgress<StorageAnalyzerProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rootPath);

        // Normalize the path
        rootPath = Path.GetFullPath(rootPath);

        var root = new FolderNode
        {
            Name = Path.GetFileName(rootPath) ?? rootPath,
            FullPath = rootPath,
            IsFile = false
        };

        return await Task.Run(() =>
        {
            int dirsScanned = 0;
            int filesScanned = 0;
            long bytesScanned = 0;

            // BFS queue: (node, directory path)
            var queue = new Queue<(FolderNode Node, string DirPath)>();
            queue.Enqueue((root, rootPath));

            while (queue.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var (node, dirPath) = queue.Dequeue();
                dirsScanned++;

                try
                {
                    // Enumerate files in the current directory
                    try
                    {
                        foreach (var filePath in Directory.EnumerateFiles(dirPath))
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            try
                            {
                                var fi = new FileInfo(filePath);
                                if (!fi.Exists) continue;

                                var size = fi.Length;
                                var ext = fi.Extension;
                                var category = FileTypeClassifier.Classify(ext);

                                var fileNode = new FolderNode
                                {
                                    Name = fi.Name,
                                    FullPath = filePath,
                                    SizeBytes = size,
                                    FileCount = 1,
                                    IsFile = true,
                                    Extension = ext,
                                    Parent = node
                                };

                                node.Children.Add(fileNode);
                                filesScanned++;
                                bytesScanned += size;

                                // Aggregate file type breakdown up the tree
                                AggregateFileType(node, category, size);
                            }
                            catch (Exception ex) when (ex is not OperationCanceledException)
                            {
                                // Skip inaccessible files
                            }
                        }
                    }
                    catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or SecurityException)
                    {
                        _logger.LogDebug("Access denied enumerating files in {Path}: {Message}", dirPath, ex.Message);
                    }

                    // Enumerate subdirectories
                    try
                    {
                        foreach (var subDir in Directory.EnumerateDirectories(dirPath))
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            var dirInfo = new DirectoryInfo(subDir);
                            var childNode = new FolderNode
                            {
                                Name = dirInfo.Name,
                                FullPath = subDir,
                                IsFile = false,
                                Parent = node
                            };

                            node.Children.Add(childNode);
                            queue.Enqueue((childNode, subDir));
                        }
                    }
                    catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or SecurityException)
                    {
                        _logger.LogDebug("Access denied enumerating directories in {Path}: {Message}", dirPath, ex.Message);
                    }

                    // Report progress periodically
                    if (dirsScanned % ProgressReportInterval == 0)
                    {
                        progress?.Report(new StorageAnalyzerProgress
                        {
                            DirectoriesScanned = dirsScanned,
                            FilesScanned = filesScanned,
                            BytesScanned = bytesScanned,
                            CurrentPath = dirPath
                        });
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
            }

            // Final pass: compute cumulative sizes bottom-up (BFS doesn't give us this naturally)
            ComputeCumulativeSizes(root);

            // Report final progress
            progress?.Report(new StorageAnalyzerProgress
            {
                DirectoriesScanned = dirsScanned,
                FilesScanned = filesScanned,
                BytesScanned = bytesScanned,
                CurrentPath = rootPath
            });

            return root;
        }, cancellationToken);
    }

    /// <summary>
    /// Propagates a file type category + size up from a node to all its ancestors,
    /// building the FileTypeBreakdown dictionary at each directory level.
    /// </summary>
    private static void AggregateFileType(FolderNode node, StorageFileTypeCategory category, long size)
    {
        var current = node;
        while (current != null)
        {
            current.FileTypeBreakdown.TryGetValue(category, out var existing);
            current.FileTypeBreakdown[category] = existing + size;
            current = current.Parent;
        }
    }

    /// <summary>
    /// Post-order traversal to compute cumulative SizeBytes and FileCount for directory nodes.
    /// File nodes already have their values set during enumeration.
    /// </summary>
    private static void ComputeCumulativeSizes(FolderNode node)
    {
        if (node.IsFile) return;

        long totalSize = 0;
        int totalFiles = 0;

        foreach (var child in node.Children)
        {
            ComputeCumulativeSizes(child);
            totalSize += child.SizeBytes;
            totalFiles += child.FileCount;
        }

        node.SizeBytes = totalSize;
        node.FileCount = totalFiles;
    }
}
