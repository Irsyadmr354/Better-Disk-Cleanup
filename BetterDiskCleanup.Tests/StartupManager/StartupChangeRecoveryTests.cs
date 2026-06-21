using BetterDiskCleanup.Core.StartupManager;
using BetterDiskCleanup.Infrastructure.StartupManager;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;

namespace BetterDiskCleanup.Tests.StartupManager;

/// <summary>
/// Tests for the startup change recovery service using real registry operations.
/// Uses a dedicated test registry key to avoid touching real startup entries.
/// </summary>
public class StartupChangeRecoveryTests : IDisposable
{
    private const string TestRunKeyPath = @"Software\BetterDiskCleanup\Tests\Run";
    private const string TestRunKeyPathFull = $"HKCU\\{TestRunKeyPath}";
    private const string BackupRegistryPath = @"Software\BetterDiskCleanup\StartupBackup";

    private readonly RegistryKey _testKey;
    private readonly string _historyFile;
    private readonly string _tempDir;

    public StartupChangeRecoveryTests()
    {
        // Create a fresh test registry key for each test
        _testKey = Registry.CurrentUser.CreateSubKey(TestRunKeyPath, writable: true);
        // Clean up any leftover values
        foreach (var name in _testKey.GetValueNames())
            _testKey.DeleteValue(name, throwOnMissingValue: false);

        // Also clean backup key
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(BackupRegistryPath, throwOnMissingSubKey: false);
        }
        catch { }

        // Use isolated history file per test instance
        _tempDir = Path.Combine(Path.GetTempPath(), $"BdcTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _historyFile = Path.Combine(_tempDir, "history.json");
    }

