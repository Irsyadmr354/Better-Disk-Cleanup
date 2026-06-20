using BetterDiskCleanup.Core.Analysis;
using BetterDiskCleanup.Core.Cleanup;
using BetterDiskCleanup.Core.LargeFiles;
using BetterDiskCleanup.Core.Recovery;
using BetterDiskCleanup.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace BetterDiskCleanup.Tests.Integration;

public sealed class LargeFileCleanupIntegrationTests : IDisposable
{
    private readonly IsolatedTestRun _isolatedRun;
    private readonly ServiceProvider _serviceProvider;

    public LargeFileCleanupIntegrationTests()
    {
        _isolatedRun = new IsolatedTestRun("lf-int");
        _serviceProvider = _isolatedRun.CreateServiceProvider();
    }

    [Fact]
    public async Task ScanLargeFiles_FindsExpectedFiles_AboveThreshold()
    {
        // Arrange — create test files in the isolated data directory
        var threshold = 1024L; // 1 KB threshold for test
        var largeFile = Path.Combine(_isolatedRun.DataDirectory, "large_video.mp4");
        var smallFile = Path.Combine(_isolatedRun.DataDirectory, "small_doc.txt");

        await File.WriteAllBytesAsync(largeFile, new byte[2048]); // 2 KB — above threshold
        await File.WriteAllBytesAsync(smallFile, new byte[100]);  // 100 bytes — below threshold

        var scanner = _serviceProvider.GetRequiredService<ILargeFileScanner>();

        // Act
        var result = await scanner.ScanAsync(
            _isolatedRun.DataDirectory,
            threshold);

        // Assert
        Assert.Single(result.Entries);
        Assert.Equal("large_video.mp4", result.Entries[0].FileName);
        Assert.Equal(FileCategory.Video, result.Entries[0].Category);
        Assert.True(result.Entries[0].SizeBytes >= threshold);
    }

    [Fact]
    public async Task ScanAndDelete_LargeFile_EntersRecoverySystem()
    {
        // Arrange — create a large test file
        var threshold = 1024L;
        var largeFile = Path.Combine(_isolatedRun.DataDirectory, "big_archive.zip");
        await File.WriteAllBytesAsync(largeFile, new byte[4096]); // 4 KB

        var scanner = _serviceProvider.GetRequiredService<ILargeFileScanner>();
        var executor = _serviceProvider.GetRequiredService<ICleanupExecutor>();

        // Act — scan
        var scanResult = await scanner.ScanAsync(_isolatedRun.DataDirectory, threshold);
        Assert.Single(scanResult.Entries);

        // Build a ScanResult for the executor (same pipeline as ViewModel would)
        var entry = scanResult.Entries[0];
        var cleanupScanResult = new ScanResult
        {
            FileCount = 1,
            FolderCount = 0,
            TotalSizeBytes = entry.SizeBytes,
            Items = [new ScanItem
            {
                Path = entry.Path,
                SizeBytes = entry.SizeBytes,
                LastModifiedUtc = entry.LastModifiedUtc,
                RiskLevel = Core.Safety.RiskLevel.Safe
            }],
            Warnings = []
        };

        // Act — delete via the standard pipeline
        var report = await executor.ExecuteAsync(cleanupScanResult);

        // Assert — file was deleted and entered recovery
        Assert.Equal(1, report.FilesDeleted);
        Assert.NotNull(report.RecoverySessionId);
        Assert.False(File.Exists(largeFile), "File should be deleted from original location.");

        // Verify recovery manifest exists
        var recoveryService = _serviceProvider.GetRequiredService<IRecoveryService>();
        var sessions = recoveryService.ListSessions();
        Assert.NotEmpty(sessions);
        Assert.Contains(sessions, s => s.SessionId == report.RecoverySessionId);
    }

    public void Dispose()
    {
        _isolatedRun.Dispose();
    }
}
