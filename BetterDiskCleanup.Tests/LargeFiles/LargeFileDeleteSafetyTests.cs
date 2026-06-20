using BetterDiskCleanup.Core.Analysis;
using BetterDiskCleanup.Core.Cleanup;
using BetterDiskCleanup.Core.Recovery;
using BetterDiskCleanup.Core.Safety;
using BetterDiskCleanup.Infrastructure.Safety;
using BetterDiskCleanup.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace BetterDiskCleanup.Tests.LargeFiles;

public sealed class LargeFileDeleteSafetyTests : IDisposable
{
    private readonly IsolatedTestRun _isolatedRun;
    private readonly ServiceProvider _serviceProvider;

    public LargeFileDeleteSafetyTests()
    {
        _isolatedRun = new IsolatedTestRun("lf-safety");
        _serviceProvider = _isolatedRun.CreateServiceProvider();
    }

    [Fact]
    public void Delete_FileInProgramFiles_IsRejectedBySafetyValidator()
    {
        // Arrange — use real PathSafetyValidator which blacklists Program Files
        var safetyValidator = new PathSafetyValidator();

        var programFilesPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var largeFilePath = Path.Combine(programFilesPath, "SomeApp", "huge_database.db");

        // Act
        var validation = safetyValidator.Validate(largeFilePath);

        // Assert — must be denied regardless of file size
        Assert.False(validation.IsAllowed);
    }

    [Fact]
    public void Delete_FileInSystem32_IsRejectedBySafetyValidator()
    {
        // Arrange
        var safetyValidator = new PathSafetyValidator();

        var system32Path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "System32",
            "large_log_file.log");

        // Act
        var validation = safetyValidator.Validate(system32Path);

        // Assert
        Assert.False(validation.IsAllowed);
    }

    [Fact]
    public async Task CleanupExecutor_ReValidatesSafety_ForLargeFilesInTemp()
    {
        // Arrange — create a large file in the isolated test dir (under %TEMP%)
        var executor = _serviceProvider.GetRequiredService<ICleanupExecutor>();
        var largeFile = Path.Combine(_isolatedRun.DataDirectory, "test_large_file.tmp");
        await File.WriteAllBytesAsync(largeFile, new byte[4096]);

        var scanResult = new ScanResult
        {
            FileCount = 1,
            FolderCount = 0,
            TotalSizeBytes = 4096,
            Items = [new ScanItem
            {
                Path = largeFile,
                SizeBytes = 4096,
                LastModifiedUtc = DateTime.UtcNow,
                RiskLevel = RiskLevel.Safe
            }],
            Warnings = []
        };

        // Act
        var report = await executor.ExecuteAsync(scanResult);

        // Assert — file in temp should be whitelisted and deleted
        Assert.Equal(1, report.FilesDeleted);
        Assert.NotNull(report.RecoverySessionId);
        Assert.False(File.Exists(largeFile));
    }

    public void Dispose()
    {
        _isolatedRun.Dispose();
    }
}
