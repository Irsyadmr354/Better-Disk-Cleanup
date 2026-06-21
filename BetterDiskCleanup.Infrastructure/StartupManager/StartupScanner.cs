using BetterDiskCleanup.Core.StartupManager;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace BetterDiskCleanup.Infrastructure.StartupManager;

/// <summary>
/// Scans registry Run/RunOnce keys, startup folders, and scheduled tasks
/// to discover all startup entries on the system.
/// </summary>
internal sealed class StartupScanner : IStartupScanner
{
    private readonly IScheduledTaskReader _taskReader;
    private readonly IStartupImpactEstimator _impactEstimator;
    private readonly IStartupEntrySafetyValidator _safetyValidator;
    private readonly ILogger<StartupScanner> _logger;

    // Registry key paths for startup entries
    internal static readonly string[] RegistryRunKeyPaths =
    {
        @"Software\Microsoft\Windows\CurrentVersion\Run",
        @"Software\Microsoft\Windows\CurrentVersion\RunOnce"
    };

    public StartupScanner(
        IScheduledTaskReader taskReader,
        IStartupImpactEstimator impactEstimator,
        IStartupEntrySafetyValidator safetyValidator,
        ILogger<StartupScanner> logger)
    {
        _taskReader = taskReader;
        _impactEstimator = impactEstimator;
        _safetyValidator = safetyValidator;
        _logger = logger;
    }

    public Task<IReadOnlyList<StartupEntry>> ScanAllAsync()
    {
        var entries = new List<StartupEntry>();

        // 1. Registry entries (HKCU + HKLM)
        ScanRegistryEntries(entries, RegistryHive.CurrentUser, "HKCU");
        ScanRegistryEntries(entries, RegistryHive.LocalMachine, "HKLM");

        // 2. Startup folder entries
        ScanStartupFolder(entries,
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            "User");
        ScanStartupFolder(entries,
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                @"Microsoft\Windows\Start Menu\Programs\Startup"),
            "AllUsers");

        // 3. Scheduled tasks
        ScanScheduledTasks(entries);

        _logger.LogInformation("Startup scan complete: {Count} entries found.", entries.Count);

        IReadOnlyList<StartupEntry> result = entries;
        return Task.FromResult(result);
    }

    private void ScanRegistryEntries(List<StartupEntry> entries, RegistryHive hive, string hiveLabel)
    {
        foreach (var keyPath in RegistryRunKeyPaths)
        {
            try
            {
                using var key = hive == RegistryHive.CurrentUser
                    ? Registry.CurrentUser.OpenSubKey(keyPath)
                    : Registry.LocalMachine.OpenSubKey(keyPath);

                if (key == null) continue;

                foreach (var valueName in key.GetValueNames())
                {
                    try
                    {
                        var valueData = key.GetValue(valueName)?.ToString() ?? string.Empty;
                        var exePath = FileSignatureHelper.ExtractExePath(valueData);
                        var publisher = FileSignatureHelper.GetPublisher(exePath);
                        var impact = _impactEstimator.Estimate(exePath);
                        var fullKeyPath = $"{hiveLabel}\\{keyPath}";

                        var entry = new StartupEntry
                        {
                            Name = valueName,
                            Publisher = publisher,
                            FilePath = exePath,
                            Status = StartupEntryStatus.Enabled, // Registry Run keys are always enabled
                            Source = StartupEntrySource.Registry,
                            Impact = impact,
                            EntryId = $"{fullKeyPath}|{valueName}",
                            RegistryKeyPath = fullKeyPath,
                            RegistryValueName = valueName,
                            RegistryValueData = valueData
                        };

                        // Check protection at scan time
                        var isProtected = _safetyValidator.IsProtected(entry);

                        entries.Add(new StartupEntry
                        {
                            Name = entry.Name, Publisher = entry.Publisher, FilePath = entry.FilePath,
                            Status = entry.Status, Source = entry.Source, Impact = entry.Impact,
                            EntryId = entry.EntryId, RegistryKeyPath = entry.RegistryKeyPath,
                            RegistryValueName = entry.RegistryValueName,
                            RegistryValueData = entry.RegistryValueData,
                            IsProtected = isProtected
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Could not read registry value '{ValueName}' in {KeyPath}.", valueName, keyPath);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not open registry key {KeyPath}.", keyPath);
            }
        }
    }

    private void ScanStartupFolder(List<StartupEntry> entries, string folderPath, string folderLabel)
    {
        try
        {
            if (!Directory.Exists(folderPath)) return;

            foreach (var lnkFile in Directory.GetFiles(folderPath, "*.lnk"))
            {
                try
                {
                    var target = FileSignatureHelper.ResolveShortcutTarget(lnkFile);
                    var name = Path.GetFileNameWithoutExtension(lnkFile);
                    var publisher = FileSignatureHelper.GetPublisher(target);
                    var impact = _impactEstimator.Estimate(target);

                    var entry = new StartupEntry
                    {
                        Name = name,
                        Publisher = publisher,
                        FilePath = target,
                        Status = StartupEntryStatus.Enabled,
                        Source = StartupEntrySource.StartupFolder,
                        Impact = impact,
                        EntryId = $"{folderLabel}:{lnkFile}",
                        ShortcutPath = lnkFile
                    };

                    entries.Add(new StartupEntry
                    {
                        Name = entry.Name, Publisher = entry.Publisher, FilePath = entry.FilePath,
                        Status = entry.Status, Source = entry.Source, Impact = entry.Impact,
                        EntryId = entry.EntryId, ShortcutPath = entry.ShortcutPath,
                        IsProtected = _safetyValidator.IsProtected(entry)
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Could not read shortcut '{File}'.", lnkFile);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not enumerate startup folder '{Path}'.", folderPath);
        }
    }

    private void ScanScheduledTasks(List<StartupEntry> entries)
    {
        try
        {
            var tasks = _taskReader.GetStartupTasks();

            foreach (var task in tasks)
            {
                try
                {
                    var exePath = FileSignatureHelper.ExtractExePath(task.ExePath);
                    var publisher = FileSignatureHelper.GetPublisher(exePath);
                    var impact = _impactEstimator.Estimate(exePath);

                    var entry = new StartupEntry
                    {
                        Name = task.Name,
                        Publisher = publisher,
                        FilePath = exePath,
                        Status = task.IsEnabled ? StartupEntryStatus.Enabled : StartupEntryStatus.Disabled,
                        Source = StartupEntrySource.ScheduledTask,
                        Impact = impact,
                        EntryId = $"Task:{task.TaskPath}",
                        TaskPath = task.TaskPath,
                        TaskXml = task.Xml
                    };

                    entries.Add(new StartupEntry
                    {
                        Name = entry.Name, Publisher = entry.Publisher, FilePath = entry.FilePath,
                        Status = entry.Status, Source = entry.Source, Impact = entry.Impact,
                        EntryId = entry.EntryId, TaskPath = entry.TaskPath, TaskXml = entry.TaskXml,
                        IsProtected = _safetyValidator.IsProtected(entry)
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Could not process scheduled task '{Name}'.", task.Name);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enumerate scheduled tasks.");
        }
    }
}
