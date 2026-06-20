using BetterDiskCleanup.Core.Browsers;
using BetterDiskCleanup.Core.Filesystem;

namespace BetterDiskCleanup.Infrastructure.Browsers;

/// <summary>
/// Adapter for Firefox (Gecko engine).
/// Reads <c>profiles.ini</c> to enumerate all profiles.
/// </summary>
internal sealed class FirefoxBrowserAdapter : IBrowserAdapter
{
    private readonly IFileSystemGateway _fileSystem;
    private readonly string _firefoxAppDataPath;
    private readonly string _firefoxLocalCacheRoot;

    public FirefoxBrowserAdapter(IFileSystemGateway fileSystem)
    {
        _fileSystem = fileSystem;

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _firefoxAppDataPath = Path.Combine(appData, "Mozilla", "Firefox");

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _firefoxLocalCacheRoot = Path.Combine(localAppData, "Mozilla", "Firefox");
    }

    /// <summary>
    /// Constructor for testing — allows injecting custom paths.
    /// </summary>
    internal FirefoxBrowserAdapter(
        IFileSystemGateway fileSystem,
        string firefoxAppDataPath,
        string firefoxLocalCacheRoot)
    {
        _fileSystem = fileSystem;
        _firefoxAppDataPath = firefoxAppDataPath;
        _firefoxLocalCacheRoot = firefoxLocalCacheRoot;
    }

    public string BrowserName => "Firefox";
    public string Engine => "Gecko";
    public string ProcessName => "firefox";

    public IReadOnlyList<BrowserProfile> DetectProfiles()
    {
        var profilesIniPath = Path.Combine(_firefoxAppDataPath, "profiles.ini");
        if (!_fileSystem.FileExists(profilesIniPath))
        {
            return [];
        }

        var profiles = new List<BrowserProfile>();

        try
        {
            var iniContent = _fileSystem.ReadAllText(profilesIniPath);
            var parsedProfiles = ParseProfilesFromIni(iniContent);

            foreach (var (name, relativePath) in parsedProfiles)
            {
                // Firefox profiles.ini relative paths are relative to the Firefox app data directory
                var profilePath = Path.GetFullPath(Path.Combine(_firefoxAppDataPath, relativePath));

                if (_fileSystem.DirectoryExists(profilePath))
                {
                    profiles.Add(new BrowserProfile
                    {
                        BrowserName = BrowserName,
                        BrowserEngine = Engine,
                        ProfileName = name,
                        ProfilePath = profilePath,
                        ProcessName = ProcessName
                    });
                }
            }
        }
        catch (IOException)
        {
            // File inaccessible — skip.
        }

        return profiles;
    }

    /// <summary>
    /// Returns the local cache root for a Firefox profile (cache2 is under LocalAppData, not Roaming).
    /// </summary>
    internal string GetLocalCachePath(string profileName)
    {
        return Path.Combine(_firefoxLocalCacheRoot, "Profiles", profileName);
    }

    internal static IReadOnlyList<(string Name, string Path)> ParseProfilesFromIni(string iniContent)
    {
        var results = new List<(string Name, string Path)>();
        string? currentName = null;
        string? currentPath = null;
        var isRelative = true;

        foreach (var rawLine in iniContent.Split('\n'))
        {
            var line = rawLine.Trim();

            if (line.StartsWith("[Profile", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("[Install", StringComparison.OrdinalIgnoreCase))
            {
                // Flush previous profile section
                if (currentName is not null && currentPath is not null)
                {
                    results.Add((currentName, currentPath));
                }

                currentName = null;
                currentPath = null;
                isRelative = true;
                continue;
            }

            if (line.StartsWith("[General", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var equalsIndex = line.IndexOf('=');
            if (equalsIndex < 0)
            {
                continue;
            }

            var key = line[..equalsIndex].Trim();
            var value = line[(equalsIndex + 1)..].Trim();

            switch (key.ToLowerInvariant())
            {
                case "name":
                    currentName = value;
                    break;
                case "path":
                    currentPath = value.Replace('/', Path.DirectorySeparatorChar);
                    break;
                case "isrelative":
                    isRelative = value == "1";
                    break;
            }
        }

        // Flush last profile section
        if (currentName is not null && currentPath is not null)
        {
            results.Add((currentName, currentPath));
        }

        return results;
    }
}
