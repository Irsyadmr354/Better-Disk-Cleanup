using BetterDiskCleanup.Core.StartupManager;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace BetterDiskCleanup.Infrastructure.StartupManager;

/// <summary>
/// Orchestrates Enable/Disable/Remove actions on startup entries.
/// Every action:
///   1. Validates the entry is not Protected (via IStartupEntrySafetyValidator)
///   2. Creates a recovery snapshot (via IStartupChangeRecoveryService)
///   3. Performs the actual change
/// </summary>
internal sealed class StartupEntryManager : IStartupEntryManager
{
    private readonly IStartupEntrySafetyValidator _safetyValidator;
    private readonly IStartupChangeRecoveryService _recoveryService;
    private readonly ILogger<StartupEntryManager> _logger;

    internal const string BackupRegistryPath = @"Software\BetterDiskCleanup\StartupBackup";

    public StartupEntryManager(
        IStartupEntrySafetyValidator safetyValidator,
        IStartupChangeRecoveryService recoveryService,
        ILogger<StartupEntryManager> logger)
    {
        _safetyValidator = safetyValidator;
        _recoveryService = recoveryService;
        _logger = logger;
    }

    public Task<StartupChangeRecord> EnableAsync(StartupEntry entry)
    {
        _safetyValidator.ValidateActionAllowed(entry, StartupChangeAction.Enable);

        // For enable, the snapshot captures the "disabled" state so undo can disable again
        var record = _recoveryService.CreateSnapshot(entry, StartupChangeAction.Enable);

        switch (entry.Source)
        {
            case StartupEntrySource.Registry:
                EnableRegistryEntry(entry);
                break;

            case StartupEntrySource.ScheduledTask:
                SetScheduledTaskEnabled(entry.TaskPath, true);
                break;

            case StartupEntrySource.StartupFolder:
                RestoreShortcutFromBackup(entry);
                break;
        }

        _logger.LogInformation("Enabled startup entry '{Name}'.", entry.Name);
        return Task.FromResult(record);
    }

    public Task<StartupChangeRecord> DisableAsync(StartupEntry entry)
    {
        // CRITICAL: Validate at service level — not just UI
        _safetyValidator.ValidateActionAllowed(entry, StartupChangeAction.Disable);

        // Create recovery snapshot BEFORE making any changes
        var record = _recoveryService.CreateSnapshot(entry, StartupChangeAction.Disable);

        switch (entry.Source)
        {
            case StartupEntrySource.Registry:
                DisableRegistryEntry(entry);
                break;

            case StartupEntrySource.ScheduledTask:
                SetScheduledTaskEnabled(entry.TaskPath, false);
                break;

            case StartupEntrySource.StartupFolder:
                MoveShortcutToBackup(entry);
                break;
        }

        _logger.LogInformation("Disabled startup entry '{Name}'.", entry.Name);
        return Task.FromResult(record);
    }

    public Task<StartupChangeRecord> RemoveAsync(StartupEntry entry)
    {
        // CRITICAL: Validate at service level — not just UI
        _safetyValidator.ValidateActionAllowed(entry, StartupChangeAction.Remove);

        // Create recovery snapshot BEFORE making any changes
        var record = _recoveryService.CreateSnapshot(entry, StartupChangeAction.Remove);

        switch (entry.Source)
        {
            case StartupEntrySource.Registry:
                RemoveRegistryEntry(entry);
                break;

            case StartupEntrySource.ScheduledTask:
                RemoveScheduledTask(entry);
                break;

            case StartupEntrySource.StartupFolder:
                RemoveShortcutFile(entry);
                break;
        }

        _logger.LogInformation("Removed startup entry '{Name}'.", entry.Name);
        return Task.FromResult(record);
    }

    // ── Registry operations ───────────────────────────────────────────────