    public void Dispose()
    {
        _testKey.Dispose();
        Registry.CurrentUser.DeleteSubKeyTree(TestRunKeyPath, throwOnMissingSubKey: false);
        try { Registry.CurrentUser.DeleteSubKeyTree(BackupRegistryPath, throwOnMissingSubKey: false); } catch { }
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private StartupChangeRecoveryService CreateService()
        => new(NullLogger<StartupChangeRecoveryService>.Instance, _historyFile);

    private StartupEntryManager CreateManager(IStartupEntrySafetyValidator? safetyValidator = null)
    {
        var validator = safetyValidator ?? CreateNonProtectedValidator();
        var recovery = CreateService();
        return new StartupEntryManager(validator, recovery, NullLogger<StartupEntryManager>.Instance);
    }

    private static StartupEntrySafetyValidator CreateNonProtectedValidator()
        => new(NullLogger<StartupEntrySafetyValidator>.Instance, _ => false);

    // ── Disable → Undo tests ──────────────────────────────────────────────

    [Fact]
    public async Task DisableRegistry_ThenUndo_RestoresOriginalValue()
    {
        // Arrange: create a test Run value
        _testKey.SetValue("TestApp", @"""C:\Test\testapp.exe"" --flag", RegistryValueKind.String);
        var originalValue = _testKey.GetValue("TestApp")?.ToString();

        var recovery = CreateService();
        var manager = new StartupEntryManager(CreateNonProtectedValidator(), recovery,
            NullLogger<StartupEntryManager>.Instance);

        var entry = new StartupEntry
        {
            Name = "TestApp",
            FilePath = @"C:\Test\testapp.exe",
            Source = StartupEntrySource.Registry,
            Status = StartupEntryStatus.Enabled,
            EntryId = $"{TestRunKeyPathFull}|TestApp",
            RegistryKeyPath = TestRunKeyPathFull,
            RegistryValueName = "TestApp",
            RegistryValueData = originalValue
        };

        // Act 1: Disable
        await manager.DisableAsync(entry);

        // Assert 1: Value should be gone from the Run key
        using var keyAfterDisable = Registry.CurrentUser.OpenSubKey(TestRunKeyPath);
        Assert.Null(keyAfterDisable?.GetValue("TestApp"));

        // Act 2: Undo
        var undone = await recovery.UndoLastChangeAsync();
        Assert.True(undone);

        // Assert 2: Value should be back in the Run key
        using var keyAfterUndo = Registry.CurrentUser.OpenSubKey(TestRunKeyPath);
        var restoredValue = keyAfterUndo?.GetValue("TestApp")?.ToString();
        Assert.Equal(originalValue, restoredValue);
    }

    [Fact]
    public async Task RemoveRegistry_ThenRestore_RestoresOriginalValue()
    {
        // Arrange
        _testKey.SetValue("RemoveMe", @"C:\Apps\remove.exe", RegistryValueKind.String);
        var originalValue = _testKey.GetValue("RemoveMe")?.ToString();

        var recovery = CreateService();
        var manager = new StartupEntryManager(CreateNonProtectedValidator(), recovery,
            NullLogger<StartupEntryManager>.Instance);

        var entry = new StartupEntry
        {
            Name = "RemoveMe",
            FilePath = @"C:\Apps\remove.exe",
            Source = StartupEntrySource.Registry,
            Status = StartupEntryStatus.Enabled,
            EntryId = $"{TestRunKeyPathFull}|RemoveMe",
            RegistryKeyPath = TestRunKeyPathFull,
            RegistryValueName = "RemoveMe",
            RegistryValueData = originalValue
        };

        // Act 1: Remove
        var record = await manager.RemoveAsync(entry);

        // Assert 1: Value gone
        using var keyAfterRemove = Registry.CurrentUser.OpenSubKey(TestRunKeyPath);
        Assert.Null(keyAfterRemove?.GetValue("RemoveMe"));

        // Act 2: Restore from history
        var restored = await recovery.RestoreFromHistoryAsync(record.ChangeId);
        Assert.True(restored);

        // Assert 2: Value restored
        using var keyAfterRestore = Registry.CurrentUser.OpenSubKey(TestRunKeyPath);
        var restoredValue = keyAfterRestore?.GetValue("RemoveMe")?.ToString();
        Assert.Equal(originalValue, restoredValue);
    }

    [Fact]
    public async Task UndoLastChange_WithNoChanges_ReturnsFalse()
    {
        var recovery = CreateService();
        var result = await recovery.UndoLastChangeAsync();
        Assert.False(result);
    }

    [Fact]
    public async Task UndoLastChange_AlreadyUndone_ReturnsFalse()
    {
        // Arrange
        _testKey.SetValue("OnceApp", "once.exe", RegistryValueKind.String);

        var recovery = CreateService();
        var manager = new StartupEntryManager(CreateNonProtectedValidator(), recovery,
            NullLogger<StartupEntryManager>.Instance);

        var entry = new StartupEntry
        {
            Name = "OnceApp",
            FilePath = "once.exe",
            Source = StartupEntrySource.Registry,
            Status = StartupEntryStatus.Enabled,
            EntryId = $"{TestRunKeyPathFull}|OnceApp",
            RegistryKeyPath = TestRunKeyPathFull,
            RegistryValueName = "OnceApp",
            RegistryValueData = "once.exe"
        };

        await manager.DisableAsync(entry);
        await recovery.UndoLastChangeAsync(); // first undo succeeds

        // Second undo should fail (no more undoable changes)
        var result = await recovery.UndoLastChangeAsync();
        Assert.False(result);
    }

    [Fact]
    public async Task GetHistory_ReturnsAllChanges()
    {
        _testKey.SetValue("App1", "app1.exe", RegistryValueKind.String);
        _testKey.SetValue("App2", "app2.exe", RegistryValueKind.String);

        var recovery = CreateService();
        var manager = new StartupEntryManager(CreateNonProtectedValidator(), recovery,
            NullLogger<StartupEntryManager>.Instance);

        var entry1 = new StartupEntry
        {
            Name = "App1", FilePath = "app1.exe", Source = StartupEntrySource.Registry,
            Status = StartupEntryStatus.Enabled, EntryId = "1",
            RegistryKeyPath = TestRunKeyPathFull, RegistryValueName = "App1", RegistryValueData = "app1.exe"
        };
        var entry2 = new StartupEntry
        {
            Name = "App2", FilePath = "app2.exe", Source = StartupEntrySource.Registry,
            Status = StartupEntryStatus.Enabled, EntryId = "2",
            RegistryKeyPath = TestRunKeyPathFull, RegistryValueName = "App2", RegistryValueData = "app2.exe"
        };

        await manager.DisableAsync(entry1);
        await manager.DisableAsync(entry2);

        var history = recovery.GetHistory();
        Assert.Equal(2, history.Count);
        Assert.Equal("App2", history[0].EntryName); // most recent first
        Assert.Equal("App1", history[1].EntryName);
    }
}
