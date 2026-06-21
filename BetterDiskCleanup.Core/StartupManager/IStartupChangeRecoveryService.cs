namespace BetterDiskCleanup.Core.StartupManager;

/// <summary>
/// Manages snapshots and undo/restore operations for startup entry changes.
/// Separate from the file-based Recovery System (Fase 2) because this operates
/// on registry values, scheduled task definitions, and shortcut files — not regular files.
/// </summary>
public interface IStartupChangeRecoveryService
{
    /// <summary>
    /// Creates a snapshot of the entry's current state BEFORE a change is applied.
    /// Returns a change record that can later be used for undo/restore.
    /// </summary>
    StartupChangeRecord CreateSnapshot(StartupEntry entry, StartupChangeAction intendedAction);

    /// <summary>
    /// Undoes the most recent change that has not yet been undone.
    /// </summary>
    /// <returns>True if an undo was performed; false if no undoable changes exist.</returns>
    Task<bool> UndoLastChangeAsync();

    /// <summary>
    /// Restores a specific change by its ID.
    /// </summary>
    /// <returns>True if the restore was performed; false if the change was not found or already undone.</returns>
    Task<bool> RestoreFromHistoryAsync(Guid changeId);

    /// <summary>
    /// Returns all change records (most recent first).
    /// </summary>
    IReadOnlyList<StartupChangeRecord> GetHistory();
}
