using BetterDiskCleanup.Core.Duplicates;

namespace BetterDiskCleanup.Infrastructure.Duplicates;

/// <summary>
/// Keeps the file with the most recent LastModifiedUtc; marks all others for deletion.
/// </summary>
public sealed class KeepNewestStrategy : IDuplicateSelectionStrategy
{
    public SelectionStrategyType StrategyType => SelectionStrategyType.KeepNewest;

    public IReadOnlyList<string> SelectForDeletion(DuplicateGroup group)
    {
        if (group.Members.Count <= 1)
        {
            return [];
        }

        var keep = group.Members
            .OrderByDescending(m => m.LastModifiedUtc)
            .ThenByDescending(m => m.CreatedUtc)
            .First();

        return group.Members
            .Where(m => !string.Equals(m.Path, keep.Path, StringComparison.OrdinalIgnoreCase))
            .Select(m => m.Path)
            .ToList();
    }
}

/// <summary>
/// Keeps the file with the oldest CreatedUtc (or LastModifiedUtc as tiebreaker); marks all others for deletion.
/// </summary>
public sealed class KeepOldestStrategy : IDuplicateSelectionStrategy
{
    public SelectionStrategyType StrategyType => SelectionStrategyType.KeepOldest;

    public IReadOnlyList<string> SelectForDeletion(DuplicateGroup group)
    {
        if (group.Members.Count <= 1)
        {
            return [];
        }

        var keep = group.Members
            .OrderBy(m => m.CreatedUtc)
            .ThenBy(m => m.LastModifiedUtc)
            .First();

        return group.Members
            .Where(m => !string.Equals(m.Path, keep.Path, StringComparison.OrdinalIgnoreCase))
            .Select(m => m.Path)
            .ToList();
    }
}

/// <summary>
/// Keeps the file that is most likely the "original":
/// 1. Prefer the shortest path (likely a canonical/original location).
/// 2. Among equal-length paths, prefer folders NOT named Download/Temp/Cache.
/// 3. Tiebreaker: oldest CreatedUtc.
/// </summary>
public sealed class KeepOriginalStrategy : IDuplicateSelectionStrategy
{
    /// <summary>
    /// Folder name segments that indicate transient/temporary locations.
    /// Files in these folders are deprioritized as "originals".
    /// </summary>
    internal static readonly HashSet<string> TransientFolderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Downloads", "Download", "Temp", "Tmp", "Cache", "CachedData",
        "AppData", ".cache", "trash", "Recycle"
    };

    public SelectionStrategyType StrategyType => SelectionStrategyType.KeepOriginal;

    public IReadOnlyList<string> SelectForDeletion(DuplicateGroup group)
    {
        if (group.Members.Count <= 1)
        {
            return [];
        }

        var keep = group.Members
            .OrderBy(m => IsInTransientFolder(m.Path) ? 1 : 0)
            .ThenBy(m => m.Path.Length)
            .ThenBy(m => m.CreatedUtc)
            .First();

        return group.Members
            .Where(m => !string.Equals(m.Path, keep.Path, StringComparison.OrdinalIgnoreCase))
            .Select(m => m.Path)
            .ToList();
    }

    internal static bool IsInTransientFolder(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(directory))
        {
            return false;
        }

        var segments = directory.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);

        return segments.Any(seg => TransientFolderNames.Contains(seg));
    }
}

/// <summary>
/// Manual strategy — returns no deletions. The user must select files manually.
/// </summary>
public sealed class ManualStrategy : IDuplicateSelectionStrategy
{
    public SelectionStrategyType StrategyType => SelectionStrategyType.Manual;

    public IReadOnlyList<string> SelectForDeletion(DuplicateGroup group) => [];
}
