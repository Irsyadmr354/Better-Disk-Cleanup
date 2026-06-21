using BetterDiskCleanup.Core.Duplicates;
using BetterDiskCleanup.Infrastructure.Duplicates;

namespace BetterDiskCleanup.Tests.Duplicates;

public sealed class DuplicateSelectionStrategyTests
{
    private static DuplicateGroup CreateTestGroup()
    {
        var members = new List<DuplicateFileEntry>
        {
            new()
            {
                Path = @"C:\Data\file.txt",
                FileName = "file.txt",
                SizeBytes = 100,
                LastModifiedUtc = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc),
                CreatedUtc = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new()
            {
                Path = @"C:\Backup\file.txt",
                FileName = "file.txt",
                SizeBytes = 100,
                LastModifiedUtc = new DateTime(2025, 3, 20, 0, 0, 0, DateTimeKind.Utc),
                CreatedUtc = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new()
            {
                Path = @"C:\Downloads\file_copy.txt",
                FileName = "file_copy.txt",
                SizeBytes = 100,
                LastModifiedUtc = new DateTime(2025, 2, 10, 0, 0, 0, DateTimeKind.Utc),
                CreatedUtc = new DateTime(2025, 2, 10, 0, 0, 0, DateTimeKind.Utc)
            }
        };

        return new DuplicateGroup
        {
            Hash = "abc123",
            FileSizeBytes = 100,
            Members = members,
            LocationType = DuplicateLocationType.DifferentFolder
        };
    }

    [Fact]
    public void KeepNewest_KeepsMostRecentlyModified()
    {
        // Arrange
        var strategy = new KeepNewestStrategy();
        var group = CreateTestGroup();

        // Act
        var toDelete = strategy.SelectForDeletion(group);

        // Assert — keep the one modified 2025-03-20 (Backup\file.txt), delete the other two
        Assert.Equal(2, toDelete.Count);
        Assert.Contains(@"C:\Data\file.txt", toDelete);
        Assert.Contains(@"C:\Downloads\file_copy.txt", toDelete);
        Assert.DoesNotContain(@"C:\Backup\file.txt", toDelete);
    }

    [Fact]
    public void KeepOldest_KeepsOldestCreated()
    {
        // Arrange
        var strategy = new KeepOldestStrategy();
        var group = CreateTestGroup();

        // Act
        var toDelete = strategy.SelectForDeletion(group);

        // Assert — keep the one created 2024-03-01 (Backup\file.txt), delete the other two
        Assert.Equal(2, toDelete.Count);
        Assert.Contains(@"C:\Data\file.txt", toDelete);
        Assert.Contains(@"C:\Downloads\file_copy.txt", toDelete);
        Assert.DoesNotContain(@"C:\Backup\file.txt", toDelete);
    }

    [Fact]
    public void KeepOriginal_PrefersShortestPathNotInTransientFolder()
    {
        // Arrange
        var strategy = new KeepOriginalStrategy();
        var group = CreateTestGroup();

        // Act
        var toDelete = strategy.SelectForDeletion(group);

        // Assert — "C:\Data\file.txt" (14 chars) is shorter than "C:\Backup\file.txt" (17 chars)
        // and neither is in a transient folder. Downloads IS transient.
        // So Data\file.txt should be kept (shortest, not transient)
        Assert.Equal(2, toDelete.Count);
        Assert.DoesNotContain(@"C:\Data\file.txt", toDelete);
    }

    [Fact]
    public void KeepOriginal_DeprioritizesDownloadsFolder()
    {
        // Arrange — all same path length, one in Downloads
        var strategy = new KeepOriginalStrategy();
        var members = new List<DuplicateFileEntry>
        {
            new()
            {
                Path = @"C:\A\doc.txt",
                FileName = "doc.txt",
                SizeBytes = 50,
                LastModifiedUtc = DateTime.UtcNow,
                CreatedUtc = DateTime.UtcNow
            },
            new()
            {
                Path = @"C:\B\doc.txt",
                FileName = "doc.txt",
                SizeBytes = 50,
                LastModifiedUtc = DateTime.UtcNow,
                CreatedUtc = DateTime.UtcNow
            },
            new()
            {
                Path = @"C:\Downloads\doc.txt",
                FileName = "doc.txt",
                SizeBytes = 50,
                LastModifiedUtc = DateTime.UtcNow,
                CreatedUtc = DateTime.UtcNow
            }
        };

        var group = new DuplicateGroup
        {
            Hash = "xyz",
            FileSizeBytes = 50,
            Members = members,
            LocationType = DuplicateLocationType.DifferentFolder
        };

        // Act
        var toDelete = strategy.SelectForDeletion(group);

        // Assert — Downloads\doc.txt should be deleted (transient folder), one of A/B kept
        Assert.Contains(@"C:\Downloads\doc.txt", toDelete);
    }

    [Fact]
    public void Manual_ReturnsNoSelections()
    {
        // Arrange
        var strategy = new ManualStrategy();
        var group = CreateTestGroup();

        // Act
        var toDelete = strategy.SelectForDeletion(group);

        // Assert
        Assert.Empty(toDelete);
    }

    [Fact]
    public void AllStrategies_SingleMember_ReturnsEmpty()
    {
        // Arrange
        var members = new List<DuplicateFileEntry>
        {
            new()
            {
                Path = @"C:\only.txt",
                FileName = "only.txt",
                SizeBytes = 10,
                LastModifiedUtc = DateTime.UtcNow,
                CreatedUtc = DateTime.UtcNow
            }
        };

        var group = new DuplicateGroup
        {
            Hash = "h",
            FileSizeBytes = 10,
            Members = members,
            LocationType = DuplicateLocationType.DifferentFolder
        };

        // Assert — all strategies must return empty for a single-member group
        Assert.Empty(new KeepNewestStrategy().SelectForDeletion(group));
        Assert.Empty(new KeepOldestStrategy().SelectForDeletion(group));
        Assert.Empty(new KeepOriginalStrategy().SelectForDeletion(group));
        Assert.Empty(new ManualStrategy().SelectForDeletion(group));
    }
}
