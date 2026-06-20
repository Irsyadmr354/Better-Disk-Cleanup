using BetterDiskCleanup.Core.Browsers;
using BetterDiskCleanup.Core.Filesystem;

namespace BetterDiskCleanup.Infrastructure.Browsers;

public sealed class BrowserDetector : IBrowserDetector
{
    private readonly IReadOnlyList<IBrowserAdapter> _adapters;

    public BrowserDetector(IFileSystemGateway fileSystem)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        _adapters = CreateAdapters(fileSystem, localAppData, appData);
    }

    /// <summary>
    /// Constructor for testing — accepts pre-built adapters.
    /// </summary>
    internal BrowserDetector(IReadOnlyList<IBrowserAdapter> adapters)
    {
        _adapters = adapters;
    }

    public IReadOnlyList<BrowserProfile> DetectInstalledBrowsers()
    {
        var profiles = new List<BrowserProfile>();

        foreach (var adapter in _adapters)
        {
            try
            {
                profiles.AddRange(adapter.DetectProfiles());
            }
            catch (Exception)
            {
                // Individual adapter failure — skip silently.
            }
        }

        return profiles;
    }

    private static IReadOnlyList<IBrowserAdapter> CreateAdapters(
        IFileSystemGateway fileSystem,
        string localAppData,
        string appData)
    {
        return
        [
            new ChromiumBrowserAdapter(
                fileSystem, "Google Chrome",
                Path.Combine(localAppData, "Google", "Chrome", "User Data"),
                "chrome"),

            new ChromiumBrowserAdapter(
                fileSystem, "Microsoft Edge",
                Path.Combine(localAppData, "Microsoft", "Edge", "User Data"),
                "msedge"),

            new ChromiumBrowserAdapter(
                fileSystem, "Brave",
                Path.Combine(localAppData, "BraveSoftware", "Brave-Browser", "User Data"),
                "brave"),

            new ChromiumBrowserAdapter(
                fileSystem, "Opera",
                Path.Combine(appData, "Opera Software", "Opera Stable"),
                "opera"),

            new ChromiumBrowserAdapter(
                fileSystem, "Vivaldi",
                Path.Combine(localAppData, "Vivaldi", "User Data"),
                "vivaldi"),

            new FirefoxBrowserAdapter(fileSystem)
        ];
    }
}
