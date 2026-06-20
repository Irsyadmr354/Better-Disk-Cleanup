using BetterDiskCleanup.Core.Analysis;
using BetterDiskCleanup.Core.Filesystem;
using BetterDiskCleanup.Core.Recovery;
using BetterDiskCleanup.Core.Safety;
using BetterDiskCleanup.Infrastructure.Recovery;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BetterDiskCleanup.Infrastructure.Scanning;

public sealed class TempFileScanner : ITempFileScanner
{
    private readonly IPathSafetyValidator _safetyValidator;
    private readonly IFileSystemGateway _fileSystem;
    private readonly IRecoveryOptions _recoveryOptions;
    private readonly ILogger<TempFileScanner> _logger;

    public TempFileScanner(
        IPathSafetyValidator safetyValidator,
        IFileSystemGateway fileSystem,
        IOptions<RecoveryOptions> recoveryOptions,
        ILogger<TempFileScanner> logger)
    {
        _safetyValidator = safetyValidator;
        _fileSystem = fileSystem;
        _recoveryOptions = new RecoveryOptionsAdapter(recoveryOptions);
        _logger = logger;
    }

    public Task<ScanResult> ScanAsync(
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => ScanCore(progress, cancellationToken), cancellationToken);
    }

    private ScanResult ScanCore(IProgress<ScanProgress>? progress, CancellationToken cancellationToken)
    {
        var scanRoots = GetTempScanRoots();
        var recoveryRoot = RecoveryPathHelper.GetRecoveryRoot(_recoveryOptions);
        var items = new List<ScanItem>();
        var warnings = new List<ScanWarning>();
        var folderPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var filesScanned = 0;
        var foldersScanned = 0;
        long bytesScanned = 0;

        foreach (var root in scanRoots)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_fileSystem.DirectoryExists(root))
            {
                _logger.LogWarning("Temp scan root does not exist: {Root}", root);
                continue;
            }

            var rootValidation = _safetyValidator.Validate(root);
            if (!rootValidation.IsAllowed)
            {
                _logger.LogWarning(
                    "Temp scan root rejected by safety validator: {Root}. Reason: {Reason}",
                    root,
                    rootValidation.Reason);
                continue;
            }

            folderPaths.Add(root);

            IEnumerable<string> directories;
            try
            {
                directories = _fileSystem.EnumerateDirectories(root)
                    .Where(directory => !RecoveryPathHelper.IsExcludedFromTempScan(directory, _recoveryOptions));
            }
            catch (Exception ex) when (IsAccessIssue(ex))
            {
                warnings.Add(new ScanWarning
                {
                    Path = root,
                    Message = $"Unable to enumerate directories: {ex.Message}"
                });
                directories = [];
            }

            foreach (var directory in directories)
            {
                cancellationToken.ThrowIfCancellationRequested();
                folderPaths.Add(directory);
            }

            IEnumerable<string> files;
            try
            {
                files = _fileSystem.EnumerateFiles(root)
                    .Where(filePath => !RecoveryPathHelper.IsExcludedFromTempScan(filePath, _recoveryOptions));
            }
            catch (Exception ex) when (IsAccessIssue(ex))
            {
                warnings.Add(new ScanWarning
                {
                    Path = root,
                    Message = $"Unable to enumerate files: {ex.Message}"
                });
                continue;
            }

            foreach (var filePath in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (RecoveryPathHelper.IsExcludedFromTempScan(filePath, _recoveryOptions))
                {
                    continue;
                }

                var validation = _safetyValidator.Validate(filePath);
                if (!validation.IsAllowed)
                {
                    continue;
                }

                try
                {
                    var size = _fileSystem.GetFileSize(filePath);
                    var lastModified = _fileSystem.GetLastWriteTimeUtc(filePath);

                    items.Add(new ScanItem
                    {
                        Path = filePath,
                        SizeBytes = size,
                        LastModifiedUtc = lastModified,
                        RiskLevel = validation.RiskLevel
                    });

                    filesScanned++;
                    bytesScanned += size;

                    progress?.Report(new ScanProgress
                    {
                        FilesScanned = filesScanned,
                        FoldersScanned = folderPaths.Count,
                        BytesScanned = bytesScanned,
                        CurrentPath = filePath
                    });
                }
                catch (Exception ex) when (IsAccessIssue(ex))
                {
                    warnings.Add(new ScanWarning
                    {
                        Path = filePath,
                        Message = $"Skipped locked or inaccessible file: {ex.Message}"
                    });
                }
            }
        }

        foldersScanned = folderPaths.Count;

        _logger.LogInformation(
            "Temp scan completed. Files: {FileCount}, Folders: {FolderCount}, Bytes: {TotalBytes}, Warnings: {WarningCount}, RecoveryRootExcluded: {RecoveryRoot}",
            items.Count,
            foldersScanned,
            bytesScanned,
            warnings.Count,
            recoveryRoot);

        return new ScanResult
        {
            FileCount = items.Count,
            FolderCount = foldersScanned,
            TotalSizeBytes = bytesScanned,
            Items = items,
            Warnings = warnings
        };
    }

    internal static IReadOnlyList<string> GetTempScanRoots()
    {
        var windowsTemp = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "Temp");

        return
        [
            Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            windowsTemp
        ];
    }

    private static bool IsAccessIssue(Exception exception) =>
        exception is UnauthorizedAccessException
        or DirectoryNotFoundException
        or FileNotFoundException
        or IOException;
}
