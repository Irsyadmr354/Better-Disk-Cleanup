namespace BetterDiskCleanup.Core.StartupManager;

/// <summary>
/// Orchestrates Enable/Disable/Remove actions on startup entries.
/// Enforces safety validation and recovery snapshots before every change.
/// </summary>
public interface IStartupEntryManager
{
    /// <summary>
    /// Enables a previously disabled startup entry.
    /// Takes a recovery snapshot before making the change.
    /// </summary>
    Task<StartupChangeRecord> EnableAsync(StartupEntry entry);

    /// <summary>
    /// Disables a startup entry without deleting it.
    /// - Registry: moves value to backup key
    /// - ScheduledTask: uses native disable API
    /// - StartupFolder: moves shortcut to internal backup folder
    /// </summary>
    Task<StartupChangeRecord> DisableAsync(StartupEntry entry);

    /// <summary>
    /// Permanently removes a startup entry.
    /// A recovery snapshot is ALWAYS saved before removal so it can be undone.
    /// </summary>
    Task<StartupChangeRecord> RemoveAsync(StartupEntry entry);
}
