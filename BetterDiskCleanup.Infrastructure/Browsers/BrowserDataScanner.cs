using BetterDiskCleanup.Core.Analysis;
using BetterDiskCleanup.Core.Browsers;
using BetterDiskCleanup.Core.Filesystem;
using BetterDiskCleanup.Core.Safety;
using Microsoft.Extensions.Logging;

namespace BetterDiskCleanup.Infrastructure.Browsers;

public sealed class BrowserDataScanner : IBrowserDataScanner
{
    private readonly IFileSystemGateway _fileSystem;
    private readonly IPathSafetyValidator _safetyValidator;
    private readonly ILogger<BrowserDataScanner> _logger;

    public BrowserDataScanner(
        IFileSystemGateway fileSystem,
        IPathSafetyValidator safetyValidator,
        ILogger<BrowserDataScanner> logger)
    {
        _fileSystem = fileSystem;
        _safetyValidator = safetyValidator;
        _logger = logger;
    }

    public Task<BrowserDataScanResult> ScanAsync(
        IReadOnlyList<BrowserProfile> profiles,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => ScanCore(profiles, progress, cancellationToken), cancellationToken);
    }

    private BrowserDataScanResult ScanCore(
        IReadOnlyList<BrowserProfile> profiles,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        var entries = new List<BrowserScanEntry>();
        var warnings = new List<ScanWarning>();
        long totalSizeBytes = 0;
        var filesScanned = 0;

        foreach (var profile in profiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var dataLocations = GetDataLocations(profile);

            foreach (var (dataType, path, isDirectory) in dataLocations)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var validation = _safetyValidator.Validate(path);
                if (!validation.IsAllowed)
                {
                    warnings.Add(new ScanWarning
                    {
                        Path = path,
                        Message = $"Safety validation denied: {validation.Reason}"
                    });
                    continue;
                }

                if (!_fileSystem.DirectoryExists(path) && !_fileSystem.FileExists(path))
                {
                    continue;
                }

                var riskLevel = GetRiskLevel(dataType);
                var scanItems = new List<ScanItem>();
                long entrySize = 0;

                if (isDirectory)
                {
                    try
                    {
                        foreach (var file in _fileSystem.EnumerateFiles(path))
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            try
                            {
                                var size = _fileSystem.GetFileSize(file);
                                var lastModified = _fileSystem.GetLastWriteTimeUtc(file);
                                scanItems.Add(new ScanItem
                                {
                                    Path = file,
                                    SizeBytes = size,
                                    LastModifiedUtc = lastModified,
                                    RiskLevel = riskLevel
                                });
                                entrySize += size;
                                filesScanned++;
                            }
                            catch (Exception ex) when (IsAccessIssue(ex))
                            {
                                // Skip inaccessible file
                            }
                        }
                    }
                    catch (Exception ex) when (IsAccessIssue(ex))
                    {
                        warnings.Add(new ScanWarning
                        {
                            Path = path,
                            Message = $"Unable to enumerate: {ex.Message}"
                        });
                    }
                }
                else
                {
                    try
                    {
                        var size = _fileSystem.GetFileSize(path);
                        var lastModified = _fileSystem.GetLastWriteTimeUtc(path);
                        scanItems.Add(new ScanItem
                        {
                            Path = path,
                            SizeBytes = size,
                            LastModifiedUtc = lastModified,
                            RiskLevel = riskLevel
                        });
                        entrySize = size;
                        filesScanned++;
                    }
                    catch (Exception ex) when (IsAccessIssue(ex))
                    {
                        warnings.Add(new ScanWarning
                        {
                            Path = path,
                            Message = $"Unable to read: {ex.Message}"
                        });
                    }
                }

                if (scanItems.Count > 0)
                {
                    entries.Add(new BrowserScanEntry
                    {
                        BrowserName = profile.BrowserName,
                        ProfileName = profile.ProfileName,
                        DataType = dataType,
                        DisplayName = GetDisplayName(dataType),
                        SizeBytes = entrySize,
                        Files = scanItems
                    });
                    totalSizeBytes += entrySize;
                }

                progress?.Report(new ScanProgress
                {
                    FilesScanned = filesScanned,
                    FoldersScanned = entries.Count,
                    BytesScanned = totalSizeBytes,
                    CurrentPath = path
                });
            }
        }

        _logger.LogInformation(
            "Browser data scan completed. Entries: {EntryCount}, Files: {FileCount}, Bytes: {TotalBytes}, Warnings: {WarningCount}",
            entries.Count, filesScanned, totalSizeBytes, warnings.Count);

        return new BrowserDataScanResult
        {
            Profiles = profiles,
            Entries = entries,
            TotalSizeBytes = totalSizeBytes,
            Warnings = warnings
        };
    }

    internal static RiskLevel GetRiskLevel(BrowserDataType dataType) => dataType switch
    {
        BrowserDataType.Cache => RiskLevel.Safe,
        BrowserDataType.ServiceWorker => RiskLevel.Recommended,
        BrowserDataType.Temporary => RiskLevel.Safe,
        BrowserDataType.Sessions => RiskLevel.Recommended,
        BrowserDataType.Cookies => RiskLevel.Advanced,
        BrowserDataType.History => RiskLevel.Advanced,
        _ => RiskLevel.Advanced
    };

    private static string GetDisplayName(BrowserDataType dataType) => dataType switch
    {
        BrowserDataType.Cache => "Cache",
        BrowserDataType.Cookies => "Cookies",
        BrowserDataType.History => "Browsing History",
        BrowserDataType.Sessions => "Sessions",
        BrowserDataType.ServiceWorker => "Service Worker Cache",
        BrowserDataType.Temporary => "Temporary Files",
        _ => dataType.ToString()
    };

    private static IReadOnlyList<(BrowserDataType DataType, string Path, bool IsDirectory)> GetDataLocations(
        BrowserProfile profile)
    {
        if (profile.BrowserEngine == "Chromium")
        {
            return GetChromiumDataLocations(profile.ProfilePath);
        }

        if (profile.BrowserEngine == "Gecko")
        {
            return GetFirefoxDataLocations(profile.ProfilePath);
        }

        return [];
    }

    private static IReadOnlyList<(BrowserDataType DataType, string Path, bool IsDirectory)> GetChromiumDataLocations(
        string profilePath)
    {
        return
        [
            (BrowserDataType.Cache, Path.Combine(profilePath, "Cache", "Cache_Data"), true),
            (BrowserDataType.Cache, Path.Combine(profilePath, "Code Cache"), true),
            (BrowserDataType.Cache, Path.Combine(profilePath, "GPUCache"), true),
            (BrowserDataType.Cookies, Path.Combine(profilePath, "Network", "Cookies"), false),
            (BrowserDataType.History, Path.Combine(profilePath, "History"), false),
            (BrowserDataType.Sessions, Path.Combine(profilePath, "Sessions"), true),
            (BrowserDataType.Sessions, Path.Combine(profilePath, "Session Storage"), true),
            (BrowserDataType.ServiceWorker, Path.Combine(profilePath, "Service Worker", "CacheStorage"), true),
            (BrowserDataType.ServiceWorker, Path.Combine(profilePath, "Service Worker", "ScriptCache"), true),
            (BrowserDataType.Temporary, Path.Combine(profilePath, "blob_storage"), true),
        ];
    }

    private static IReadOnlyList<(BrowserDataType DataType, string Path, bool IsDirectory)> GetFirefoxDataLocations(
        string profilePath)
    {
        // Firefox stores cache under LocalAppData, but the profile path points to Roaming.
        // We derive the cache path from the profile folder name.
        var profileFolderName = Path.GetFileName(profilePath);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var cachePath = Path.Combine(localAppData, "Mozilla", "Firefox", "Profiles", profileFolderName, "cache2");

        return
        [
            (BrowserDataType.Cache, cachePath, true),
            (BrowserDataType.Cookies, Path.Combine(profilePath, "cookies.sqlite"), false),
            (BrowserDataType.History, Path.Combine(profilePath, "places.sqlite"), false),
            (BrowserDataType.Sessions, Path.Combine(profilePath, "sessionstore-backups"), true),
            (BrowserDataType.Temporary, Path.Combine(profilePath, "storage", "temporary"), true),
        ];
    }

    private static bool IsAccessIssue(Exception exception) =>
        exception is UnauthorizedAccessException
        or DirectoryNotFoundException
        or FileNotFoundException
        or IOException;
}
