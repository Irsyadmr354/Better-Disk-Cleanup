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

public sealed class CleanupExecutorReadOnlyTests : IDisposable
{
    private readonly IsolatedTestRun _isolatedRun;

    public CleanupExecutorReadOnlyTests()
    {
        _isolatedRun = new IsolatedTestRun("readonly");
    }

    [Fact]
    public async Task ExecuteAsync_ReadOnlyFile_ClearsAttributeAndDeletesSuccessfully()
    {
        var readOnlyFile = Path.Combine(_isolatedRun.DataDirectory, "readonly.tmp");
        await File.WriteAllTextAsync(readOnlyFile, "read-only-content");
        File.SetAttributes(readOnlyFile, FileAttributes.ReadOnly);

        var validator = new AllowAllPathSafetyValidator();
        var fileSystem = new FileSystemGateway();
        var recoveryOptions = _isolatedRun.CreateRecoveryOptions();
        var recovery = new RecoverySnapshotService(
            fileSystem,
            validator,
            recoveryOptions,
            NullLogger<RecoverySnapshotService>.Instance);
        var executor = new CleanupExecutor(
            validator,
            fileSystem,
            recovery,
            recoveryOptions,
            new NullCleanupFailureDetailLogger(),
            new NullFileLockInspector(),
            new NullCriticalFileGuard(),
            NullLogger<CleanupExecutor>.Instance);

        var scanResult = CreateScanResult(readOnlyFile);
        var report = await executor.ExecuteAsync(scanResult);

        Assert.Equal(1, report.FilesDeleted);
        Assert.Empty(report.Errors);
        Assert.Empty(report.SkippedInUse);
        Assert.False(File.Exists(readOnlyFile));
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
