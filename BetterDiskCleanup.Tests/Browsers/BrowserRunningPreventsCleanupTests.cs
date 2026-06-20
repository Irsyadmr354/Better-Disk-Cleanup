using BetterDiskCleanup.Core.Browsers;
using BetterDiskCleanup.Infrastructure.Browsers;

namespace BetterDiskCleanup.Tests.Browsers;

public sealed class BrowserRunningPreventsCleanupTests
{
    [Fact]
    public void GetRunningBrowsers_ReturnsEmpty_WhenNoBrowsersRunning()
    {
        var checker = new BrowserProcessChecker(_ => false);
        var profiles = new List<BrowserProfile>
        {
            CreateProfile("chrome"),
            CreateProfile("msedge")
        };

        var running = checker.GetRunningBrowserProcesses(profiles);

        Assert.Empty(running);
    }

    [Fact]
    public void GetRunningBrowsers_ReturnsProcessName_WhenBrowserIsRunning()
    {
        var checker = new BrowserProcessChecker(
            processName => processName.Equals("chrome", StringComparison.OrdinalIgnoreCase));

        var profiles = new List<BrowserProfile>
        {
            CreateProfile("chrome"),
            CreateProfile("msedge")
        };

        var running = checker.GetRunningBrowserProcesses(profiles);

        Assert.Single(running);
        Assert.Contains("chrome", running);
    }

    [Fact]
    public void GetRunningBrowsers_DeduplicatesByProcessName()
    {
        var checker = new BrowserProcessChecker(
            processName => processName.Equals("chrome", StringComparison.OrdinalIgnoreCase));

        // Two Chrome profiles should only produce one entry in the result
        var profiles = new List<BrowserProfile>
        {
            CreateProfile("chrome", "Default"),
            CreateProfile("chrome", "Profile 1")
        };

        var running = checker.GetRunningBrowserProcesses(profiles);

        Assert.Single(running);
    }

    [Fact]
    public void GetRunningBrowsers_ReturnsMultiple_WhenMultipleBrowsersRunning()
    {
        var runningSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "chrome", "firefox" };
        var checker = new BrowserProcessChecker(
            processName => runningSet.Contains(processName));

        var profiles = new List<BrowserProfile>
        {
            CreateProfile("chrome"),
            CreateProfile("firefox"),
            CreateProfile("msedge")
        };

        var running = checker.GetRunningBrowserProcesses(profiles);

        Assert.Equal(2, running.Count);
        Assert.Contains("chrome", running);
        Assert.Contains("firefox", running);
    }

    private static BrowserProfile CreateProfile(string processName, string profileName = "Default") => new()
    {
        BrowserName = processName == "firefox" ? "Firefox" : "Chrome",
        BrowserEngine = processName == "firefox" ? "Gecko" : "Chromium",
        ProfileName = profileName,
        ProfilePath = Path.Combine(Path.GetTempPath(), "test-profile"),
        ProcessName = processName
    };
}
