using BetterDiskCleanup.Core.Analysis;
using BetterDiskCleanup.Core.Cleanup;
using BetterDiskCleanup.Core.Recovery;
using BetterDiskCleanup.Core.Safety;
using BetterDiskCleanup.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace BetterDiskCleanup.Tests.Recovery;

public sealed class RecoverySnapshotTests : IDisposable
{
    private readonly IsolatedTestRun _isolatedRun;
    private readonly ServiceProvider _serviceProvider;
    private readonly ICleanupExecutor _executor;
    private readonly IRecoveryService _recoveryService;

    public RecoverySnapshotTests()
    {
        _isolatedRun = new IsolatedTestRun("recovery-stage");
        _serviceProvider = _isolatedRun.CreateServiceProvider();
        _executor = _serviceProvider.GetRequiredService<ICleanupExecutor>();
        _recoveryService = _serviceProvider.GetRequiredService<IRecoveryService>();
    }

    [Fact]
    public async Task ExecuteAsync_StagesFilesWithManifestInsteadOfPermanentDelete()
    {
        var filePath = Path.Combine(_isolatedRun.DataDirectory, "stage-me.tmp");
        await File.WriteAllTextAsync(filePath, "recovery-stage-test");

        var scanResult = CreateScanResult(filePath);
        var report = await _executor.ExecuteAsync(scanResult);

        Assert.Equal(1, report.FilesDeleted);
        Assert.NotNull(report.RecoverySessionId);
        Assert.False(File.Exists(filePath));

        var manifest = _recoveryService.GetSessionManifest(report.RecoverySessionId!);
        Assert.NotNull(manifest);
        Assert.Single(manifest!.Items);
        Assert.Equal(filePath, manifest.Items[0].OriginalPath, StringComparer.OrdinalIgnoreCase);
        Assert.True(File.Exists(manifest.Items[0].StagedPath));
        Assert.False(string.IsNullOrWhiteSpace(manifest.Items[0].Sha256Hash));
        Assert.Equal(RecoverySessionStatus.Active, manifest.Status);
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        _isolatedRun.Dispose();
    }

    private static ScanResult CreateScanResult(string filePath) =>
        new()
        {
            FileCount = 1,
            FolderCount = 1,
            TotalSizeBytes = new FileInfo(filePath).Length,
            Items =
            [
                new ScanItem
                {
                    Path = filePath,
                    SizeBytes = new FileInfo(filePath).Length,
                    LastModifiedUtc = DateTime.UtcNow,
                    RiskLevel = RiskLevel.Safe
                }
            ],
            Warnings = []
        };
}

public sealed class RecoveryRestoreTests : IDisposable
{
    private readonly IsolatedTestRun _isolatedRun;
    private readonly ServiceProvider _serviceProvider;
    private readonly ICleanupExecutor _executor;
    private readonly IRecoveryService _recoveryService;

    public RecoveryRestoreTests()
    {
        _isolatedRun = new IsolatedTestRun("recovery-restore");
        _serviceProvider = _isolatedRun.CreateServiceProvider();
        _executor = _serviceProvider.GetRequiredService<ICleanupExecutor>();
        _recoveryService = _serviceProvider.GetRequiredService<IRecoveryService>();
    }

    [Fact]
    public async Task RestoreSession_RestoresOriginalContentWithMatchingHash()
    {
        var filePath = Path.Combine(_isolatedRun.DataDirectory, "restore-me.tmp");
        const string content = "restore-session-content";
        await File.WriteAllTextAsync(filePath, content);
        var originalHash = ComputeHash(filePath);

        var report = await _executor.ExecuteAsync(CreateScanResult(filePath, content.Length));
        Assert.NotNull(report.RecoverySessionId);
        Assert.False(File.Exists(filePath));

        var restoreResult = await _recoveryService.RestoreSessionAsync(report.RecoverySessionId!);
        Assert.Single(restoreResult.Items);
        Assert.True(restoreResult.Items[0].Restored);
        Assert.True(File.Exists(filePath));
        Assert.Equal(content, await File.ReadAllTextAsync(filePath));
        Assert.Equal(originalHash, ComputeHash(filePath));
    }

    [Fact]
    public async Task RestoreSession_WhenOriginalPathOccupiedByDifferentFile_SkipsWithoutOverwrite()
    {
        var filePath = Path.Combine(_isolatedRun.DataDirectory, "conflict.tmp");
        await File.WriteAllTextAsync(filePath, "original-content");
        var report = await _executor.ExecuteAsync(CreateScanResult(filePath, "original-content".Length));
        Assert.NotNull(report.RecoverySessionId);
        Assert.False(File.Exists(filePath));

        await File.WriteAllTextAsync(filePath, "new-unrelated-file");
        var restoreResult = await _recoveryService.RestoreSessionAsync(
            report.RecoverySessionId!,
            RestoreConflictPolicy.Skip);

        Assert.Single(restoreResult.Items);
        Assert.True(restoreResult.Items[0].Skipped);
        Assert.False(restoreResult.Items[0].Restored);
        Assert.Equal("new-unrelated-file", await File.ReadAllTextAsync(filePath));
    }

