using BetterDiskCleanup.Core.StorageAnalyzer;
using BetterDiskCleanup.Infrastructure.StorageAnalyzer;
using Xunit;

namespace BetterDiskCleanup.Tests.StorageAnalyzer;

public class SquarifiedTreemapTests
{
    [Fact]
    public void Layout_ShouldProduceRectanglesProportionalToSizes()
    {
        // Arrange
        var items = new List<FolderNode>
        {
            new FolderNode { Name = "A", SizeBytes = 60 },
            new FolderNode { Name = "B", SizeBytes = 30 },
            new FolderNode { Name = "C", SizeBytes = 10 }
        };

        double width = 100;
        double height = 100;
        double totalArea = width * height;

        // Act
        var results = SquarifiedTreemap.Layout(items, 0, 0, width, height);

        // Assert
        Assert.Equal(3, results.Count);

        var rectA = results.Single(r => r.Node.Name == "A");
        var rectB = results.Single(r => r.Node.Name == "B");
        var rectC = results.Single(r => r.Node.Name == "C");

        // Allow small floating-point variations
        Assert.True(Math.Abs((rectA.Width * rectA.Height) - (totalArea * 0.6)) < 0.1);
        Assert.True(Math.Abs((rectB.Width * rectB.Height) - (totalArea * 0.3)) < 0.1);
        Assert.True(Math.Abs((rectC.Width * rectC.Height) - (totalArea * 0.1)) < 0.1);

        // Check bounds
        Assert.All(results, r => Assert.True(r.X >= 0 && r.X + r.Width <= width && r.Y >= 0 && r.Y + r.Height <= height));
    }

    [Fact]
    public void Layout_ShouldGroupSmallItemsIntoOthers()
    {
        // Arrange
        var items = new List<FolderNode>
        {
            new FolderNode { Name = "Big", SizeBytes = 10000 },
            new FolderNode { Name = "Tiny1", SizeBytes = 10 },
            new FolderNode { Name = "Tiny2", SizeBytes = 10 },
        };

        // Act
        var results = SquarifiedTreemap.Layout(items, 0, 0, 100, 100);

        // Assert
        Assert.Equal(2, results.Count); // "Big" and "Others"
        Assert.Single(results, r => r.IsOthersBucket);
        var othersBucket = results.Single(r => r.IsOthersBucket);
        Assert.Equal(20, othersBucket.Node.SizeBytes);
    }
}
