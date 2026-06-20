namespace BetterDiskCleanup.Core.Browsers;

public interface IBrowserDetector
{
    IReadOnlyList<BrowserProfile> DetectInstalledBrowsers();
}
