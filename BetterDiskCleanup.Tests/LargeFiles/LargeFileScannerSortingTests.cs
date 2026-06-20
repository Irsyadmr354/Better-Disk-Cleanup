using BetterDiskCleanup.Core.LargeFiles;
using BetterDiskCleanup.Infrastructure.LargeFiles;
using BetterDiskCleanup.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace BetterDiskCleanup.Tests.LargeFiles;

public sealed class LargeFileScannerSortingTests
{
    private static LargeFileScanResult GetScanResult()
    {
        var entries = new List<LargeFileEntry>
        {
            new()
            {
                Path = @"C:\TestData\alpha.mp4",
                FileName = "alpha.mp4",
                Extension = ".mp4",
                Category = FileCategory.Video,
                SizeBytes = 3L * 1024 * 1024 * 1024,
                LastModifiedUtc = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc)
            },
            new()
            {
                Path = @"C:\TestData\beta.zip",
                FileName = "beta.zip",
                Extension = ".zip",
                Category = FileCategory.Archive,
                SizeBytes = 1L * 1024 * 1024 * 1024,
                LastModifiedUtc = new DateTime(2024, 6, 20, 0, 0, 0, DateTimeKind.Utc)
            },
            new()
            {
                Path = @"C:\TestData\gamma.iso",
                FileName = "gamma.iso",
                Extension = ".iso",
                Category = FileCategory.DiskImage,
                SizeBytes = 5L * 1024 * 1024 * 1024,
                LastModifiedUtc = new DateTime(2023, 12, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        };

        return new LargeFileScanResult
        {
            Entries = entries,
            Warnings = [],
            TotalSizeBytes = entries.Sum(e => e.SizeBytes)
        };
    }

    [Fact]
    public void SortBySize_Descending_LargestFirst()
    {
        var result = GetScanResult();
        var sorted = result.Entries.OrderByDescending(e => e.SizeBytes).ToList();

        Assert.Equal("gamma.iso", sorted[0].FileName);
        Assert.Equal("alpha.mp4", sorted[1].FileName);
        Assert.Equal("beta.zip", sorted[2].FileName);
    }

    [Fact]
    public void SortBySize_Ascending_SmallestFirst()
    {
        var result = GetScanResult();
        var sorted = result.Entries.OrderBy(e => e.SizeBytes).ToList();

        Assert.Equal("beta.zip", sorted[0].FileName);
        Assert.Equal("alpha.mp4", sorted[1].FileName);
        Assert.Equal("gamma.iso", sorted[2].FileName);
    }

    [Fact]
    public void SortByName_Ascending_Alphabetical()
    {
        var result = GetScanResult();
        var sorted = result.Entries.OrderBy(e => e.FileName, StringComparer.OrdinalIgnoreCase).ToList();

        Assert.Equal("alpha.mp4", sorted[0].FileName);
        Assert.Equal("beta.zip", sorted[1].FileName);
        Assert.Equal("gamma.iso", sorted[2].FileName);
    }

    [Fact]
    public void SortByName_Descending_ReverseAlphabetical()
    {
        var result = GetScanResult();
        var sorted = result.Entries.OrderByDescending(e => e.FileName, StringComparer.OrdinalIgnoreCase).ToList();

        Assert.Equal("gamma.iso", sorted[0].FileName);
        Assert.Equal("beta.zip", sorted[1].FileName);
        Assert.Equal("alpha.mp4", sorted[2].FileName);
    }

    [Fact]
    public void SortByDate_Descending_NewestFirst()
    {
        var result = GetScanResult();
        var sorted = result.Entries.OrderByDescending(e => e.LastModifiedUtc).ToList();

        Assert.Equal("beta.zip", sorted[0].FileName);   // June 2024
        Assert.Equal("alpha.mp4", sorted[1].FileName);  // Jan 2024
        Assert.Equal("gamma.iso", sorted[2].FileName);  // Dec 2023
    }

    [Fact]
    public void SortByDate_Ascending_OldestFirst()
    {
        var result = GetScanResult();
        var sorted = result.Entries.OrderBy(e => e.LastModifiedUtc).ToList();

        Assert.Equal("gamma.iso", sorted[0].FileName);
        Assert.Equal("alpha.mp4", sorted[1].FileName);
        Assert.Equal("beta.zip", sorted[2].FileName);
    }
}