    [Fact]
    public async Task RestoreSession_WhenOriginalPathOccupied_RenamePolicyRestoresToNewPath()
    {
        var filePath = Path.Combine(_isolatedRun.DataDirectory, "rename-restore.tmp");
        await File.WriteAllTextAsync(filePath, "rename-content");
        var report = await _executor.ExecuteAsync(CreateScanResult(filePath, "rename-content".Length));
        Assert.NotNull(report.RecoverySessionId);

        await File.WriteAllTextAsync(filePath, "blocking-file");
        var restoreResult = await _recoveryService.RestoreSessionAsync(
            report.RecoverySessionId!,
            RestoreConflictPolicy.Rename);

        Assert.Single(restoreResult.Items);
        Assert.True(restoreResult.Items[0].Restored);
        Assert.True(restoreResult.Items[0].Renamed);
        Assert.NotNull(restoreResult.Items[0].RestoredPath);
        Assert.NotEqual(filePath, restoreResult.Items[0].RestoredPath, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("rename-content", await File.ReadAllTextAsync(restoreResult.Items[0].RestoredPath!));
    }

    [Fact]
    public async Task PurgeSession_PermanentlyRemovesStagedFiles()
    {
        var filePath = Path.Combine(_isolatedRun.DataDirectory, "purge-me.tmp");
        await File.WriteAllTextAsync(filePath, "purge-content");
        var report = await _executor.ExecuteAsync(CreateScanResult(filePath, "purge-content".Length));
        Assert.NotNull(report.RecoverySessionId);

        var manifest = _recoveryService.GetSessionManifest(report.RecoverySessionId!)!;
        var stagedPath = manifest.Items[0].StagedPath;
        Assert.True(File.Exists(stagedPath));

        await _recoveryService.PurgeSessionAsync(report.RecoverySessionId!);

        Assert.False(File.Exists(stagedPath));
        Assert.False(Directory.Exists(Path.GetDirectoryName(stagedPath)!));
        var purgedManifest = _recoveryService.GetSessionManifest(report.RecoverySessionId!);
        Assert.Null(purgedManifest);
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        _isolatedRun.Dispose();
    }

    private static ScanResult CreateScanResult(string filePath, int contentLength) =>
        new()
        {
            FileCount = 1,
            FolderCount = 1,
            TotalSizeBytes = contentLength,
            Items =
            [
                new ScanItem
                {
                    Path = filePath,
                    SizeBytes = contentLength,
                    LastModifiedUtc = DateTime.UtcNow,
                    RiskLevel = RiskLevel.Safe
                }
            ],
            Warnings = []
        };

    private static string ComputeHash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream)).ToLowerInvariant();
    }
}

public sealed class RecoveryEndToEndTests : IDisposable
{
    private readonly IsolatedTestRun _isolatedRun;
    private readonly ServiceProvider _serviceProvider;

    public RecoveryEndToEndTests()
    {
        _isolatedRun = new IsolatedTestRun("recovery-e2e");
        _serviceProvider = _isolatedRun.CreateServiceProvider();
    }

    [Fact]
    public async Task ScanCleanRestore_ReturnsFilesIntactAtOriginalLocation()
    {
        var scanner = _serviceProvider.GetRequiredService<ITempFileScanner>();
        var executor = _serviceProvider.GetRequiredService<ICleanupExecutor>();
        var recoveryService = _serviceProvider.GetRequiredService<IRecoveryService>();

        var files = new List<string>();
        long expectedTotal = 0;
        for (var index = 0; index < 2; index++)
        {
            var filePath = Path.Combine(_isolatedRun.DataDirectory, $"e2e-{index}.tmp");
            var content = $"integration-content-{index}";
            await File.WriteAllTextAsync(filePath, content);
            files.Add(filePath);
            expectedTotal += content.Length;
        }

        var scanResult = await scanner.ScanAsync();
        var scopedItems = scanResult.Items
            .Where(item => item.Path.StartsWith(_isolatedRun.DataDirectory, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Equal(2, scopedItems.Count);

        var scopedScan = new ScanResult
        {
            FileCount = scopedItems.Count,
            FolderCount = 1,
            TotalSizeBytes = scopedItems.Sum(item => item.SizeBytes),
            Items = scopedItems,
            Warnings = []
        };

        var cleanupReport = await executor.ExecuteAsync(scopedScan);
        Assert.Equal(2, cleanupReport.FilesDeleted);
        Assert.NotNull(cleanupReport.RecoverySessionId);
        Assert.All(files, path => Assert.False(File.Exists(path)));

        var restoreResult = await recoveryService.RestoreSessionAsync(cleanupReport.RecoverySessionId!);
        Assert.Equal(2, restoreResult.Items.Count(item => item.Restored));
        Assert.All(files, path => Assert.True(File.Exists(path)));
        Assert.Equal("integration-content-0", await File.ReadAllTextAsync(files[0]));
        Assert.Equal("integration-content-1", await File.ReadAllTextAsync(files[1]));
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        _isolatedRun.Dispose();
    }
}
