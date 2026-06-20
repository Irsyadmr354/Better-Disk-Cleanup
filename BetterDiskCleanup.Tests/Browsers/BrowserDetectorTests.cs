using BetterDiskCleanup.Infrastructure.Browsers;
using BetterDiskCleanup.Tests.Support;

namespace BetterDiskCleanup.Tests.Browsers;

public sealed class BrowserDetectorTests
{
    [Fact]
    public void ParseProfileNamesFromLocalState_SingleProfile_ReturnsDefault()
    {
        const string json = """
        {
            "profile": {
                "info_cache": {
                    "Default": {
                        "name": "Person 1",
                        "gaia_name": "Test User"
                    }
                }
            }
        }
        """;

        var result = ChromiumBrowserAdapter.ParseProfileNamesFromLocalState(json);

        Assert.Single(result);
        Assert.Equal("Default", result[0]);
    }

    [Fact]
    public void ParseProfileNamesFromLocalState_MultipleProfiles_ReturnsAll()
    {
        const string json = """
        {
            "profile": {
                "info_cache": {
                    "Default": {
                        "name": "Person 1"
                    },
                    "Profile 1": {
                        "name": "Work"
                    },
                    "Profile 2": {
                        "name": "Personal"
                    }
                }
            }
        }
        """;

        var result = ChromiumBrowserAdapter.ParseProfileNamesFromLocalState(json);

        Assert.Equal(3, result.Count);
        Assert.Contains("Default", result);
        Assert.Contains("Profile 1", result);
        Assert.Contains("Profile 2", result);
    }

    [Fact]
    public void ParseProfileNamesFromLocalState_NoProfileSection_ReturnsEmpty()
    {
        const string json = """
        {
            "browser": {
                "enabled_labs_experiments": []
            }
        }
        """;

        var result = ChromiumBrowserAdapter.ParseProfileNamesFromLocalState(json);

        Assert.Empty(result);
    }

    [Fact]
    public void ChromiumAdapter_DetectProfiles_ReadsFromFileSystem()
    {
        var gateway = new InMemoryFileSystemGateway();
        var userDataPath = Path.Combine(Path.GetTempPath(), "bdc-test-chrome");
        var defaultPath = Path.Combine(userDataPath, "Default");
        var profile1Path = Path.Combine(userDataPath, "Profile 1");

        gateway.AddDirectory(userDataPath);
        gateway.AddDirectory(defaultPath);
        gateway.AddDirectory(profile1Path);

        const string localState = """
        {
            "profile": {
                "info_cache": {
                    "Default": { "name": "Person 1" },
                    "Profile 1": { "name": "Work" }
                }
            }
        }
        """;

        gateway.AddFile(
            Path.Combine(userDataPath, "Local State"),
            localState.Length,
            content: System.Text.Encoding.UTF8.GetBytes(localState));

        var adapter = new ChromiumBrowserAdapter(gateway, "Google Chrome", userDataPath, "chrome");
        var profiles = adapter.DetectProfiles();

        Assert.Equal(2, profiles.Count);
        Assert.All(profiles, p =>
        {
            Assert.Equal("Google Chrome", p.BrowserName);
            Assert.Equal("Chromium", p.BrowserEngine);
            Assert.Equal("chrome", p.ProcessName);
        });
        Assert.Contains(profiles, p => p.ProfileName == "Default");
        Assert.Contains(profiles, p => p.ProfileName == "Profile 1");
    }

    [Fact]
    public void ChromiumAdapter_DetectProfiles_MissingDirectory_ReturnsEmpty()
    {
        var gateway = new InMemoryFileSystemGateway();
        var adapter = new ChromiumBrowserAdapter(
            gateway, "Chrome", Path.Combine(Path.GetTempPath(), "nonexistent-chrome"), "chrome");

        var profiles = adapter.DetectProfiles();

        Assert.Empty(profiles);
    }

    [Fact]
    public void ParseProfilesFromIni_SingleProfile()
    {
        const string ini = """
        [General]
        StartWithLastProfile=1

        [Profile0]
        Name=default
        IsRelative=1
        Path=Profiles/abc123.default
        Default=1
        """;

        var result = FirefoxBrowserAdapter.ParseProfilesFromIni(ini);

        Assert.Single(result);
        Assert.Equal("default", result[0].Name);
        Assert.Contains("abc123.default", result[0].Path);
    }

    [Fact]
    public void ParseProfilesFromIni_MultipleProfiles()
    {
        const string ini = """
        [General]
        StartWithLastProfile=1

        [Profile0]
        Name=default
        IsRelative=1
        Path=Profiles/abc123.default
        Default=1

        [Profile1]
        Name=dev-edition-default
        IsRelative=1
        Path=Profiles/xyz789.dev-edition-default
        """;

        var result = FirefoxBrowserAdapter.ParseProfilesFromIni(ini);

        Assert.Equal(2, result.Count);
        Assert.Equal("default", result[0].Name);
        Assert.Equal("dev-edition-default", result[1].Name);
    }

    [Fact]
    public void FirefoxAdapter_DetectProfiles_ReadsFromFileSystem()
    {
        var gateway = new InMemoryFileSystemGateway();
        var appDataPath = Path.Combine(Path.GetTempPath(), "bdc-test-firefox-appdata");
        var profilePath = Path.Combine(appDataPath, "Profiles", "abc123.default");

        gateway.AddDirectory(appDataPath);
        gateway.AddDirectory(profilePath);

        const string profilesIni = """
        [General]
        StartWithLastProfile=1

        [Profile0]
        Name=default
        IsRelative=1
        Path=Profiles/abc123.default
        Default=1
        """;

        gateway.AddFile(
            Path.Combine(appDataPath, "profiles.ini"),
            profilesIni.Length,
            content: System.Text.Encoding.UTF8.GetBytes(profilesIni));

        var adapter = new FirefoxBrowserAdapter(
            gateway,
            appDataPath,
            Path.Combine(Path.GetTempPath(), "bdc-test-firefox-local"));

        var profiles = adapter.DetectProfiles();

        Assert.Single(profiles);
        Assert.Equal("Firefox", profiles[0].BrowserName);
        Assert.Equal("Gecko", profiles[0].BrowserEngine);
        Assert.Equal("firefox", profiles[0].ProcessName);
    }
}
