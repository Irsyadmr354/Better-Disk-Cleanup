namespace BetterDiskCleanup.Core.Duplicates;

public interface IDuplicateSelectionStrategy
{
    SelectionStrategyType StrategyType { get; }

    /// <summary>
    /// Determines which files in the group should be deleted.
    /// Returns the set of file paths to mark for deletion.
    /// MUST leave at least one file alive per group.
    /// </summary>
    IReadOnlyList<string> SelectForDeletion(DuplicateGroup group);
}
