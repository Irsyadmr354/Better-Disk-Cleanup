using System.Collections.Concurrent;
using BetterDiskCleanup.Core.Analysis;
using BetterDiskCleanup.Core.Filesystem;
using BetterDiskCleanup.Core.LargeFiles;
using Microsoft.Extensions.Logging;

namespace BetterDiskCleanup.Infrastructure.LargeFiles;

public sealed class LargeFileScanner : ILargeFileScanner
{
    private readonly IFileSystemGateway _fileSystem;
    private readonly ILogger<LargeFileScanner> _logger;

    internal static readonly Dictionary<string, FileCategory> ExtensionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Video
        { ".mp4", FileCategory.Video },
        { ".mkv", FileCategory.Video },
        { ".avi", FileCategory.Video },
        { ".mov", FileCategory.Video },
        { ".wmv", FileCategory.Video },
        { ".flv", FileCategory.Video },
        { ".webm", FileCategory.Video },
        // Disk Image
        { ".iso", FileCategory.DiskImage },
        { ".vhd", FileCategory.DiskImage },
        { ".vhdx", FileCategory.DiskImage },
        { ".img", FileCategory.DiskImage },
        // Archive
        { ".zip", FileCategory.Archive },
        { ".rar", FileCategory.Archive },
        { ".7z", FileCategory.Archive },
        { ".tar", FileCategory.Archive },
        { ".gz", FileCategory.Archive },
        // Document
        { ".pdf", FileCategory.Document },
        { ".doc", FileCategory.Document },
        { ".docx", FileCategory.Document },
        { ".xls", FileCategory.Document },
        { ".xlsx", FileCategory.Document },
        { ".ppt", FileCategory.Document },
        { ".pptx", FileCategory.Document },
    };

    public LargeFileScanner(
        IFileSystemGateway fileSystem,
        ILogger<LargeFileScanner> logger)
    {
        _fileSystem = fileSystem;
        _logger = logger;
    }

    public IReadOnlyList<string> GetAvailableDrives()
    {
        try
        {
            return DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                .Select(d => d.RootDirectory.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enumerate drives.");
            return [];
        }
    }

    public Task<LargeFileScanResult> ScanAsync(
        string rootPath,
        long thresholdBytes,
        IProgress<LargeFileScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => ScanCore(rootPath, thresholdBytes, progress, cancellationToken), cancellationToken);
    }

    private LargeFileScanResult ScanCore(
        string rootPath,
        long thresholdBytes,
        IProgress<LargeFileScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        var entries = new ConcurrentBag<LargeFileEntry>();
        var warnings = new ConcurrentBag<ScanWarning>();
        var filesFoundCounter = new int[1];
        var bytesScannedCounter = new long[1];

        if (!_fileSystem.DirectoryExists(rootPath))
        {
            warnings.Add(new ScanWarning
            {
                Path = rootPath,
                Message = "Root path does not exist."
            });
            return BuildResult(entries, warnings, 0);
        }

        var maxDegree = Math.Min(Environment.ProcessorCount, 8);

        // BFS directory traversal, processing in batches
        var batch = new List<string> { rootPath };
        var nextLevel = new List<string>();

        while (batch.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            nextLevel.Clear();

            // Discover child directories for each directory in the current batch
            foreach (var dir in batch)
            {
                IEnumerable<string> childDirs;
                try
                {
                    childDirs = _fileSystem.EnumerateDirectoriesDirect(dir);
                }
                catch (Exception ex) when (IsAccessIssue(ex))
                {
                    if (dir == rootPath)
                    {
                        warnings.Add(new ScanWarning
                        {
                            Path = rootPath,
                            Message = $"Unable to enumerate subdirectories: {ex.Message}"
                        });
                    }
                    continue;
                }

                foreach (var childDir in childDirs)
                {
                    if (!ShouldSkipReparsePoint(childDir))
                    {
                        nextLevel.Add(childDir);
                    }
                }
            }

            // Process current batch of directories in parallel for files
            try
            {
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = maxDegree,
                    CancellationToken = cancellationToken
                };

                Parallel.ForEachAsync(batch, parallelOptions, (directory, ct) =>
                {
                    ct.ThrowIfCancellationRequested();
                    ProcessDirectory(directory, thresholdBytes, entries, warnings,
                        filesFoundCounter, bytesScannedCounter, progress);
                    return ValueTask.CompletedTask;
                }).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (AggregateException ae) when (ae.InnerExceptions.All(e => e is OperationCanceledException))
            {
                break;
            }

            // Move to next level
            batch.Clear();
            batch.AddRange(nextLevel);
        }

        var totalSize = entries.Sum(e => e.SizeBytes);
        _logger.LogInformation(
            "Large file scan completed. Root={Root}, Threshold={Threshold}, Files={FileCount}, Bytes={TotalBytes}, Warnings={WarningCount}",
            rootPath, thresholdBytes, entries.Count, totalSize, warnings.Count);

        return BuildResult(entries, warnings, totalSize);
    }

    private void ProcessDirectory(
        string directory,
        long thresholdBytes,
        ConcurrentBag<LargeFileEntry> entries,
        ConcurrentBag<ScanWarning> warnings,
        int[] filesFound,
        long[] bytesScanned,
        IProgress<LargeFileScanProgress>? progress)
    {
        IEnumerable<string> files;
        try
        {
            files = _fileSystem.EnumerateFilesDirect(directory);
        }
        catch (Exception ex) when (IsAccessIssue(ex))
        {
            warnings.Add(new ScanWarning
            {
                Path = directory,
                Message = $"Access denied or unavailable: {ex.Message}"
            });
            return;
        }

        foreach (var filePath in files)
        {
            try
            {
                var size = _fileSystem.GetFileSize(filePath);
                if (size < thresholdBytes)
                {
                    continue;
                }

                var extension = Path.GetExtension(filePath);
                var fileName = Path.GetFileName(filePath);
                var lastModified = _fileSystem.GetLastWriteTimeUtc(filePath);

                var entry = new LargeFileEntry
                {
                    Path = filePath,
                    FileName = fileName,
                    Extension = extension,
                    Category = CategorizeFile(extension),
                    SizeBytes = size,
                    LastModifiedUtc = lastModified
                };

                entries.Add(entry);
                var count = Interlocked.Increment(ref filesFound[0]);
                var total = Interlocked.Add(ref bytesScanned[0], size);

                progress?.Report(new LargeFileScanProgress
                {
                    FilesFound = count,
                    BytesScanned = total,
                    CurrentPath = filePath
                });
            }
            catch (Exception ex) when (IsAccessIssue(ex))
            {
                warnings.Add(new ScanWarning
                {
                    Path = filePath,
                    Message = $"Skipped inaccessible file: {ex.Message}"
                });
            }
        }
    }

    private bool ShouldSkipReparsePoint(string directoryPath)
    {
        try
        {
            return (_fileSystem.GetAttributes(directoryPath) & FileAttributes.ReparsePoint) != 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return true;
        }
        catch
        {
            // For test doubles or gateways that don't support attributes on directories,
            // assume the directory is safe to traverse.
            return false;
        }
    }

    internal static FileCategory CategorizeFile(string extension)
    {
        if (string.IsNullOrEmpty(extension))
        {
            return FileCategory.Other;
        }

        return ExtensionMap.TryGetValue(extension, out var category) ? category : FileCategory.Other;
    }

    private static LargeFileScanResult BuildResult(
        IEnumerable<LargeFileEntry> entries,
        IEnumerable<ScanWarning> warnings,
        long totalSizeBytes)
    {
        var entryList = entries.OrderByDescending(e => e.SizeBytes).ToList();
        return new LargeFileScanResult
        {
            Entries = entryList,
            Warnings = warnings.ToList(),
            TotalSizeBytes = totalSizeBytes
        };
    }

    private static bool IsAccessIssue(Exception exception) =>
        exception is UnauthorizedAccessException
        or DirectoryNotFoundException
        or FileNotFoundException
        or IOException;
}
