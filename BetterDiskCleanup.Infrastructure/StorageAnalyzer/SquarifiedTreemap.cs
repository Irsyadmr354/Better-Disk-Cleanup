using BetterDiskCleanup.Core.StorageAnalyzer;

namespace BetterDiskCleanup.Infrastructure.StorageAnalyzer;

/// <summary>
/// Implements the Bruls / Huijsen / van Wijk squarified treemap layout algorithm.
/// Produces rectangles with aspect ratios as close to 1:1 as possible.
/// </summary>
public static class SquarifiedTreemap
{
    /// <summary>
    /// Items that represent less than this fraction of the total are collapsed into "Others".
    /// </summary>
    public const double OthersThresholdFraction = 0.005; // 0.5%

    /// <summary>
    /// Computes a squarified treemap layout for the given items inside the bounding rectangle.
    /// </summary>
    /// <param name="items">Folder nodes with their sizes. Items with SizeBytes == 0 are excluded.</param>
    /// <param name="boundsX">Left edge of the bounding rectangle.</param>
    /// <param name="boundsY">Top edge of the bounding rectangle.</param>
    /// <param name="boundsWidth">Width of the bounding rectangle.</param>
    /// <param name="boundsHeight">Height of the bounding rectangle.</param>
    /// <returns>A list of <see cref="TreemapRect"/> positioned within the bounding rectangle.</returns>
    public static List<TreemapRect> Layout(
        IReadOnlyList<FolderNode> items,
        double boundsX,
        double boundsY,
        double boundsWidth,
        double boundsHeight)
    {
        if (items == null || items.Count == 0 || boundsWidth <= 0 || boundsHeight <= 0)
        {
            return [];
        }

        // Filter out zero-size items
        var nonZero = items.Where(i => i.SizeBytes > 0).ToList();
        if (nonZero.Count == 0)
        {
            return [];
        }

        var totalSize = nonZero.Sum(i => i.SizeBytes);

        // Group small items into "Others"
        var threshold = (long)(totalSize * OthersThresholdFraction);
        var significant = new List<FolderNode>();
        long othersSize = 0;
        int othersCount = 0;

        foreach (var item in nonZero)
        {
            if (item.SizeBytes < threshold && nonZero.Count > 1)
            {
                othersSize += item.SizeBytes;
                othersCount += item.FileCount > 0 ? item.FileCount : 1;
            }
            else
            {
                significant.Add(item);
            }
        }

        // Create "Others" bucket if there were collapsed items
        FolderNode? othersNode = null;
        if (othersSize > 0)
        {
            othersNode = new FolderNode
            {
                Name = $"Others ({othersCount} items)",
                FullPath = string.Empty,
                SizeBytes = othersSize,
                FileCount = othersCount,
                IsFile = false
            };
            significant.Add(othersNode);
        }

        // Sort descending by size
        significant.Sort((a, b) => b.SizeBytes.CompareTo(a.SizeBytes));

        // Scale sizes to area units (pixel² proportional to bytes)
        var totalArea = boundsWidth * boundsHeight;
        var scaledAreas = significant.Select(n => (double)n.SizeBytes / totalSize * totalArea).ToList();

        var results = new List<TreemapRect>();
        Squarify(significant, scaledAreas, boundsX, boundsY, boundsWidth, boundsHeight, results, othersNode, totalSize);

        return results;
    }

