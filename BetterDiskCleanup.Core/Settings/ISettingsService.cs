namespace BetterDiskCleanup.Core.Settings;

public interface ISettingsService
{
    AppSettings Current { get; }
    Task SaveAsync(AppSettings settings);
    event EventHandler<AppSettings>? SettingsChanged;
}
