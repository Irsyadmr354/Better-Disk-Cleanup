using System.Text.Json;
using BetterDiskCleanup.Core.Browsers;
using BetterDiskCleanup.Core.Filesystem;

namespace BetterDiskCleanup.Infrastructure.Browsers;

/// <summary>
/// Adapter for Chromium-based browsers (Chrome, Edge, Brave, Opera, Vivaldi).
/// Reads the <c>Local State</c> JSON file to enumerate all profiles.
/// </summary>
internal sealed class ChromiumBrowserAdapter : IBrowserAdapter
{
    private readonly IFileSystemGateway _fileSystem;
    private readonly string _userDataPath;

    public ChromiumBrowserAdapter(
        IFileSystemGateway fileSystem,
        string browserName,
        string userDataPath,
        string processName)
    {
        _fileSystem = fileSystem;
        BrowserName = browserName;
        _userDataPath = userDataPath;
        ProcessName = processName;
    }

    public string BrowserName { get; }
    public string Engine => "Chromium";
    public string ProcessName { get; }

    public IReadOnlyList<BrowserProfile> DetectProfiles()
    {
        if (!_fileSystem.DirectoryExists(_userDataPath))
        {
            return [];
        }

        var localStatePath = Path.Combine(_userDataPath, "Local State");
        if (!_fileSystem.FileExists(localStatePath))
        {
            return [];
        }

        var profiles = new List<BrowserProfile>();

        try
        {
            var json = _fileSystem.ReadAllText(localStatePath);
            var profileNames = ParseProfileNamesFromLocalState(json);

            foreach (var profileName in profileNames)
            {
                var profilePath = Path.Combine(_userDataPath, profileName);
                if (_fileSystem.DirectoryExists(profilePath))
                {
                    profiles.Add(new BrowserProfile
                    {
                        BrowserName = BrowserName,
                        BrowserEngine = Engine,
                        ProfileName = profileName,
                        ProfilePath = profilePath,
                        ProcessName = ProcessName
                    });
                }
            }
        }
        catch (JsonException)
        {
            // Malformed Local State — skip this browser.
        }
        catch (IOException)
        {
            // File locked or inaccessible — skip.
        }

        return profiles;
    }

    internal static IReadOnlyList<string> ParseProfileNamesFromLocalState(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (!root.TryGetProperty("profile", out var profileSection))
        {
            return [];
        }

        if (!profileSection.TryGetProperty("info_cache", out var infoCache))
        {
            return [];
        }

        return infoCache
            .EnumerateObject()
            .Select(property => property.Name)
            .ToList();
    }
}
