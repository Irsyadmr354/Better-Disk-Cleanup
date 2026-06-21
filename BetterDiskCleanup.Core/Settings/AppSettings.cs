namespace BetterDiskCleanup.Core.Settings;

public class AppSettings
{
    // Appearance
    public bool IsDarkMode { get; set; } = true;

    // Notifications
    public bool EnableNotifications { get; set; } = true;
    public double CriticalDiskSpaceThresholdGb { get; set; } = 5.0;
    public double JunkFileWarningThresholdMb { get; set; } = 1000.0;

    // Cleanup Rules
    public int LargeFileThresholdMb { get; set; } = 500;
    public int RecoveryRetentionDays { get; init; } = 30;
    public string DefaultDuplicateSelectionStrategy { get; set; } = "KeepNewest";
    
    // Custom Exclusions
    public List<string> UserExclusionPatterns { get; set; } = new();
}
