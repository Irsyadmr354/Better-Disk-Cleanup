using BetterDiskCleanup.Core.Analysis;
using BetterDiskCleanup.Core.Cleanup;
using BetterDiskCleanup.Core.Recovery;
using BetterDiskCleanup.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace BetterDiskCleanup.Tests.Integration;

public sealed class TempCleanupIntegrationTests : IDisposable
{
    private readonly IsolatedTestRun _isolatedRun;
    private readonly ServiceProvider _serviceProvider;

    public TempCleanupIntegrationTests()
    {
        _isolatedRun = new IsolatedTestRun("int");
        _serviceProvider = _isolatedRun.CreateServiceProvider();
    }

    [Fact]
    public async Task ScanSimulateDelete_RemovesFilesAndMatchesReport()
    {
        const int fileCount = 3;
        const int fileSize = 512;
        long expectedTotal = fileCount * fileSize;

        for (var index = 0; index < fileCount; index++)
        {
            var filePath = Path.Combine(_isolatedRun.DataDirectory, $"temp-{index}.tmp");
            await File.WriteAllBytesAsync(filePath, new byte[fileSize]);
        }

        var scanner = _serviceProvider.GetRequiredService<ITempFileScanner>();
        var simulator = _serviceProvider.GetRequiredService<ICleanupSimulator>();
        var executor = _serviceProvider.GetRequiredService<ICleanupExecutor>();

        var scanResult = await scanner.ScanAsync(cancellationToken: CancellationToken.None);
        var testItems = scanResult.Items
            .Where(item => item.Path.StartsWith(_isolatedRun.DataDirectory, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Equal(fileCount, testItems.Count);
        Assert.Equal(expectedTotal, testItems.Sum(item => item.SizeBytes));

        var scopedScan = new ScanResult
        {
            FileCount = testItems.Count,
            FolderCount = 1,
            TotalSizeBytes = testItems.Sum(item => item.SizeBytes),
            Items = testItems,
            Warnings = []
        };

        var simulation = await simulator.SimulateAsync(scopedScan);
        Assert.Equal(fileCount, simulation.FileCount);
        Assert.Equal(expectedTotal, simulation.RecoverableBytes);

        var report = await executor.ExecuteAsync(scopedScan);

        Assert.Equal(fileCount, report.FilesDeleted);
        Assert.Equal(expectedTotal, report.SpaceRecoveredBytes);
        Assert.NotNull(report.RecoverySessionId);

        foreach (var index in Enumerable.Range(0, fileCount))
        {
            var filePath = Path.Combine(_isolatedRun.DataDirectory, $"temp-{index}.tmp");
            Assert.False(File.Exists(filePath));
        }
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        _isolatedRun.Dispose();
    }
}
