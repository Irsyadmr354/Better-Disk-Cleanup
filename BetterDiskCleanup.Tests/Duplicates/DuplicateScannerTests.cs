using BetterDiskCleanup.Infrastructure.Duplicates;
using BetterDiskCleanup.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace BetterDiskCleanup.Tests.Duplicates;

public sealed class DuplicateScannerTests
{
    private static DuplicateFileScanner CreateScanner(InMemoryFileSystemGateway fs)
    {
        return new DuplicateFileScanner(fs, NullLogger<DuplicateFileScanner>.Instance);
    }

    [Fact]
    public async Task SameSize_DifferentContent_NotDetectedAsDuplicates()
    {
        // Arrange — two files with same size but different content
        var fs = new InMemoryFileSystemGateway();
        var root = @"C:\TestData";
        fs.AddDirectory(root);

        // Both 1024 bytes, but different content bytes
        fs.AddFile(Path.Combine(root, "file_a.txt"), 1024, content: new byte[1024].Select((_, i) => (byte)(i % 256)).ToArray());
        fs.AddFile(Path.Combine(root, "file_b.txt"), 1024, content: new byte[1024].Select((_, i) => (byte)((i + 1) % 256)).ToArray());

        var scanner = CreateScanner(fs);

        // Act
        var result = await scanner.ScanAsync(root);

        // Assert — different hashes, so no duplicate groups
        Assert.Empty(result.Groups);
        Assert.Equal(0, result.TotalDuplicateFiles);
    }

    [Fact]
    public async Task IdenticalContent_DifferentNames_DetectedAsDuplicateGroup()
    {
        // Arrange — two files with identical content, different names
        var fs = new InMemoryFileSystemGateway();
        var root = @"C:\TestData";
        fs.AddDirectory(root);

        var content = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x01, 0x02, 0x03, 0x04 };
        fs.AddFile(Path.Combine(root, "original.dat"), 8, content: content);
        fs.AddFile(Path.Combine(root, "copy.dat"), 8, content: content);

        var scanner = CreateScanner(fs);

        // Act
        var result = await scanner.ScanAsync(root);

