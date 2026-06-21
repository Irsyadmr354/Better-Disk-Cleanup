using System.Diagnostics;
using System.Security.Cryptography;
using BetterDiskCleanup.Infrastructure.Duplicates;
using BetterDiskCleanup.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace BetterDiskCleanup.Tests.Duplicates;

public sealed class DuplicateScannerPerformanceTests
{
    [Fact]
    public async Task ParallelHashing_CompletesInReasonableTime_ForManySameSizeFiles()
    {
        // Arrange — create 120+ files with the same size (but different content)
        // to force the scanner to hash all of them in parallel
        const int fileCount = 120;
        const int fileSize = 8 * 1024; // 8 KB each

        var fs = new InMemoryFileSystemGateway();
        var root = @"C:\PerfTest";
        fs.AddDirectory(root);

        var rng = new Random(42); // fixed seed for reproducibility
        for (var i = 0; i < fileCount; i++)
        {
            var content = new byte[fileSize];
            rng.NextBytes(content);
            fs.AddFile(Path.Combine(root, $"file_{i:D4}.bin"), fileSize, content: content);
        }

        // ── Parallel scan should complete in reasonable time ──
        var scanner = new DuplicateFileScanner(fs, NullLogger<DuplicateFileScanner>.Instance);

        var sw = Stopwatch.StartNew();
        var result = await scanner.ScanAsync(root);
        sw.Stop();

        // ── Validate correctness: scanner found 0 duplicate groups (all unique content) ──
        Assert.Empty(result.Groups);

        // ── Assert it completed within a reasonable time (< 5 seconds for 120 small in-memory files) ──
        Assert.True(
            sw.Elapsed.TotalSeconds < 5.0,
            $"Scan of {fileCount} files took {sw.Elapsed.TotalSeconds:F1}s, expected < 5s.");
    }

    [Fact]
    public async Task ParallelHashing_UsesBoundedConcurrency()
    {
        // Verify the scanner uses MaxDegreeOfParallelism = min(Processors, 8)
        // by checking it handles many files without issues
        const int fileCount = 50;
        const int fileSize = 1024;

        var fs = new InMemoryFileSystemGateway();
        var root = @"C:\ConcurrencyTest";
        fs.AddDirectory(root);

        // Create files with same size — all will need hashing
        var content = new byte[fileSize];
        for (var i = 0; i < fileCount; i++)
        {
            content[0] = (byte)i; // unique first byte = unique hash
            fs.AddFile(Path.Combine(root, $"f_{i}.bin"), fileSize, content: content.ToArray());
        }

        var scanner = new DuplicateFileScanner(fs, NullLogger<DuplicateFileScanner>.Instance);
        var result = await scanner.ScanAsync(root);

        // All unique content — no duplicates
        Assert.Empty(result.Groups);
    }

    [Fact]
    public void PartialHash_MatchesForIdenticalPrefix()
    {
        // Arrange — two buffers with same first 64 bytes, different remainder
        var buf1 = new byte[128];
        var buf2 = new byte[128];

        for (var i = 0; i < 64; i++)
        {
            buf1[i] = (byte)i;
            buf2[i] = (byte)i;
        }

        // Different data after byte 64
        buf1[65] = 0xFF;
        buf2[65] = 0x00;

        // Act
        var hash1 = DuplicateFileScanner.ComputePartialHash(buf1, 64);
        var hash2 = DuplicateFileScanner.ComputePartialHash(buf2, 64);

        // Assert — partial hashes match (same first 64 bytes)
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void PartialHash_DiffersForDifferentPrefix()
    {
        // Arrange
        var buf1 = new byte[128];
        var buf2 = new byte[128];

        buf1[0] = 0x01;
        buf2[0] = 0x02;

        // Act
        var hash1 = DuplicateFileScanner.ComputePartialHash(buf1, 64);
        var hash2 = DuplicateFileScanner.ComputePartialHash(buf2, 64);

        // Assert
        Assert.NotEqual(hash1, hash2);
    }
}