    /// <summary>
    /// Disable a registry Run entry by moving the value to a backup key, then deleting the original.
    /// This makes the operation reversible without losing the command-line string.
    /// </summary>
    private void DisableRegistryEntry(StartupEntry entry)
    {
        if (string.IsNullOrEmpty(entry.RegistryKeyPath) || string.IsNullOrEmpty(entry.RegistryValueName))
            return;

        var (hive, subKeyPath) = StartupChangeRecoveryService.ParseRegistryKeyPath(entry.RegistryKeyPath);

        // Read the current value
        using var baseKey = StartupChangeRecoveryService.OpenHive(hive);
        using var sourceKey = baseKey.OpenSubKey(subKeyPath, writable: true);
        if (sourceKey == null)
        {
            _logger.LogWarning("Registry key {Path} not found for disable.", entry.RegistryKeyPath);
            return;
        }

        var valueData = sourceKey.GetValue(entry.RegistryValueName);
        if (valueData == null)
        {
            _logger.LogWarning("Registry value '{Name}' not found in {Path}.",
                entry.RegistryValueName, entry.RegistryKeyPath);
            return;
        }

        // Store in backup key
        using var backupKey = Registry.CurrentUser.CreateSubKey(BackupRegistryPath);
        var backupData = SerializeBackupData(entry, valueData.ToString() ?? string.Empty);
        backupKey.SetValue(entry.RegistryValueName, backupData, RegistryValueKind.String);

        // Delete original value
        sourceKey.DeleteValue(entry.RegistryValueName);

        _logger.LogInformation(
            "Disabled registry entry '{Name}': moved from {Source} to backup key.",
            entry.Name, entry.RegistryKeyPath);
    }

    /// <summary>
    /// Enable a registry entry by restoring it from the backup key.
    /// </summary>
    private void EnableRegistryEntry(StartupEntry entry)
    {
        if (string.IsNullOrEmpty(entry.RegistryValueName))
            return;

        // Read from backup
        using var backupKey = Registry.CurrentUser.OpenSubKey(BackupRegistryPath);
        if (backupKey == null)
        {
            _logger.LogWarning("No backup key found for enable of '{Name}'.", entry.Name);
            return;
        }

        var backupData = backupKey.GetValue(entry.RegistryValueName)?.ToString();
        if (string.IsNullOrEmpty(backupData))
        {
            _logger.LogWarning("No backup data for '{Name}'.", entry.Name);
            return;
        }

        var originalValueData = DeserializeBackupData(backupData);

        // Determine target key
        var targetKeyPath = entry.RegistryKeyPath;
        if (string.IsNullOrEmpty(targetKeyPath))
            targetKeyPath = $@"HKCU\Software\Microsoft\Windows\CurrentVersion\Run";

        var (hive, subKeyPath) = StartupChangeRecoveryService.ParseRegistryKeyPath(targetKeyPath);
        using var baseKey2 = StartupChangeRecoveryService.OpenHive(hive);
        using var targetKey = baseKey2.OpenSubKey(subKeyPath, writable: true)
                              ?? baseKey2.CreateSubKey(subKeyPath);
        targetKey.SetValue(entry.RegistryValueName, originalValueData, RegistryValueKind.String);

        // Clean up backup
        backupKey.Dispose();
        using var backupKeyWritable = Registry.CurrentUser.OpenSubKey(BackupRegistryPath, writable: true);
        backupKeyWritable?.DeleteValue(entry.RegistryValueName, throwOnMissingValue: false);

        _logger.LogInformation("Enabled registry entry '{Name}': restored from backup.", entry.Name);
    }

    /// <summary>
    /// Permanently remove a registry value (snapshot is already saved).
    /// </summary>
    private void RemoveRegistryEntry(StartupEntry entry)
    {
        if (string.IsNullOrEmpty(entry.RegistryKeyPath) || string.IsNullOrEmpty(entry.RegistryValueName))
            return;

        var (hive, subKeyPath) = StartupChangeRecoveryService.ParseRegistryKeyPath(entry.RegistryKeyPath);
        using var baseKey3 = StartupChangeRecoveryService.OpenHive(hive);
        using var key = baseKey3.OpenSubKey(subKeyPath, writable: true);
        key?.DeleteValue(entry.RegistryValueName, throwOnMissingValue: false);

        _logger.LogInformation("Removed registry entry '{Name}' from {Path}.",
            entry.Name, entry.RegistryKeyPath);
    }

    // ── Scheduled task operations ─────────────────────────────────────────

