using BetterDiskCleanup.Core.Settings;

namespace BetterDiskCleanup.Tests.Support;

public sealed class NullUserExclusionService : IUserExclusionService
{
    public bool IsExcluded(string path) => false;
    public Task AddExclusionAsync(string pattern) => Task.CompletedTask;
    public Task RemoveExclusionAsync(string pattern) => Task.CompletedTask;
    public IReadOnlyList<string> GetExclusions() => Array.Empty<string>();
}
