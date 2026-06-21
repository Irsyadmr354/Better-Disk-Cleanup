namespace BetterDiskCleanup.Core.StartupManager;

/// <summary>
/// Represents a single startup entry discovered from the registry,
/// startup folder, or scheduled tasks.
/// </summary>
public sealed class StartupEntry
{
    /// <summary>Display name of the entry (registry value name, shortcut name, or task name).</summary>
    public required string Name { get; init; }

    /// <summary>Publisher extracted from file metadata / digital signature. May be empty.</summary>
    public string Publisher { get; init; } = string.Empty;

    /// <summary>Full path to the executable or shortcut target.</summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>Current enabled/disabled status.</summary>
    public StartupEntryStatus Status { get; init; }

    /// <summary>Where this entry was discovered.</summary>
    public StartupEntrySource Source { get; init; }

    /// <summary>Estimated startup impact.</summary>
    public StartupImpactLevel Impact { get; init; }

    /// <summary>
    /// True when the entry is a critical Windows system component
    /// (Microsoft-signed AND in a system directory). Cannot be disabled or removed.
    /// </summary>
    public bool IsProtected { get; init; }

    /// <summary>
    /// Source-specific identifier:
    /// - Registry: full registry key path + value name
    /// - StartupFolder: shortcut file path
    /// - ScheduledTask: task path in Task Scheduler
    /// </summary>
    public string EntryId { get; init; } = string.Empty;

    /// <summary>
    /// For registry entries: the registry key path (e.g. HKCU\...\Run).
    /// For other sources: empty.
    /// </summary>
    public string? RegistryKeyPath { get; init; }

    /// <summary>
    /// For registry entries: the value name.
    /// For other sources: empty.
    /// </summary>
    public string? RegistryValueName { get; init; }

    /// <summary>
    /// For registry entries: the value data (command line string).
    /// </summary>
    public string? RegistryValueData { get; init; }

    /// <summary>
    /// For scheduled tasks: the XML definition of the task.
    /// </summary>
    public string? TaskXml { get; init; }

    /// <summary>
    /// For scheduled tasks: the task path in Task Scheduler (e.g. \MyTask).
    /// </summary>
    public string? TaskPath { get; init; }

    /// <summary>
    /// For startup folder entries: the full path to the .lnk file.
    /// </summary>
    public string? ShortcutPath { get; init; }
}
