namespace BetterDiskCleanup.Core.Settings;

public interface IUserExclusionService
{
    bool IsExcluded(string path);
    Task AddExclusionAsync(string pattern);
    Task RemoveExclusionAsync(string pattern);
    IReadOnlyList<string> GetExclusions();
}
