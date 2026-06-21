namespace BetterDiskCleanup.Core.StartupManager;

/// <summary>
/// Where the startup entry was discovered.
/// </summary>
public enum StartupEntrySource
{
    /// <summary>Startup folder shortcut (.lnk) in user or all-users Startup folder.</summary>
    StartupFolder,

    /// <summary>Registry Run / RunOnce value.</summary>
    Registry,

    /// <summary>Scheduled Task with a logon/startup trigger.</summary>
    ScheduledTask
}