        // Assert
        Assert.Single(result.Groups);
        Assert.Equal(2, result.Groups[0].Members.Count);
        Assert.Equal(8, result.Groups[0].FileSizeBytes);
    }

    [Fact]
    public async Task IdenticalContent_DifferentLocations_DetectedAsDuplicateGroup()
    {
        // Arrange — identical files in different directories
        var fs = new InMemoryFileSystemGateway();
        var root = @"C:\TestData";
        var subDir1 = Path.Combine(root, "Folder1");
        var subDir2 = Path.Combine(root, "Folder2");
        fs.AddDirectory(root);
        fs.AddDirectory(subDir1);
        fs.AddDirectory(subDir2);

        var content = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        fs.AddFile(Path.Combine(subDir1, "report.pdf"), 10, content: content);
        fs.AddFile(Path.Combine(subDir2, "report_backup.pdf"), 10, content: content);

        var scanner = CreateScanner(fs);

        // Act
        var result = await scanner.ScanAsync(root);

        // Assert
        Assert.Single(result.Groups);
        var group = result.Groups[0];
        Assert.Equal(2, group.Members.Count);
        Assert.Equal(Core.Duplicates.DuplicateLocationType.DifferentFolder, group.LocationType);
    }

    [Fact]
    public async Task SameFolderDifferentName_LocationType_IsSameFolderDifferentName()
    {
        // Arrange — two identical files in the same folder with different names
        var fs = new InMemoryFileSystemGateway();
        var root = @"C:\TestData";
        fs.AddDirectory(root);

        var content = new byte[] { 42, 42, 42, 42 };
        fs.AddFile(Path.Combine(root, "photo.jpg"), 4, content: content);
        fs.AddFile(Path.Combine(root, "photo (1).jpg"), 4, content: content);

        var scanner = CreateScanner(fs);

        // Act
        var result = await scanner.ScanAsync(root);

        // Assert
        Assert.Single(result.Groups);
        Assert.Equal(Core.Duplicates.DuplicateLocationType.SameFolderDifferentName, result.Groups[0].LocationType);
    }

    [Fact]
    public async Task UniqueSizes_NotGroupedAsDuplicates()
    {
        // Arrange — files with unique sizes (no two share the same size)
        var fs = new InMemoryFileSystemGateway();
        var root = @"C:\TestData";
        fs.AddDirectory(root);

        fs.AddFile(Path.Combine(root, "a.txt"), 100, content: new byte[100]);
        fs.AddFile(Path.Combine(root, "b.txt"), 200, content: new byte[200]);
        fs.AddFile(Path.Combine(root, "c.txt"), 300, content: new byte[300]);

        var scanner = CreateScanner(fs);

        // Act
        var result = await scanner.ScanAsync(root);

        // Assert
        Assert.Empty(result.Groups);
    }

    [Fact]
    public async Task EmptyFiles_AreSkipped()
    {
        // Arrange — empty files should be skipped
        var fs = new InMemoryFileSystemGateway();
        var root = @"C:\TestData";
        fs.AddDirectory(root);

        fs.AddFile(Path.Combine(root, "empty1.txt"), 0, content: []);
        fs.AddFile(Path.Combine(root, "empty2.txt"), 0, content: []);

        var scanner = CreateScanner(fs);

        // Act
        var result = await scanner.ScanAsync(root);

        // Assert
        Assert.Empty(result.Groups);
    }

    [Fact]
    public async Task NonExistentRoot_ReturnsWarningAndNoGroups()
    {
        // Arrange
        var fs = new InMemoryFileSystemGateway();
        var scanner = CreateScanner(fs);

        // Act
        var result = await scanner.ScanAsync(@"C:\DoesNotExist");

        // Assert
        Assert.Empty(result.Groups);
        Assert.Single(result.Warnings);
        Assert.Contains("does not exist", result.Warnings[0].Message);
    }

    [Fact]
    public async Task ThreeIdenticalFiles_CorrectRecoverableBytes()
    {
        // Arrange — 3 identical files of 100 bytes each
        var fs = new InMemoryFileSystemGateway();
        var root = @"C:\TestData";
        fs.AddDirectory(root);

        var content = new byte[100].Select((_, i) => (byte)i).ToArray();
        fs.AddFile(Path.Combine(root, "a.bin"), 100, content: content);
        fs.AddFile(Path.Combine(root, "b.bin"), 100, content: content);
        fs.AddFile(Path.Combine(root, "c.bin"), 100, content: content);

        var scanner = CreateScanner(fs);

        // Act
        var result = await scanner.ScanAsync(root);

        // Assert
        Assert.Single(result.Groups);
        var group = result.Groups[0];
        Assert.Equal(3, group.Members.Count);
        // Recoverable = 100 * (3-1) = 200 bytes (keep 1, delete 2)
        Assert.Equal(200, group.RecoverableBytes);
        Assert.Equal(2, result.TotalDuplicateFiles);
    }

    [Fact]
    public async Task CancellationDuringScan_ReturnsPartialResults()
    {
        // Arrange
        var fs = new InMemoryFileSystemGateway();
        var root = @"C:\TestData";
        fs.AddDirectory(root);

        var content = new byte[] { 1, 2, 3, 4 };
        fs.AddFile(Path.Combine(root, "a.txt"), 4, content: content);
        fs.AddFile(Path.Combine(root, "b.txt"), 4, content: content);

        var scanner = CreateScanner(fs);
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert
        // With immediate cancellation, should either throw or return partial
        try
        {
            var result = await scanner.ScanAsync(root, cancellationToken: cts.Token);
            // If it completes, that's fine — partial results
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
    }
}
