using BetterDiskCleanup.Core.Settings;

namespace BetterDiskCleanup.Infrastructure.Settings;

public sealed class UserExclusionService : IUserExclusionService
{
    private readonly ISettingsService _settingsService;

    public UserExclusionService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public bool IsExcluded(string path)
    {
        var settings = _settingsService.Current;
        var patterns = settings.UserExclusionPatterns;

        if (patterns is null || patterns.Count == 0)
        {
            return false;
        }

        foreach (var pattern in patterns)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                continue;
            }

            if (pattern.EndsWith("*", StringComparison.Ordinal))
            {
                var prefix = pattern.Substring(0, pattern.Length - 1);
                if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            else
            {
                if (string.Equals(path, pattern, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public async Task AddExclusionAsync(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return;
        }

        var settings = _settingsService.Current;
        if (!settings.UserExclusionPatterns.Contains(pattern, StringComparer.OrdinalIgnoreCase))
        {
            settings.UserExclusionPatterns.Add(pattern);
            await _settingsService.SaveAsync(settings);
        }
    }

    public async Task RemoveExclusionAsync(string pattern)
    {
        var settings = _settingsService.Current;
        var toRemove = settings.UserExclusionPatterns
            .FirstOrDefault(p => string.Equals(p, pattern, StringComparison.OrdinalIgnoreCase));

        if (toRemove is not null)
        {
            settings.UserExclusionPatterns.Remove(toRemove);
            await _settingsService.SaveAsync(settings);
        }
    }

    public IReadOnlyList<string> GetExclusions()
    {
        return _settingsService.Current.UserExclusionPatterns.ToList();
    }
}
