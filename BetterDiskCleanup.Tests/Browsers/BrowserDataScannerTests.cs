using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BetterDiskCleanup.Core.Browsers;
using BetterDiskCleanup.Infrastructure.Browsers;
using BetterDiskCleanup.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BetterDiskCleanup.Tests.Browsers;

public class BrowserDataScannerTests
{
    [Fact]
    public async Task ScanAsync_WithValidProfile_ReturnsFiles()
    {
        var fs = new InMemoryFileSystemGateway();
        var cacheDir = @"C:\Users\Test\AppData\Local\Google\Chrome\User Data\Default\Cache";
        fs.CreateDirectory(cacheDir);
        fs.CreateFile(Path.Combine(cacheDir, "data_1"), 1024);
        fs.CreateFile(Path.Combine(cacheDir, "data_2"), 2048);

        var adapters = new List<IBrowserAdapter> { new ChromiumBrowserAdapter(fs) };
        var scanner = new BrowserDataScanner(adapters, NullLogger<BrowserDataScanner>.Instance);

        var profile = new BrowserProfile
        {
            BrowserName = "Google Chrome",
            ProfileName = "Default",
            ProfilePath = @"C:\Users\Test\AppData\Local\Google\Chrome\User Data\Default",
            BrowserType = BrowserType.Chromium
        };

        var result = await scanner.ScanAsync(new[] { profile }, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Single(result.Profiles);
        Assert.NotEmpty(result.Entries);
        Assert.Equal(3072, result.TotalSizeBytes);
    }
}
