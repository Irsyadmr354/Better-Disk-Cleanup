using BetterDiskCleanup.Core.Analysis;
using BetterDiskCleanup.Core.Cleanup;
using BetterDiskCleanup.Core.Recovery;
using BetterDiskCleanup.Core.Safety;
using BetterDiskCleanup.Infrastructure.Cleanup;
using BetterDiskCleanup.Infrastructure.Filesystem;
using BetterDiskCleanup.Infrastructure.Recovery;
using BetterDiskCleanup.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace BetterDiskCleanup.Tests.Cleanup;

public sealed class CleanupExecutorLockedFileTests : IDisposable
{
    private readonly IsolatedTestRun _isolatedRun;

    public CleanupExecutorLockedFileTests()
    {
        _isolatedRun = new IsolatedTestRun("locked");
    }

    [Fact]
    public async Task ExecuteAsync_LockedFile_SkipsWithWarningWithoutRetry()
    {
        var lockedFile = Path.Combine(_isolatedRun.DataDirectory, "locked.tmp");
        await File.WriteAllTextAsync(lockedFile, "locked-content");

        var innerGateway = new FileSystemGateway();
        var trackingGateway = new TrackingFileSystemGateway(innerGateway);
        var validator = new AllowAllPathSafetyValidator();
        var recoveryOptions = _isolatedRun.CreateRecoveryOptions();
        var recovery = new RecoverySnapshotService(
            trackingGateway,
            validator,
            recoveryOptions,
            NullLogger<RecoverySnapshotService>.Instance);
        var executor = new CleanupExecutor(
            validator,
            trackingGateway,
            recovery,
            recoveryOptions,
            new NullCleanupFailureDetailLogger(),
            new NullFileLockInspector(),
            new NullCriticalFileGuard(),
            NullLogger<CleanupExecutor>.Instance);

        using var lockStream = new FileStream(
            lockedFile,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None);

        var scanResult = CreateScanResult(lockedFile);
        var report = await executor.ExecuteAsync(scanResult);

        Assert.Equal(0, report.FilesDeleted);
        Assert.Empty(report.Errors);
        Assert.Single(report.SkippedInUse);
        Assert.Equal(0, trackingGateway.DeleteAttemptCount);
        Assert.True(File.Exists(lockedFile));
    }

    public void Dispose()
    {
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

    private sealed class AllowAllPathSafetyValidator : IPathSafetyValidator
    {
        public SafetyValidationResult Validate(string path) =>
            SafetyValidationResult.Allowed(RiskLevel.Safe);
    }
}
