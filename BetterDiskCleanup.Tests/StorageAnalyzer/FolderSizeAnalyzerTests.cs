using BetterDiskCleanup.Core.StorageAnalyzer;
using BetterDiskCleanup.Infrastructure.StorageAnalyzer;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BetterDiskCleanup.Tests.StorageAnalyzer;

public class FolderSizeAnalyzerTests : IDisposable
{
    private readonly string _testRoot;
    private readonly FolderSizeAnalyzer _analyzer;

    public FolderSizeAnalyzerTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "StorageAnalyzerTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);
        _analyzer = new FolderSizeAnalyzer(NullLogger<FolderSizeAnalyzer>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
            Directory.Delete(_testRoot, true);
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldCalculateCumulativeSizesCorrectly()
    {
        // Arrange
        var subDir1 = Path.Combine(_testRoot, "Sub1");
        var subDir2 = Path.Combine(_testRoot, "Sub2");
        Directory.CreateDirectory(subDir1);
        Directory.CreateDirectory(subDir2);

        File.WriteAllBytes(Path.Combine(_testRoot, "root1.txt"), new byte[100]);
        File.WriteAllBytes(Path.Combine(subDir1, "sub1.txt"), new byte[200]);
        File.WriteAllBytes(Path.Combine(subDir2, "sub2.txt"), new byte[300]);
        File.WriteAllBytes(Path.Combine(subDir2, "sub2_2.txt"), new byte[400]);

        // Act
        var result = await _analyzer.AnalyzeAsync(_testRoot);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(100 + 200 + 300 + 400, result.SizeBytes);
        Assert.Equal(4, result.FileCount);

        var childSub1 = result.Children.Single(c => c.Name == "Sub1");
        Assert.Equal(200, childSub1.SizeBytes);
        Assert.Equal(1, childSub1.FileCount);

        var childSub2 = result.Children.Single(c => c.Name == "Sub2");
        Assert.Equal(700, childSub2.SizeBytes);
        Assert.Equal(2, childSub2.FileCount);

        // Validate size = sum of children sizes (for directories) + own files
        long calculatedSize = result.Children.Sum(c => c.SizeBytes);
        Assert.Equal(calculatedSize, result.SizeBytes);
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldReportProgressPeriodically()
    {
        // Arrange
        // Create 100 directories to trigger progress reports (interval is 50)
        for (int i = 0; i < 110; i++)
        {
            var dir = Path.Combine(_testRoot, $"Dir_{i}");
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, "file.txt"), new byte[10]);
        }

        int progressCount = 0;
        var progress = new Progress<StorageAnalyzerProgress>(p =>
        {
            progressCount++;
            Assert.True(p.DirectoriesScanned > 0);
        });

        // Act
        await _analyzer.AnalyzeAsync(_testRoot, progress);

        // Assert
        // We might not get exactly 2 periodic + 1 final because of async scheduling
        // but it should definitely be > 0
        Assert.True(progressCount > 0); 
    }
}