    private void SetScheduledTaskEnabled(string? taskPath, bool enabled)
    {
        if (string.IsNullOrEmpty(taskPath)) return;

        try
        {
            var tsType = Type.GetTypeFromProgID("Schedule.Service");
            if (tsType == null) return;

            dynamic? ts = Activator.CreateInstance(tsType);
            if (ts == null) return;

            ts.Connect();
            dynamic rootFolder = ts.GetFolder("\\");
            dynamic task = rootFolder.GetTask(taskPath);
            task.Enabled = enabled;

            _logger.LogInformation("Set scheduled task '{Path}' enabled={Enabled}.", taskPath, enabled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set task '{Path}' enabled={Enabled}.", taskPath, enabled);
            throw;
        }
    }

    private void RemoveScheduledTask(StartupEntry entry)
    {
        if (string.IsNullOrEmpty(entry.TaskPath)) return;

        try
        {
            var tsType = Type.GetTypeFromProgID("Schedule.Service");
            if (tsType == null) return;

            dynamic? ts = Activator.CreateInstance(tsType);
            if (ts == null) return;

            ts.Connect();
            dynamic rootFolder = ts.GetFolder("\\");

            // Determine folder and task name
            var taskName = Path.GetFileName(entry.TaskPath);
            var folderPath = Path.GetDirectoryName(entry.TaskPath)?.Replace('/', '\\') ?? "\\";
            if (folderPath == "\\") folderPath = "\\";

            dynamic folder = folderPath == "\\" ? rootFolder : ts.GetFolder(folderPath);
            folder.DeleteTask(taskName, 0);

            _logger.LogInformation("Removed scheduled task '{Path}'.", entry.TaskPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove task '{Path}'.", entry.TaskPath);
            throw;
        }
    }

    // ── Startup folder operations ─────────────────────────────────────────

    private void MoveShortcutToBackup(StartupEntry entry)
    {
        if (string.IsNullOrEmpty(entry.ShortcutPath) || !File.Exists(entry.ShortcutPath))
            return;

        var backupDir = Path.Combine(Path.GetTempPath(), "BetterDiskCleanup", "StartupBackup");
        Directory.CreateDirectory(backupDir);

        var backupPath = Path.Combine(backupDir, Path.GetFileName(entry.ShortcutPath));
        File.Move(entry.ShortcutPath, backupPath, overwrite: true);

        _logger.LogInformation("Moved shortcut '{Source}' to backup '{Dest}'.",
            entry.ShortcutPath, backupPath);
    }

    private void RestoreShortcutFromBackup(StartupEntry entry)
    {
        if (string.IsNullOrEmpty(entry.ShortcutPath)) return;

        var backupPath = Path.Combine(
            Path.GetTempPath(), "BetterDiskCleanup", "StartupBackup",
            Path.GetFileName(entry.ShortcutPath));

        if (File.Exists(backupPath))
        {
            var targetDir = Path.GetDirectoryName(entry.ShortcutPath);
            if (!string.IsNullOrEmpty(targetDir))
                Directory.CreateDirectory(targetDir);

            File.Move(backupPath, entry.ShortcutPath, overwrite: true);
            _logger.LogInformation("Restored shortcut from backup to '{Path}'.", entry.ShortcutPath);
        }
    }

    private void RemoveShortcutFile(StartupEntry entry)
    {
        if (string.IsNullOrEmpty(entry.ShortcutPath) || !File.Exists(entry.ShortcutPath))
            return;

        File.Delete(entry.ShortcutPath);
        _logger.LogInformation("Removed shortcut file '{Path}'.", entry.ShortcutPath);
    }

    // ── Backup serialization ──────────────────────────────────────────────

    private static string SerializeBackupData(StartupEntry entry, string valueData)
    {
        // Simple format: keyPath|valueData
        return $"{entry.RegistryKeyPath}|{valueData}";
    }

    private static string DeserializeBackupData(string backupData)
    {
        // Extract the value data (everything after the first '|')
        var separatorIdx = backupData.IndexOf('|');
        if (separatorIdx >= 0 && separatorIdx < backupData.Length - 1)
            return backupData[(separatorIdx + 1)..];
        return backupData;
    }
}
