namespace BetterDiskCleanup.Core.Duplicates;

public sealed class DuplicateGroup
{
    public required string Hash { get; init; }
    public required long FileSizeBytes { get; init; }
    public required IReadOnlyList<DuplicateFileEntry> Members { get; init; }
    public required DuplicateLocationType LocationType { get; init; }

    /// <summary>
    /// Total space that could be reclaimed by deleting all members except one.
    /// </summary>
    public long RecoverableBytes => FileSizeBytes * (Members.Count - 1);

    /// <summary>
    /// Determines the location type from the members' paths.
    /// If all files are in the same directory with different names → SameFolderDifferentName,
    /// otherwise → DifferentFolder.
    /// </summary>
    public static DuplicateLocationType DetermineLocationType(IReadOnlyList<DuplicateFileEntry> members)
    {
        if (members.Count < 2)
        {
            return DuplicateLocationType.DifferentFolder;
        }

        var directories = members
            .Select(m => System.IO.Path.GetDirectoryName(m.Path))
            .Where(d => !string.IsNullOrEmpty(d))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return directories.Count == 1
            ? DuplicateLocationType.SameFolderDifferentName
            : DuplicateLocationType.DifferentFolder;
    }
}
