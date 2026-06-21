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
            // Keep the trailing backslash (e.g. "C:\") — without it,
            // Directory.EnumerateDirectories("C:") refers to the current
            // directory on the drive, NOT the root, resulting in 0 subdirs.
            return DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                .Select(d => d.RootDirectory.FullName)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enumerate drives.");
            return [];
        }
    }

    public async Task<LargeFileScanResult> ScanAsync(
        string rootPath,
        long thresholdBytes,
        IProgress<LargeFileScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await ScanCoreAsync(rootPath, thresholdBytes, progress, cancellationToken);
    }

    private async Task<LargeFileScanResult> ScanCoreAsync(
        string rootPath,
        long thresholdBytes,
        IProgress<LargeFileScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        var entries = new ConcurrentBag<LargeFileEntry>();
        var warnings = new ConcurrentBag<ScanWarning>();
        var filesFoundCounter = new int[1];
        var bytesScannedCounter = new long[1];
        var dirsScannedCounter = new int[1];

        if (!_fileSystem.DirectoryExists(rootPath))
        {
            warnings.Add(new ScanWarning
            {
                Path = rootPath,
                Message = "Root path does not exist."
            });
            return BuildResult(entries, warnings, 0);
        }

        _logger.LogInformation(
            "Starting large file scan. Root={Root}, Threshold={ThresholdBytes} bytes ({ThresholdMB} MB)",
            rootPath, thresholdBytes, thresholdBytes / (1024 * 1024));

        var maxDegree = Math.Min(Environment.ProcessorCount, 8);
        var level = 0;
        var totalDirsProcessed = 0;
        var totalDirsDenied = 0;

        // BFS directory traversal, processing in batches
        var batch = new List<string> { rootPath };
        var nextLevel = new List<string>();

        while (batch.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            nextLevel.Clear();
            level++;

            _logger.LogInformation("BFS Level {Level}: processing {Count} directories...", level, batch.Count);

            // Discover child directories for each directory in the current batch
            foreach (var dir in batch)
            {
                IReadOnlyList<string> childDirs;
                try
                {
                    // Materialize inside try-catch to capture lazy enumeration exceptions
                    childDirs = _fileSystem.EnumerateDirectoriesDirect(dir).ToList();
                }
                catch (Exception ex) when (IsAccessIssue(ex))
                {
                    totalDirsDenied++;
                    if (dir == rootPath)
                    {
                        warnings.Add(new ScanWarning
                        {
                            Path = rootPath,
                            Message = $"Unable to enumerate subdirectories: {ex.Message}"
                        });
                    }
                    _logger.LogDebug("Access denied enumerating: {Dir} ({Msg})", dir, ex.Message);
                    continue;
                }

                foreach (var childDir in childDirs)
                {
                    try
                    {
                        if (!ShouldSkipReparsePoint(childDir))
                        {
                            nextLevel.Add(childDir);
                        }
                    }
                    catch (Exception ex) when (IsAccessIssue(ex))
                    {
                        totalDirsDenied++;
                    }
                }
            }

            _logger.LogInformation("BFS Level {Level}: found {ChildCount} subdirectories, processing files in {DirCount} dirs",
                level, nextLevel.Count, batch.Count);

            // Process current batch of directories in parallel for files
            try
            {
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = maxDegree,
                    CancellationToken = cancellationToken
                };

                await Parallel.ForEachAsync(batch, parallelOptions, (directory, ct) =>
                {
                    ct.ThrowIfCancellationRequested();
                    ProcessDirectory(directory, thresholdBytes, entries, warnings,
                        filesFoundCounter, bytesScannedCounter, dirsScannedCounter, progress);
                    return ValueTask.CompletedTask;
                });
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (AggregateException ae) when (ae.InnerExceptions.All(e => e is OperationCanceledException))
            {
                break;
            }

            totalDirsProcessed += batch.Count;

            // Report progress after each BFS level
            progress?.Report(new LargeFileScanProgress
            {
                FilesFound = filesFoundCounter[0],
                BytesScanned = bytesScannedCounter[0],
                DirectoriesScanned = dirsScannedCounter[0],
                CurrentPath = $"Level {level}: {nextLevel.Count} subdirs queued"
            });

            // Move to next level
            batch.Clear();
            batch.AddRange(nextLevel);
        }

        _logger.LogInformation("BFS traversal complete. Levels={Levels}, TotalDirs={TotalDirs}, Denied={Denied}, FilesFound={Files}",
            level, totalDirsProcessed, totalDirsDenied, filesFoundCounter[0]);

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
        int[] dirsScanned,
        IProgress<LargeFileScanProgress>? progress)
    {
        IReadOnlyList<string> files;
        try
        {
            // Materialize inside try-catch to capture lazy enumeration exceptions
            files = _fileSystem.EnumerateFilesDirect(directory).ToList();
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
                    DirectoriesScanned = dirsScanned[0],
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

        Interlocked.Increment(ref dirsScanned[0]);
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
