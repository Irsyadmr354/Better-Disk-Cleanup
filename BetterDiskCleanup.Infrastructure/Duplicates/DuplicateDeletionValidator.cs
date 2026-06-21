using BetterDiskCleanup.Core.Duplicates;

namespace BetterDiskCleanup.Infrastructure.Duplicates;

/// <summary>
/// Validates duplicate deletion selections to ensure at least one file
/// survives per duplicate group. This is a critical safety rule.
/// </summary>
public static class DuplicateDeletionValidator
{
    /// <summary>
    /// Validates and corrects the set of paths selected for deletion.
    /// If all members of a group are selected, un-selects one file
    /// (preferring to keep the newest) to prevent total data loss.
    /// Returns the corrected set of paths safe for deletion.
    /// </summary>
    public static IReadOnlyList<string> EnsureMinimumOneSurvivor(
        IReadOnlyList<DuplicateGroup> groups,
        IReadOnlyCollection<string> selectedForDeletion)
    {
        var corrected = new HashSet<string>(selectedForDeletion, StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var groupPaths = group.Members.Select(m => m.Path).ToList();
            var selectedInGroup = groupPaths
                .Where(p => corrected.Contains(p))
                .ToList();

            // If ALL members are selected, we must un-select one
            if (selectedInGroup.Count >= group.Members.Count)
            {
                // Keep the newest (by LastModifiedUtc) as survivor
                var survivor = group.Members
                    .OrderByDescending(m => m.LastModifiedUtc)
                    .ThenByDescending(m => m.CreatedUtc)
                    .First();

                corrected.Remove(survivor.Path);
            }
        }

        return corrected.ToList();
    }
}
