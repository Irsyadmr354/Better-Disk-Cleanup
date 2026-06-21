using System.Text.Json;
using BetterDiskCleanup.Core.StartupManager;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace BetterDiskCleanup.Infrastructure.StartupManager;

/// <summary>
/// Manages recovery for startup entry changes (registry, tasks, shortcuts).
/// Separate from Fase 2's file-based Recovery System because this operates on
/// registry values, task definitions, and shortcut files.
///
/// Backup strategy:
/// - Registry: Values backed up to HKCU\Software\BetterDiskCleanup\StartupBackup\{valueName}
/// - Scheduled tasks: XML definition backed up to the same registry key
/// - Startup folder shortcuts: .lnk file copied to %TEMP%\BetterDiskCleanup\StartupBackup\
///
/// Change history is persisted as JSON to %LocalAppData%\BetterDiskCleanup\startup-change-history.json
/// </summary>
internal sealed class StartupChangeRecoveryService : IStartupChangeRecoveryService
{
    private readonly ILogger<StartupChangeRecoveryService> _logger;
    private readonly List<StartupChangeRecord> _history;
    private readonly object _lock = new();
    private readonly string _historyFilePath;
    private readonly string _shortcutBackupDir;

    internal const string BackupRegistryPath = @"Software\BetterDiskCleanup\StartupBackup";

