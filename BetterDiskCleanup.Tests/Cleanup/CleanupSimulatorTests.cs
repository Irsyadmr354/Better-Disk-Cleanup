using BetterDiskCleanup.Core.Analysis;
using BetterDiskCleanup.Core.Recovery;
using BetterDiskCleanup.Core.Safety;
using BetterDiskCleanup.Infrastructure.Cleanup;
using BetterDiskCleanup.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BetterDiskCleanup.Tests.Cleanup;

public sealed class CleanupSimulatorTests
{
    [Fact]
    public async Task SimulateAsync_DoesNotDeleteAnyFiles()
    {
        var fileSystem = new InMemoryFileSystemGateway();
        var tempRoot = Path.Combine("in-memory", $"bdc-sim-{Guid.NewGuid():N}");
        fileSystem.AddDirectory(tempRoot);

        var fileOne = Path.Combine(tempRoot, "one.tmp");
        var fileTwo = Path.Combine(tempRoot, "two.tmp");
        fileSystem.AddFile(fileOne, 128);
        fileSystem.AddFile(fileTwo, 256);

        var validator = new AllowAllPathSafetyValidator();
        var simulator = new CleanupSimulator(
            validator,
            fileSystem,
            Options.Create(new RecoveryOptions { StagingFolderName = "BetterDiskCleanup" }),
            NullLogger<CleanupSimulator>.Instance);

        var scanResult = new ScanResult
        {
            FileCount = 2,
            FolderCount = 1,
            TotalSizeBytes = 384,
            Items =
            [
                new ScanItem { Path = fileOne, SizeBytes = 128, LastModifiedUtc = DateTime.UtcNow, RiskLevel = RiskLevel.Safe },
                new ScanItem { Path = fileTwo, SizeBytes = 256, LastModifiedUtc = DateTime.UtcNow, RiskLevel = RiskLevel.Safe }
            ],
            Warnings = []
        };

        var simulation = await simulator.SimulateAsync(scanResult);

        Assert.Equal(2, simulation.FileCount);
        Assert.Equal(384, simulation.RecoverableBytes);
        Assert.Empty(fileSystem.DeletedFiles);
        Assert.True(fileSystem.FileExists(fileOne));
        Assert.True(fileSystem.FileExists(fileTwo));
    }

    private sealed class AllowAllPathSafetyValidator : IPathSafetyValidator
    {
        public SafetyValidationResult Validate(string path) =>
            SafetyValidationResult.Allowed(RiskLevel.Safe);
    }
}
