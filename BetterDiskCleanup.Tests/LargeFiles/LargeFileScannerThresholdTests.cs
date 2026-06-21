using BetterDiskCleanup.Core.LargeFiles;
using BetterDiskCleanup.Infrastructure.LargeFiles;
using BetterDiskCleanup.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace BetterDiskCleanup.Tests.LargeFiles;

public sealed class LargeFileScannerThresholdTests
{
    private static LargeFileScanner CreateScanner(InMemoryFileSystemGateway fs)
    {
        return new LargeFileScanner(
            fs,
            new NullCriticalFileGuard(),
            new NullUserExclusionService(),
            NullLogger<LargeFileScanner>.Instance);
    }

    [Fact]
    public async Task Scan_With1GBThreshold_OnlyReturnsFilesAbove1GB()
    {
        // Arrange
        var fs = new InMemoryFileSystemGateway();
        var root = @"C:\TestData";
        fs.AddDirectory(root);

        var oneGB = 1024L * 1024 * 1024;
        fs.AddFile(Path.Combine(root, "small.mp4"), 500 * 1024 * 1024, content: [1, 2, 3]); // 500 MB
        fs.AddFile(Path.Combine(root, "exact_1gb.iso"), oneGB, content: [1, 2, 3]);           // exactly 1 GB
        fs.AddFile(Path.Combine(root, "large.mkv"), oneGB + 1, content: [1, 2, 3]);           // just above 1 GB
        fs.AddFile(Path.Combine(root, "huge.vhd"), 5L * 1024 * 1024 * 1024, content: [1, 2, 3]); // 5 GB

        var scanner = CreateScanner(fs);

        // Act
        var result = await scanner.ScanAsync(root, oneGB);

        // Assert (threshold is >= so 1GB exactly IS included)
        Assert.Equal(3, result.Entries.Count);
        Assert.Contains(result.Entries, e => e.FileName == "exact_1gb.iso");
        Assert.Contains(result.Entries, e => e.FileName == "large.mkv");
        Assert.Contains(result.Entries, e => e.FileName == "huge.vhd");
        Assert.DoesNotContain(result.Entries, e => e.FileName == "small.mp4");
    }

    [Fact]
    public async Task Scan_With500MBThreshold_ReturnsCorrectFiles()
    {
        // Arrange
        var fs = new InMemoryFileSystemGateway();
        var root = @"C:\TestData";
        fs.AddDirectory(root);

        var threshold = 500L * 1024 * 1024;
        fs.AddFile(Path.Combine(root, "tiny.txt"), 100 * 1024, content: [1, 2, 3]); // 100 KB
        fs.AddFile(Path.Combine(root, "medium.zip"), 499 * 1024 * 1024, content: [1, 2, 3]); // just below
        fs.AddFile(Path.Combine(root, "big.mp4"), threshold, content: [1, 2, 3]); // exactly 500 MB
        fs.AddFile(Path.Combine(root, "bigger.mp4"), threshold + 1, content: [1, 2, 3]);

        var scanner = CreateScanner(fs);

        // Act
        var result = await scanner.ScanAsync(root, threshold);

        // Assert
        Assert.Equal(2, result.Entries.Count);
        Assert.Contains(result.Entries, e => e.FileName == "big.mp4");
        Assert.Contains(result.Entries, e => e.FileName == "bigger.mp4");
    }

    [Fact]
    public async Task Scan_With5GBThreshold_OnlyReturnsVeryLargeFiles()
    {
        // Arrange
        var fs = new InMemoryFileSystemGateway();
        var root = @"C:\TestData";
        fs.AddDirectory(root);

        var fiveGB = 5L * 1024 * 1024 * 1024;
        fs.AddFile(Path.Combine(root, "movie.mp4"), 4L * 1024 * 1024 * 1024, content: [1, 2, 3]); // 4 GB
        fs.AddFile(Path.Combine(root, "vm_disk.vhdx"), fiveGB, content: [1, 2, 3]); // exactly 5 GB
        fs.AddFile(Path.Combine(root, "database.img"), 10L * 1024 * 1024 * 1024, content: [1, 2, 3]); // 10 GB

        var scanner = CreateScanner(fs);

        // Act
        var result = await scanner.ScanAsync(root, fiveGB);

        // Assert
        Assert.Equal(2, result.Entries.Count);
        Assert.Contains(result.Entries, e => e.FileName == "vm_disk.vhdx");
        Assert.Contains(result.Entries, e => e.FileName == "database.img");
    }

    [Fact]
    public async Task Scan_With100MBThreshold_IncludesFilesInSubfolders()
    {
        // Arrange
        var fs = new InMemoryFileSystemGateway();
        var root = @"C:\TestData";
        var subDir = Path.Combine(root, "subfolder");
        fs.AddDirectory(root);
        fs.AddDirectory(subDir);

        var threshold = 100L * 1024 * 1024;
        fs.AddFile(Path.Combine(root, "root_file.zip"), threshold, content: [1, 2, 3]);
        fs.AddFile(Path.Combine(subDir, "sub_file.zip"), threshold + 1000, content: [1, 2, 3]);

        var scanner = CreateScanner(fs);

        // Act
        var result = await scanner.ScanAsync(root, threshold);

        // Assert
        Assert.Equal(2, result.Entries.Count);
        Assert.Contains(result.Entries, e => e.FileName == "root_file.zip");
        Assert.Contains(result.Entries, e => e.FileName == "sub_file.zip");
    }

    [Fact]
    public async Task Scan_NonExistentRoot_ReturnsWarning()
    {
        // Arrange
        var fs = new InMemoryFileSystemGateway();
        var scanner = CreateScanner(fs);

        // Act
        var result = await scanner.ScanAsync(@"C:\DoesNotExist", 100L * 1024 * 1024);

        // Assert
        Assert.Empty(result.Entries);
        Assert.Single(result.Warnings);
        Assert.Contains("does not exist", result.Warnings[0].Message);
    }
}