    public StartupChangeRecoveryService(ILogger<StartupChangeRecoveryService> logger)
        : this(logger, Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BetterDiskCleanup", "startup-change-history.json"))
    {
    }

    /// <summary>
    /// Internal constructor for testing with a custom history file path.
    /// </summary>
    internal StartupChangeRecoveryService(ILogger<StartupChangeRecoveryService> logger, string historyFilePath)
    {
        _logger = logger;
        _historyFilePath = historyFilePath;
        _shortcutBackupDir = Path.Combine(
            Path.GetTempPath(), "BetterDiskCleanup", "StartupBackup");

        _history = LoadHistory();
    }

    public StartupChangeRecord CreateSnapshot(StartupEntry entry, StartupChangeAction intendedAction)
    {
        var snapshot = new StartupEntrySnapshot
        {
            Source = entry.Source,
            EntryName = entry.Name,
            FilePath = entry.FilePath,
            Status = entry.Status,
            RegistryKeyPath = entry.RegistryKeyPath,
            RegistryValueName = entry.RegistryValueName,
            RegistryValueData = entry.RegistryValueData,
            TaskPath = entry.TaskPath,
            TaskXml = entry.TaskXml,
            ShortcutPath = entry.ShortcutPath
        };

        // For startup folder entries, backup the .lnk file bytes
        if (entry.Source == StartupEntrySource.StartupFolder
            && !string.IsNullOrEmpty(entry.ShortcutPath)
            && File.Exists(entry.ShortcutPath))
        {
            try
            {
                snapshot = new StartupEntrySnapshot
                {
                    Source = snapshot.Source,
                    EntryName = snapshot.EntryName,
                    FilePath = snapshot.FilePath,
                    Status = snapshot.Status,
                    RegistryKeyPath = snapshot.RegistryKeyPath,
                    RegistryValueName = snapshot.RegistryValueName,
                    RegistryValueData = snapshot.RegistryValueData,
                    TaskPath = snapshot.TaskPath,
                    TaskXml = snapshot.TaskXml,
                    ShortcutPath = snapshot.ShortcutPath,
                    ShortcutBytes = File.ReadAllBytes(entry.ShortcutPath)
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not backup shortcut file for '{Name}'.", entry.Name);
            }
        }

        var record = new StartupChangeRecord
        {
            ChangeId = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            Action = intendedAction,
            EntryName = entry.Name,
            Source = entry.Source,
            SnapshotBefore = snapshot,
            IsUndone = false
        };

        lock (_lock)
        {
            _history.Insert(0, record);
            SaveHistory();
        }

        _logger.LogInformation(
            "Created snapshot for '{Name}' (action: {Action}, id: {ChangeId}).",
            entry.Name, intendedAction, record.ChangeId);

        return record;
    }

    public async Task<bool> UndoLastChangeAsync()
    {
        StartupChangeRecord? record;
        lock (_lock)
        {
            record = _history.FirstOrDefault(r => !r.IsUndone);
        }

        if (record == null)
        {
            _logger.LogInformation("No undoable changes found.");
            return false;
        }

        return await RestoreFromHistoryAsync(record.ChangeId);
    }

    public Task<bool> RestoreFromHistoryAsync(Guid changeId)
    {
        StartupChangeRecord? record;
        lock (_lock)
        {
            record = _history.FirstOrDefault(r => r.ChangeId == changeId && !r.IsUndone);
        }

        if (record == null)
        {
            _logger.LogInformation("Change {ChangeId} not found or already undone.", changeId);
            return Task.FromResult(false);
        }

        var snapshot = record.SnapshotBefore;
        if (snapshot == null)
        {
            _logger.LogWarning("Change {ChangeId} has no snapshot.", changeId);
            return Task.FromResult(false);
        }

        try
        {
            switch (record.Action)
            {
                case StartupChangeAction.Disable:
                    RestoreDisabledEntry(snapshot);
                    break;

                case StartupChangeAction.Enable:
                    RestoreEnabledEntry(snapshot);
                    break;

                case StartupChangeAction.Remove:
                    RestoreRemovedEntry(snapshot);
                    break;
            }

            lock (_lock)
            {
                record.IsUndone = true;
                SaveHistory();
            }

            _logger.LogInformation(
                "Restored change {ChangeId} ('{Name}', action: {Action}).",
                changeId, record.EntryName, record.Action);

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore change {ChangeId}.", changeId);
            return Task.FromResult(false);
        }
    }

    public IReadOnlyList<StartupChangeRecord> GetHistory()
    {
        lock (_lock)
        {
            return _history.ToList().AsReadOnly();
        }
    }

    // ── Restore helpers ───────────────────────────────────────────────────

    /// <summary>
    /// Restores an entry that was disabled: re-creates the registry value from backup,
    /// re-enables the scheduled task, or restores the shortcut file.
    /// </summary>
    private void RestoreDisabledEntry(StartupEntrySnapshot snapshot)
    {
        switch (snapshot.Source)
        {
            case StartupEntrySource.Registry:
                RestoreRegistryValue(snapshot);
                break;

            case StartupEntrySource.ScheduledTask:
                EnableScheduledTask(snapshot);
                break;

            case StartupEntrySource.StartupFolder:
                RestoreShortcutFile(snapshot);
                break;
        }
    }

    /// <summary>
    /// Restores an entry that was enabled: disables it again.
    /// </summary>
    private void RestoreEnabledEntry(StartupEntrySnapshot snapshot)
    {
        // The entry was enabled, so to undo we need to disable it
        switch (snapshot.Source)
        {
            case StartupEntrySource.ScheduledTask:
                DisableScheduledTask(snapshot);
                break;
            // Registry and startup folder entries are always "enabled" — no undo needed
        }
    }

    /// <summary>
    /// Restores an entry that was removed: re-creates it from the snapshot.
    /// </summary>
    private void RestoreRemovedEntry(StartupEntrySnapshot snapshot)
    {
        switch (snapshot.Source)
        {
            case StartupEntrySource.Registry:
                RestoreRegistryValue(snapshot);
                break;

            case StartupEntrySource.ScheduledTask:
                RestoreScheduledTask(snapshot);
                break;

            case StartupEntrySource.StartupFolder:
                RestoreShortcutFile(snapshot);
                break;
        }
    }

    // ── Registry ──────────────────────────────────────────────────────────

    /// <summary>
    /// Restores a registry value from the snapshot data.
    /// </summary>
    private void RestoreRegistryValue(StartupEntrySnapshot snapshot)
    {
        if (string.IsNullOrEmpty(snapshot.RegistryKeyPath)
            || string.IsNullOrEmpty(snapshot.RegistryValueName))
            return;

        var (hive, subKeyPath) = ParseRegistryKeyPath(snapshot.RegistryKeyPath);
        using var baseKey = OpenHive(hive);
        using var key = baseKey.OpenSubKey(subKeyPath, writable: true);
        if (key == null)
        {
            // Key doesn't exist — try to create it
            using var createdKey = baseKey.CreateSubKey(subKeyPath);
            createdKey?.SetValue(snapshot.RegistryValueName,
                snapshot.RegistryValueData ?? string.Empty,
                RegistryValueKind.String);
            return;
        }

        key.SetValue(snapshot.RegistryValueName,
            snapshot.RegistryValueData ?? string.Empty,
            RegistryValueKind.String);

        _logger.LogInformation("Restored registry value '{Name}' in {KeyPath}.",
            snapshot.RegistryValueName, snapshot.RegistryKeyPath);

        // Clean up backup entry
        CleanupBackupEntry(snapshot.RegistryValueName);
    }

    // ── Scheduled Tasks ───────────────────────────────────────────────────

    private void EnableScheduledTask(StartupEntrySnapshot snapshot)
    {
        SetScheduledTaskEnabled(snapshot, true);
    }

    private void DisableScheduledTask(StartupEntrySnapshot snapshot)
    {
        SetScheduledTaskEnabled(snapshot, false);
    }

    private void SetScheduledTaskEnabled(StartupEntrySnapshot snapshot, bool enabled)
    {
        if (string.IsNullOrEmpty(snapshot.TaskPath)) return;

        try
        {
            var tsType = Type.GetTypeFromProgID("Schedule.Service");
            if (tsType == null) return;

            dynamic? ts = Activator.CreateInstance(tsType);
            if (ts == null) return;

            ts.Connect();
            dynamic rootFolder = ts.GetFolder("\\");
            dynamic task = rootFolder.GetTask(snapshot.TaskPath);
            task.Enabled = enabled;

            _logger.LogInformation("Set scheduled task '{Path}' enabled={Enabled}.",
                snapshot.TaskPath, enabled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set scheduled task '{Path}' enabled={Enabled}.",
                snapshot.TaskPath, enabled);
        }
    }

    private void RestoreScheduledTask(StartupEntrySnapshot snapshot)
    {
        if (string.IsNullOrEmpty(snapshot.TaskXml) || string.IsNullOrEmpty(snapshot.TaskPath))
            return;

        try
        {
            // Use schtasks to re-create the task from XML
            var xmlPath = Path.Combine(Path.GetTempPath(),
                $"BetterDiskCleanup_task_{Guid.NewGuid():N}.xml");
            try
            {
                File.WriteAllText(xmlPath, snapshot.TaskXml);

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "schtasks",
                    Arguments = $"/Create /TN \"{snapshot.TaskPath.TrimStart('\\')}\" /XML \"{xmlPath}\" /F",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(psi);
                process?.WaitForExit(10_000);

                _logger.LogInformation("Restored scheduled task '{Path}' from XML backup.",
                    snapshot.TaskPath);
            }
            finally
            {
                try { File.Delete(xmlPath); } catch { }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore scheduled task '{Path}'.", snapshot.TaskPath);
        }
    }

    // ── Startup Folder ────────────────────────────────────────────────────

    private void RestoreShortcutFile(StartupEntrySnapshot snapshot)
    {
        if (string.IsNullOrEmpty(snapshot.ShortcutPath)) return;

        if (snapshot.ShortcutBytes is { Length: > 0 })
        {
            try
            {
                var dir = Path.GetDirectoryName(snapshot.ShortcutPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllBytes(snapshot.ShortcutPath, snapshot.ShortcutBytes);
                _logger.LogInformation("Restored shortcut file '{Path}'.", snapshot.ShortcutPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restore shortcut '{Path}'.", snapshot.ShortcutPath);
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void CleanupBackupEntry(string valueName)
    {
        try
        {
            using var backupKey = Registry.CurrentUser.OpenSubKey(BackupRegistryPath, writable: true);
            backupKey?.DeleteValue(valueName, throwOnMissingValue: false);
        }
        catch { }
    }

    internal static (RegistryHive hive, string subKeyPath) ParseRegistryKeyPath(string fullPath)
    {
        // Expected formats: "HKCU\Software\..." or "HKLM\Software\..."
        if (fullPath.StartsWith("HKCU\\", StringComparison.OrdinalIgnoreCase))
            return (RegistryHive.CurrentUser, fullPath[5..]);
        if (fullPath.StartsWith("HKLM\\", StringComparison.OrdinalIgnoreCase))
            return (RegistryHive.LocalMachine, fullPath[5..]);

        // Fallback: assume HKCU
        return (RegistryHive.CurrentUser, fullPath);
    }

    internal static RegistryKey OpenHive(RegistryHive hive)
        => RegistryKey.OpenBaseKey(hive, RegistryView.Default);

    // ── History persistence ─────────────────────────────────────────────────

    private List<StartupChangeRecord> LoadHistory()
    {
        try
        {
            if (File.Exists(_historyFilePath))
            {
                var json = File.ReadAllText(_historyFilePath);
                return JsonSerializer.Deserialize<List<StartupChangeRecord>>(json) ?? new();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load startup change history.");
        }

        return new();
    }

    private void SaveHistory()
    {
        try
        {
            var dir = Path.GetDirectoryName(_historyFilePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(_history, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(_historyFilePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not save startup change history.");
        }
    }
}
