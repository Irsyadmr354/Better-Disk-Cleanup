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
