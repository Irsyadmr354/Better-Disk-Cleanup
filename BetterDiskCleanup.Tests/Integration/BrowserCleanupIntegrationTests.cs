using BetterDiskCleanup.Core.Analysis;
using BetterDiskCleanup.Core.Browsers;
using BetterDiskCleanup.Core.Cleanup;
using BetterDiskCleanup.Core.Recovery;
using BetterDiskCleanup.Core.Safety;
using BetterDiskCleanup.Infrastructure.Browsers;
using BetterDiskCleanup.Infrastructure.Filesystem;
using BetterDiskCleanup.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace BetterDiskCleanup.Tests.Integration;

public sealed class BrowserCleanupIntegrationTests : IDisposable
{
    private readonly IsolatedTestRun _isolatedRun;
    private readonly ServiceProvider _serviceProvider;

    public BrowserCleanupIntegrationTests()
    {
        _isolatedRun = new IsolatedTestRun("brc-int");
        _serviceProvider = _isolatedRun.CreateServiceProvider();
    }

    [Fact]
    public async Task BrowserCacheCleanup_GoesThroughRecoveryAndCanBeRestored()
    {
        // Arrange: create fake browser cache files under the isolated data directory
        var cacheDir = Path.Combine(_isolatedRun.DataDirectory, "Cache", "Cache_Data");
        Directory.CreateDirectory(cacheDir);

        const int fileCount = 3;
        const int fileSize = 256;
        var originalFiles = new Dictionary<string, byte[]>();

        for (var i = 0; i < fileCount; i++)
        {
            var filePath = Path.Combine(cacheDir, $"cache_{i}.tmp");
            var content = new byte[fileSize];
            new Random(i).NextBytes(content);
            await File.WriteAllBytesAsync(filePath, content);
            originalFiles[filePath] = content;
        }

        // Build ScanItems representing browser cache entries
        var scanItems = originalFiles.Keys.Select(path => new ScanItem
        {
            Path = path,
            SizeBytes = fileSize,
            LastModifiedUtc = DateTime.UtcNow,
            RiskLevel = RiskLevel.Safe
        }).ToList();

        var scanResult = new ScanResult
        {
            FileCount = scanItems.Count,
            FolderCount = 1,
            TotalSizeBytes = scanItems.Sum(i => i.SizeBytes),
            Items = scanItems,
            Warnings = []
        };

        // Act: run through cleanup executor (which uses recovery staging)
        var executor = _serviceProvider.GetRequiredService<ICleanupExecutor>();
        var report = await executor.ExecuteAsync(scanResult);

        // Assert: files deleted, recovery session created
        Assert.Equal(fileCount, report.FilesDeleted);
        Assert.Equal(fileCount * fileSize, report.SpaceRecoveredBytes);
        Assert.NotNull(report.RecoverySessionId);
        Assert.Empty(report.Errors);

        // Verify files are gone from disk
        foreach (var filePath in originalFiles.Keys)
        {
            Assert.False(File.Exists(filePath), $"File should be deleted: {filePath}");
        }

        // Act: restore from recovery
        var recoveryService = _serviceProvider.GetRequiredService<IRecoveryService>();
        var restoreResult = await recoveryService.RestoreSessionAsync(report.RecoverySessionId);

        // Assert: files restored
        Assert.Equal(fileCount, restoreResult.Items.Count(item => item.Restored));

        foreach (var (filePath, originalContent) in originalFiles)
        {
            Assert.True(File.Exists(filePath), $"File should be restored: {filePath}");
            var restoredContent = await File.ReadAllBytesAsync(filePath);
            Assert.Equal(originalContent, restoredContent);
        }
    }

    [Fact]
    public void BrowserDataScanResult_ToScanResult_ProducesValidPipelineInput()
    {
        // Arrange: build a BrowserDataScanResult with entries
        var cacheFile1 = new ScanItem
        {
            Path = Path.Combine(Path.GetTempPath(), "cache1.tmp"),
            SizeBytes = 100,
            LastModifiedUtc = DateTime.UtcNow,
            RiskLevel = RiskLevel.Safe
        };

        var cookiesFile = new ScanItem
        {
            Path = Path.Combine(Path.GetTempPath(), "cookies.db"),
            SizeBytes = 200,
            LastModifiedUtc = DateTime.UtcNow,
            RiskLevel = RiskLevel.Advanced
        };

        var browserScanResult = new BrowserDataScanResult
        {
            Profiles =
            [
                new BrowserProfile
                {
                    BrowserName = "Chrome",
                    BrowserEngine = "Chromium",
                    ProfileName = "Default",
                    ProfilePath = Path.Combine(Path.GetTempPath(), "chrome-profile"),
                    ProcessName = "chrome"
                }
            ],
            Entries =
            [
                new BrowserScanEntry
                {
                    BrowserName = "Chrome",
                    ProfileName = "Default",
                    DataType = BrowserDataType.Cache,
                    DisplayName = "Cache",
                    SizeBytes = 100,
                    Files = [cacheFile1]
                },
                new BrowserScanEntry
                {
                    BrowserName = "Chrome",
                    ProfileName = "Default",
                    DataType = BrowserDataType.Cookies,
                    DisplayName = "Cookies",
                    SizeBytes = 200,
                    Files = [cookiesFile]
                }
            ],
            TotalSizeBytes = 300,
            Warnings = []
        };

        // Act
        var scanResult = browserScanResult.ToScanResult();

        // Assert
        Assert.Equal(2, scanResult.FileCount);
        Assert.Equal(2, scanResult.FolderCount);
        Assert.Equal(300, scanResult.TotalSizeBytes);
        Assert.Equal(2, scanResult.Items.Count);
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        _isolatedRun.Dispose();
    }
}
