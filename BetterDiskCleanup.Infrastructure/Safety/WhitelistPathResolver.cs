using BetterDiskCleanup.Core.Safety;

namespace BetterDiskCleanup.Infrastructure.Safety;

internal static class WhitelistPathResolver
{
    public static IReadOnlyList<(string Path, RiskLevel RiskLevel, string Description)> ResolveAll(
        IEnumerable<CleanupPathEntry> entries)
    {
        return entries
            .Select(entry => (Path: Resolve(entry.Template), entry.RiskLevel, entry.Description))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Path))
            .Select(entry => (Path: PathSafetyValidator.NormalizePath(entry.Path), entry.RiskLevel, entry.Description))
            .ToList();
    }

    private static string Resolve(WhitelistPathTemplate template)
    {
        var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        return template switch
        {
            WhitelistPathTemplate.SystemRootTemp => Path.Combine(systemRoot, "Temp"),
            WhitelistPathTemplate.UserTemp => Path.GetTempPath(),
            WhitelistPathTemplate.LocalAppDataTemp => Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Temp"),
            WhitelistPathTemplate.WindowsPrefetch => Path.Combine(systemRoot, "Prefetch"),
            WhitelistPathTemplate.SoftwareDistributionDownload => Path.Combine(
                systemRoot,
                "SoftwareDistribution",
                "Download"),
            WhitelistPathTemplate.DeliveryOptimizationCache => Path.Combine(
                systemRoot,
                "ServiceProfiles",
                "NetworkService",
                "AppData",
                "Local",
                "Microsoft",
                "Windows",
                "DeliveryOptimization",
                "Cache"),
            WhitelistPathTemplate.ChromeUserData => Path.Combine(
                localAppData, "Google", "Chrome", "User Data"),
            WhitelistPathTemplate.EdgeUserData => Path.Combine(
                localAppData, "Microsoft", "Edge", "User Data"),
            WhitelistPathTemplate.BraveUserData => Path.Combine(
                localAppData, "BraveSoftware", "Brave-Browser", "User Data"),
            WhitelistPathTemplate.OperaUserData => Path.Combine(
                appData, "Opera Software", "Opera Stable"),
            WhitelistPathTemplate.VivaldiUserData => Path.Combine(
                localAppData, "Vivaldi", "User Data"),
            WhitelistPathTemplate.FirefoxProfiles => Path.Combine(
                appData, "Mozilla", "Firefox", "Profiles"),
            _ => throw new ArgumentOutOfRangeException(nameof(template), template, "Unknown whitelist template.")
        };
    }
}
