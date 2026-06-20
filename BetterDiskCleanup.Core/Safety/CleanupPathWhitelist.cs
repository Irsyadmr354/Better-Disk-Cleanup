namespace BetterDiskCleanup.Core.Safety;

public static class CleanupPathWhitelist
{
    public static IReadOnlyList<CleanupPathEntry> Entries { get; } =
    [
        new()
        {
            Id = "windows-temp",
            Template = WhitelistPathTemplate.SystemRootTemp,
            RiskLevel = RiskLevel.Recommended,
            Description = "Windows temporary files folder"
        },
        new()
        {
            Id = "user-temp",
            Template = WhitelistPathTemplate.UserTemp,
            RiskLevel = RiskLevel.Safe,
            Description = "Current user temporary files folder"
        },
        new()
        {
            Id = "local-appdata-temp",
            Template = WhitelistPathTemplate.LocalAppDataTemp,
            RiskLevel = RiskLevel.Safe,
            Description = "Local application temporary files folder"
        },
        new()
        {
            Id = "windows-prefetch",
            Template = WhitelistPathTemplate.WindowsPrefetch,
            RiskLevel = RiskLevel.Advanced,
            Description = "Windows Prefetch cache"
        },
        new()
        {
            Id = "software-distribution-download",
            Template = WhitelistPathTemplate.SoftwareDistributionDownload,
            RiskLevel = RiskLevel.Advanced,
            Description = "Windows Update download cache"
        },
        new()
        {
            Id = "delivery-optimization-cache",
            Template = WhitelistPathTemplate.DeliveryOptimizationCache,
            RiskLevel = RiskLevel.Advanced,
            Description = "Delivery Optimization cache"
        },
        new()
        {
            Id = "chrome-user-data",
            Template = WhitelistPathTemplate.ChromeUserData,
            RiskLevel = RiskLevel.Recommended,
            Description = "Google Chrome user data"
        },
        new()
        {
            Id = "edge-user-data",
            Template = WhitelistPathTemplate.EdgeUserData,
            RiskLevel = RiskLevel.Recommended,
            Description = "Microsoft Edge user data"
        },
        new()
        {
            Id = "brave-user-data",
            Template = WhitelistPathTemplate.BraveUserData,
            RiskLevel = RiskLevel.Recommended,
            Description = "Brave browser user data"
        },
        new()
        {
            Id = "opera-user-data",
            Template = WhitelistPathTemplate.OperaUserData,
            RiskLevel = RiskLevel.Recommended,
            Description = "Opera browser user data"
        },
        new()
        {
            Id = "vivaldi-user-data",
            Template = WhitelistPathTemplate.VivaldiUserData,
            RiskLevel = RiskLevel.Recommended,
            Description = "Vivaldi browser user data"
        },
        new()
        {
            Id = "firefox-profiles",
            Template = WhitelistPathTemplate.FirefoxProfiles,
            RiskLevel = RiskLevel.Recommended,
            Description = "Mozilla Firefox profiles"
        }
    ];
}
