using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BetterDiskCleanup.Core.Browsers;
using BetterDiskCleanup.Infrastructure.Browsers;
using BetterDiskCleanup.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using BetterDiskCleanup.Core.Safety;
using Moq;
using Xunit;

namespace BetterDiskCleanup.Tests.Browsers;

public class BrowserDataScannerTests
{
    [Fact]
    public async Task ScanAsync_WithValidProfile_ReturnsFiles()
    {
        var fs = new InMemoryFileSystemGateway();
        var cacheDir = @"C:\Users\Test\AppData\Local\Google\Chrome\User Data\Default\Cache\Cache_Data";
        fs.CreateDirectory(cacheDir);
        fs.AddFile(Path.Combine(cacheDir, "data_1"), 1024);
        fs.AddFile(Path.Combine(cacheDir, "data_2"), 2048);

        var safetyValidator = new Mock<IPathSafetyValidator>();
        safetyValidator.Setup(v => v.Validate(It.IsAny<string>())).Returns(SafetyValidationResult.Allowed(RiskLevel.Safe));

        var scanner = new BrowserDataScanner(fs, safetyValidator.Object, NullLogger<BrowserDataScanner>.Instance);

        var profile = new BrowserProfile
        {
            BrowserName = "Google Chrome",
            ProfileName = "Default",
            ProfilePath = @"C:\Users\Test\AppData\Local\Google\Chrome\User Data\Default",
            BrowserEngine = "Chromium",
            ProcessName = "chrome"
        };

        var result = await scanner.ScanAsync(new[] { profile }, null, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Single(result.Profiles);
        Assert.NotEmpty(result.Entries);
        Assert.Equal(3072, result.TotalSizeBytes);
    }
}
