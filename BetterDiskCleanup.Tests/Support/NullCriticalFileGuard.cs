using BetterDiskCleanup.Core.Safety;

namespace BetterDiskCleanup.Tests.Support;

public sealed class NullCriticalFileGuard : ICriticalFileGuard
{
    public CriticalFileCheckResult Check(string path)
    {
        return new CriticalFileCheckResult { IsCritical = false, Reason = null };
    }
}
