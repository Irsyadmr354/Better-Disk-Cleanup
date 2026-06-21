namespace BetterDiskCleanup.Core.StartupManager;

/// <summary>
/// Record of a single change made to a startup entry, stored for undo/restore.
/// </summary>
public sealed class StartupChangeRecord
{
    /// <summary>Unique identifier for this change.</summary>
    public Guid ChangeId { get; init; } = Guid.NewGuid();

    /// <summary>When the change was made.</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>What action was performed.</summary>
    public StartupChangeAction Action { get; init; }

    /// <summary>The entry name (for display purposes).</summary>
    public string EntryName { get; init; } = string.Empty;

    /// <summary>Where the entry came from.</summary>
    public StartupEntrySource Source { get; init; }

    /// <summary>
    /// Snapshot of the entry state BEFORE the change was applied.
    /// Used to restore the entry to its previous state.
    /// </summary>
    public StartupEntrySnapshot? SnapshotBefore { get; init; }

    /// <summary>Whether this change has already been undone.</summary>
    public bool IsUndone { get; set; }
}

/// <summary>
/// Immutable snapshot of a startup entry's state, used for recovery.
/// </summary>
public sealed class StartupEntrySnapshot
{
    public StartupEntrySource Source { get; init; }

    // Registry snapshot
    public string? RegistryKeyPath { get; init; }
    public string? RegistryValueName { get; init; }
    public string? RegistryValueData { get; init; }

    // Scheduled task snapshot
    public string? TaskPath { get; init; }
    public string? TaskXml { get; init; }

    // Startup folder snapshot
    public string? ShortcutPath { get; init; }
    public byte[]? ShortcutBytes { get; init; }

    // Common
    public string EntryName { get; init; } = string.Empty;
    public string FilePath { get; init; } = string.Empty;
    public StartupEntryStatus Status { get; init; }
}
