using BetterDiskCleanup.Core.Analysis;
using BetterDiskCleanup.Core.Cleanup;
using BetterDiskCleanup.Core.Duplicates;
using BetterDiskCleanup.Core.Recovery;
using BetterDiskCleanup.Core.Safety;
using BetterDiskCleanup.Infrastructure.Duplicates;
using BetterDiskCleanup.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace BetterDiskCleanup.Tests.Duplicates;

public sealed class DuplicateCleanupIntegrationTests : IDisposable
{
    private readonly IsolatedTestRun _isolatedRun;
    private readonly ServiceProvider _serviceProvider;

    public DuplicateCleanupIntegrationTests()
    {
        _isolatedRun = new IsolatedTestRun("dup-int");
        _serviceProvider = _isolatedRun.CreateServiceProvider();
    }

    [Fact]
    public async Task ScanDuplicates_DeleteSelected_EntersRecoverySystem()
    {
        // Arrange — create duplicate files in the isolated data directory
        var content = new byte[] { 10, 20, 30, 40, 50, 60, 70, 80 };

        var file1 = Path.Combine(_isolatedRun.DataDirectory, "report.pdf");
        var file2 = Path.Combine(_isolatedRun.DataDirectory, "report_copy.pdf");
        var file3 = Path.Combine(_isolatedRun.DataDirectory, "unique.txt");

        await File.WriteAllBytesAsync(file1, content);
        await File.WriteAllBytesAsync(file2, content); // duplicate of file1
        await File.WriteAllBytesAsync(file3, new byte[] { 1, 2, 3 }); // unique file

        var scanner = _serviceProvider.GetRequiredService<IDuplicateFileScanner>();
        var executor = _serviceProvider.GetRequiredService<ICleanupExecutor>();

        // Act — scan for duplicates
        var scanResult = await scanner.ScanAsync(_isolatedRun.DataDirectory);

        // Assert — should find 1 duplicate group
        Assert.Single(scanResult.Groups);
        var group = scanResult.Groups[0];
        Assert.Equal(2, group.Members.Count);
        Assert.Equal(8, group.FileSizeBytes);

        // Act — use KeepNewest strategy, delete the older one
        var strategy = new KeepNewestStrategy();
        var toDelete = strategy.SelectForDeletion(group);
        Assert.Single(toDelete); // 1 file to delete, 1 survives

        // Build ScanResult for executor (same pipeline as ViewModel)
        var deleteEntry = group.Members.First(m =>
            string.Equals(m.Path, toDelete[0], StringComparison.OrdinalIgnoreCase));

        var cleanupScanResult = new ScanResult
        {
            FileCount = 1,
            FolderCount = 0,
            TotalSizeBytes = deleteEntry.SizeBytes,
            Items = [new ScanItem
            {
                Path = deleteEntry.Path,
                SizeBytes = deleteEntry.SizeBytes,
                LastModifiedUtc = deleteEntry.LastModifiedUtc,
                RiskLevel = RiskLevel.Safe
            }],
            Warnings = []
        };

        // Act — delete via the standard pipeline
        var report = await executor.ExecuteAsync(cleanupScanResult);

        // Assert — file was deleted and entered recovery
        Assert.Equal(1, report.FilesDeleted);
        Assert.NotNull(report.RecoverySessionId);
        Assert.False(File.Exists(deleteEntry.Path), "Deleted file should not exist on disk.");

        // The surviving file should still exist
        var survivingFile = group.Members.First(m =>
            !string.Equals(m.Path, deleteEntry.Path, StringComparison.OrdinalIgnoreCase)).Path;
        Assert.True(File.Exists(survivingFile), "Surviving file should still exist.");

        // Verify recovery session exists
        var recoveryService = _serviceProvider.GetRequiredService<IRecoveryService>();
        var sessions = recoveryService.ListSessions();
        Assert.NotEmpty(sessions);
        Assert.Contains(sessions, s => s.SessionId == report.RecoverySessionId);
    }

    [Fact]
    public async Task ScanDuplicates_DeleteAndRestore_FileRecoversSuccessfully()
    {
        // Arrange — create two identical files
        var content = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };
        var file1 = Path.Combine(_isolatedRun.DataDirectory, "photo.jpg");
        var file2 = Path.Combine(_isolatedRun.DataDirectory, "photo_backup.jpg");

        await File.WriteAllBytesAsync(file1, content);
        await File.WriteAllBytesAsync(file2, content);

        var scanner = _serviceProvider.GetRequiredService<IDuplicateFileScanner>();
        var executor = _serviceProvider.GetRequiredService<ICleanupExecutor>();
        var recoveryService = _serviceProvider.GetRequiredService<IRecoveryService>();

        // Act — scan
        var scanResult = await scanner.ScanAsync(_isolatedRun.DataDirectory);
        Assert.Single(scanResult.Groups);

        var group = scanResult.Groups[0];

        // Delete file2 (the backup)
        var fileToDelete = group.Members.Last();
        var cleanupScanResult = new ScanResult
        {
            FileCount = 1,
            FolderCount = 0,
            TotalSizeBytes = fileToDelete.SizeBytes,
            Items = [new ScanItem
            {
                Path = fileToDelete.Path,
                SizeBytes = fileToDelete.SizeBytes,
                LastModifiedUtc = fileToDelete.LastModifiedUtc,
                RiskLevel = RiskLevel.Safe
            }],
            Warnings = []
        };

        var report = await executor.ExecuteAsync(cleanupScanResult);
        Assert.Equal(1, report.FilesDeleted);
        Assert.NotNull(report.RecoverySessionId);
        Assert.False(File.Exists(fileToDelete.Path));

        // Act — restore from recovery
        var sessions = recoveryService.ListSessions();
        var session = sessions.First(s => s.SessionId == report.RecoverySessionId);

        var restoreResult = await recoveryService.RestoreSessionAsync(
            session.SessionId, RestoreConflictPolicy.Rename);

        // Assert — file was restored
        Assert.NotEmpty(restoreResult.Items);
        Assert.All(restoreResult.Items, item => Assert.True(item.Restored, $"Item {item.OriginalPath} was not restored."));
        Assert.True(File.Exists(fileToDelete.Path), "Restored file should exist at original path.");

        // Verify content matches
        var restoredContent = await File.ReadAllBytesAsync(fileToDelete.Path);
        Assert.Equal(content, restoredContent);
    }

    [Fact]
    public async Task ValidatorPreventsDeletingAllCopies()
    {
        // Arrange — create two identical files
        var content = new byte[] { 1, 2, 3, 4 };
        var file1 = Path.Combine(_isolatedRun.DataDirectory, "a.bin");
        var file2 = Path.Combine(_isolatedRun.DataDirectory, "b.bin");

        await File.WriteAllBytesAsync(file1, content);
        await File.WriteAllBytesAsync(file2, content);

        var scanner = _serviceProvider.GetRequiredService<IDuplicateFileScanner>();
        var scanResult = await scanner.ScanAsync(_isolatedRun.DataDirectory);

        Assert.Single(scanResult.Groups);
        var group = scanResult.Groups[0];

        // Act — try to select ALL members for deletion
        var allPaths = group.Members.Select(m => m.Path).ToList();
        var corrected = DuplicateDeletionValidator.EnsureMinimumOneSurvivor(
            scanResult.Groups, allPaths);

        // Assert — correction should have removed one path
        Assert.Single(corrected);
        Assert.True(corrected.Count < allPaths.Count,
            "Validator must prevent deleting all copies in a group.");
    }

    public void Dispose()
    {
        _isolatedRun.Dispose();
    }
}
