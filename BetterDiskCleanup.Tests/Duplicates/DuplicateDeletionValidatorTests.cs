using BetterDiskCleanup.Core.Duplicates;
using BetterDiskCleanup.Infrastructure.Duplicates;

namespace BetterDiskCleanup.Tests.Duplicates;

public sealed class DuplicateDeletionValidatorTests
{
    private static DuplicateGroup CreateGroup(string hash, params (string path, DateTime modified)[] files)
    {
        var members = files.Select(f => new DuplicateFileEntry
        {
            Path = f.path,
            FileName = Path.GetFileName(f.path),
            SizeBytes = 100,
            LastModifiedUtc = f.modified,
            CreatedUtc = f.modified
        }).ToList();

        return new DuplicateGroup
        {
            Hash = hash,
            FileSizeBytes = 100,
            Members = members,
            LocationType = DuplicateGroup.DetermineLocationType(members)
        };
    }

    [Fact]
    public void AllMembersSelected_UnselectsNewest()
    {
        // Arrange — group with 3 files, all selected for deletion
        var group = CreateGroup("h1",
            (@"C:\a.txt", new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            (@"C:\b.txt", new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc)), // newest
            (@"C:\c.txt", new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc)));

        var allPaths = group.Members.Select(m => m.Path).ToList();

        // Act
        var corrected = DuplicateDeletionValidator.EnsureMinimumOneSurvivor([group], allPaths);

        // Assert — newest (b.txt) should be unselected
        Assert.Equal(2, corrected.Count);
        Assert.DoesNotContain(@"C:\b.txt", corrected);
    }

    [Fact]
    public void NotAllMembersSelected_NoChange()
    {
        // Arrange — only 2 of 3 selected
        var group = CreateGroup("h1",
            (@"C:\a.txt", new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            (@"C:\b.txt", new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc)),
            (@"C:\c.txt", new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc)));

        var selected = new List<string> { @"C:\a.txt", @"C:\c.txt" };

        // Act
        var corrected = DuplicateDeletionValidator.EnsureMinimumOneSurvivor([group], selected);

        // Assert — b.txt is not selected, so at least 1 survives; no correction needed
        Assert.Equal(2, corrected.Count);
        Assert.Contains(@"C:\a.txt", corrected);
        Assert.Contains(@"C:\c.txt", corrected);
    }

    [Fact]
    public void MultipleGroups_EachValidatedIndependently()
    {
        // Arrange — two groups, both with all members selected
        var group1 = CreateGroup("h1",
            (@"C:\x1.txt", new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc)),
            (@"C:\x2.txt", new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

        var group2 = CreateGroup("h2",
            (@"C:\y1.txt", new DateTime(2025, 4, 1, 0, 0, 0, DateTimeKind.Utc)),
            (@"C:\y2.txt", new DateTime(2025, 9, 1, 0, 0, 0, DateTimeKind.Utc)));

        var allSelected = new List<string>
        {
            @"C:\x1.txt", @"C:\x2.txt",
            @"C:\y1.txt", @"C:\y2.txt"
        };

        // Act
        var corrected = DuplicateDeletionValidator.EnsureMinimumOneSurvivor(
            [group1, group2], allSelected);

        // Assert — each group keeps its newest, so deletion list has the older files
        Assert.Equal(2, corrected.Count);
        Assert.Contains(@"C:\x2.txt", corrected); // x1 is newest in group1, so x2 is deleted
        Assert.Contains(@"C:\y1.txt", corrected); // y2 is newest in group2, so y1 is deleted
    }

    [Fact]
    public void EmptySelection_ReturnsEmpty()
    {
        // Arrange
        var group = CreateGroup("h1",
            (@"C:\a.txt", DateTime.UtcNow),
            (@"C:\b.txt", DateTime.UtcNow));

        // Act
        var corrected = DuplicateDeletionValidator.EnsureMinimumOneSurvivor([group], []);

        // Assert
        Assert.Empty(corrected);
    }

    [Fact]
    public void SingleMemberGroup_AllSelected_StillReturnsEmpty()
    {
        // Arrange — a group with only 1 member selected (it's the only one)
        var group = CreateGroup("h1",
            (@"C:\a.txt", DateTime.UtcNow));

        var selected = new List<string> { @"C:\a.txt" };

        // Act
        var corrected = DuplicateDeletionValidator.EnsureMinimumOneSurvivor([group], selected);

        // Assert — single member must survive
        Assert.Empty(corrected);
    }
}
