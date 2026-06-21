using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using BetterDiskCleanup.Core.Settings;

namespace BetterDiskCleanup.Infrastructure.Settings;

public class JsonSettingsService : ISettingsService
{
    private readonly string _settingsFilePath;
    private AppSettings _current;

    public event EventHandler<AppSettings>? SettingsChanged;

    public AppSettings Current => _current;

    public JsonSettingsService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(appData, "BetterDiskCleanup");
        Directory.CreateDirectory(appFolder);
        _settingsFilePath = Path.Combine(appFolder, "settings.json");

        _current = Load();
    }

    private AppSettings Load()
    {
        if (File.Exists(_settingsFilePath))
        {
            try
            {
                var json = File.ReadAllText(_settingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null) return settings;
            }
            catch { /* Ignore and return default */ }
        }
        return new AppSettings();
    }

    public async Task SaveAsync(AppSettings settings)
    {
        _current = settings;
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_settingsFilePath, json);
        SettingsChanged?.Invoke(this, _current);
    }
}