    /// <summary>
    /// Core squarification: greedily places rows along the shortest side of the remaining rectangle.
    /// </summary>
    private static void Squarify(
        List<FolderNode> nodes,
        List<double> areas,
        double x, double y, double w, double h,
        List<TreemapRect> results,
        FolderNode? othersNode,
        long totalSize)
    {
        int index = 0;

        while (index < nodes.Count)
        {
            // Remaining rectangle
            double shortest = Math.Min(w, h);

            if (shortest <= 0)
            {
                break;
            }

            // Build a row: greedily add items while the worst aspect ratio improves
            var rowAreas = new List<double> { areas[index] };
            var rowIndices = new List<int> { index };
            index++;

            double currentWorst = WorstAspectRatio(rowAreas, shortest);

            while (index < nodes.Count)
            {
                var candidateAreas = new List<double>(rowAreas) { areas[index] };
                double candidateWorst = WorstAspectRatio(candidateAreas, shortest);

                if (candidateWorst <= currentWorst)
                {
                    // Adding this item improves (or maintains) the aspect ratio
                    rowAreas.Add(areas[index]);
                    rowIndices.Add(index);
                    currentWorst = candidateWorst;
                    index++;
                }
                else
                {
                    // Adding worsens the ratio — finalize the row
                    break;
                }
            }

            // Layout the row along the shortest side
            bool horizontal = w >= h; // lay out along the width if wider, else along height
            double rowTotalArea = rowAreas.Sum();

            if (horizontal)
            {
                // Row spans full width, height proportional to area
                double rowHeight = rowTotalArea / w;
                if (rowHeight > h) rowHeight = h; // clamp
                double curX = x;

                for (int i = 0; i < rowAreas.Count; i++)
                {
                    double itemWidth = rowAreas[i] / rowHeight;
                    if (curX + itemWidth > x + w) itemWidth = x + w - curX; // clamp

                    var node = nodes[rowIndices[i]];
                    results.Add(CreateRect(node, curX, y, itemWidth, rowHeight, othersNode, totalSize));
                    curX += itemWidth;
                }

                // Remaining rectangle below the row
                y += rowHeight;
                h -= rowHeight;
            }
            else
            {
                // Row spans full height, width proportional to area
                double rowWidth = rowTotalArea / h;
                if (rowWidth > w) rowWidth = w; // clamp
                double curY = y;

                for (int i = 0; i < rowAreas.Count; i++)
                {
                    double itemHeight = rowAreas[i] / rowWidth;
                    if (curY + itemHeight > y + h) itemHeight = y + h - curY; // clamp

                    var node = nodes[rowIndices[i]];
                    results.Add(CreateRect(node, x, curY, rowWidth, itemHeight, othersNode, totalSize));
                    curY += itemHeight;
                }

                // Remaining rectangle to the right of the row
                x += rowWidth;
                w -= rowWidth;
            }
        }
    }

    /// <summary>
    /// Computes the worst (maximum) aspect ratio among items in a row,
    /// where the row has the given thickness (shortest side of the container).
    /// </summary>
    private static double WorstAspectRatio(List<double> rowAreas, double sideLength)
    {
        if (rowAreas.Count == 0 || sideLength <= 0) return double.MaxValue;

        double sumArea = rowAreas.Sum();
        double rowThickness = sumArea / sideLength; // the dimension along the short side

        double worst = 0;
        foreach (var area in rowAreas)
        {
            double itemLength = area / rowThickness; // the dimension along the long side
            double ratio = Math.Max(rowThickness / itemLength, itemLength / rowThickness);
            if (ratio > worst) worst = ratio;
        }

        return worst;
    }

    private static TreemapRect CreateRect(
        FolderNode node, double x, double y, double w, double h,
        FolderNode? othersNode, long totalSize)
    {
        bool isOthers = ReferenceEquals(node, othersNode);

        // Determine category
        StorageFileTypeCategory category;
        if (isOthers)
        {
            category = StorageFileTypeCategory.Other;
        }
        else if (node.IsFile)
        {
            category = FileTypeClassifier.Classify(node.Extension);
        }
        else
        {
            // For directories: use the dominant category from breakdown
            category = node.FileTypeBreakdown.Count > 0
                ? node.FileTypeBreakdown.MaxBy(kv => kv.Value).Key
                : StorageFileTypeCategory.Other;
        }

        double percentage = totalSize > 0 ? (double)node.SizeBytes / totalSize * 100.0 : 0;

        return new TreemapRect
        {
            X = x,
            Y = y,
            Width = w,
            Height = h,
            Node = node,
            Category = category,
            IsOthersBucket = isOthers,
            Percentage = percentage
        };
    }
}
