using BetterDiskCleanup.Core.Analysis;
using BetterDiskCleanup.Core.Cleanup;
using BetterDiskCleanup.Core.Filesystem;
using BetterDiskCleanup.Core.Recovery;
using BetterDiskCleanup.Core.Safety;
using BetterDiskCleanup.Infrastructure.Cleanup;
using BetterDiskCleanup.Infrastructure.Filesystem;
using BetterDiskCleanup.Infrastructure.Recovery;
using BetterDiskCleanup.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace BetterDiskCleanup.Tests.Cleanup;

public sealed class CleanupExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_DeniedPathsAreNeverDeleted()
    {
        using var isolatedRun = new IsolatedTestRun("exec");
        var innerGateway = new FileSystemGateway();
        var trackingGateway = new TrackingFileSystemGateway(innerGateway);

        var allowedFile = Path.Combine(isolatedRun.DataDirectory, "allowed.tmp");
        var deniedFile = Path.Combine(isolatedRun.DataDirectory, "denied.tmp");
        await File.WriteAllTextAsync(allowedFile, "allowed-content");
        await File.WriteAllTextAsync(deniedFile, "denied-content");

        var validator = new SelectivePathSafetyValidator(deniedFile);
        var recoveryOptions = isolatedRun.CreateRecoveryOptions();
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

        var scanResult = new ScanResult
        {
            FileCount = 2,
            FolderCount = 1,
            TotalSizeBytes = 0,
            Items =
            [
                new ScanItem
                {
                    Path = allowedFile,
                    SizeBytes = new FileInfo(allowedFile).Length,
                    LastModifiedUtc = DateTime.UtcNow,
                    RiskLevel = RiskLevel.Safe
                },
                new ScanItem
                {
                    Path = deniedFile,
                    SizeBytes = new FileInfo(deniedFile).Length,
                    LastModifiedUtc = DateTime.UtcNow,
                    RiskLevel = RiskLevel.Safe
                }
            ],
            Warnings = []
        };

        var report = await executor.ExecuteAsync(scanResult);

        Assert.Equal(1, report.FilesDeleted);
        Assert.False(File.Exists(allowedFile));
        Assert.True(File.Exists(deniedFile));
        Assert.Single(report.Warnings);
        Assert.Empty(report.Errors);
    }

    [Fact]
    public async Task ExecuteAsync_CriticalFilesAreNeverDeleted_EvenIfPassesSafetyValidator()
    {
        using var isolatedRun = new IsolatedTestRun("exec-critical");
        var innerGateway = new FileSystemGateway();
        var trackingGateway = new TrackingFileSystemGateway(innerGateway);

        var allowedFile = Path.Combine(isolatedRun.DataDirectory, "allowed.tmp");
        var criticalFile = Path.Combine(isolatedRun.DataDirectory, "critical.tmp");
        await File.WriteAllTextAsync(allowedFile, "allowed-content");
        await File.WriteAllTextAsync(criticalFile, "critical-content");

        var validator = new AllowAllPathSafetyValidator();
        var criticalGuard = new AlwaysCriticalFileGuard(criticalFile);
        var recoveryOptions = isolatedRun.CreateRecoveryOptions();
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
            criticalGuard,
            NullLogger<CleanupExecutor>.Instance);

        var scanResult = new ScanResult
        {
            FileCount = 2,
            FolderCount = 1,
            TotalSizeBytes = 0,
            Items =
            [
                new ScanItem
                {
                    Path = allowedFile,
                    SizeBytes = new FileInfo(allowedFile).Length,
                    LastModifiedUtc = DateTime.UtcNow,
                    RiskLevel = RiskLevel.Safe
                },
                new ScanItem
                {
                    Path = criticalFile,
                    SizeBytes = new FileInfo(criticalFile).Length,
                    LastModifiedUtc = DateTime.UtcNow,
                    RiskLevel = RiskLevel.Safe
                }
            ],
            Warnings = []
        };

        var report = await executor.ExecuteAsync(scanResult);

        Assert.Equal(1, report.FilesDeleted);
        Assert.False(File.Exists(allowedFile));
        Assert.True(File.Exists(criticalFile));
        Assert.Single(report.Warnings);
        Assert.Empty(report.Errors);
    }

    private sealed class AlwaysCriticalFileGuard : ICriticalFileGuard
    {
        private readonly string _criticalPath;

        public AlwaysCriticalFileGuard(string criticalPath)
        {
            _criticalPath = Path.GetFullPath(criticalPath);
        }

        public CriticalFileCheckResult Check(string path)
        {
            return Path.GetFullPath(path).Equals(_criticalPath, StringComparison.OrdinalIgnoreCase)
                ? new CriticalFileCheckResult { IsCritical = true, Reason = "Critical for test." }
                : new CriticalFileCheckResult { IsCritical = false, Reason = null };
        }
    }

    private sealed class AllowAllPathSafetyValidator : IPathSafetyValidator
    {
        public SafetyValidationResult Validate(string path) => SafetyValidationResult.Allowed(RiskLevel.Safe);
    }

    private sealed class SelectivePathSafetyValidator : IPathSafetyValidator
    {
        private readonly string _deniedPath;

        public SelectivePathSafetyValidator(string deniedPath)
        {
            _deniedPath = Path.GetFullPath(deniedPath);
        }

        public SafetyValidationResult Validate(string path)
        {
            return Path.GetFullPath(path).Equals(_deniedPath, StringComparison.OrdinalIgnoreCase)
                ? SafetyValidationResult.Denied("Denied for test.")
                : SafetyValidationResult.Allowed(RiskLevel.Safe);
        }
    }
}
