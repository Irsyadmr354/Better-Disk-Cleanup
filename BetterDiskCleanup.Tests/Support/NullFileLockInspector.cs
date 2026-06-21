using BetterDiskCleanup.Core.Filesystem;

namespace BetterDiskCleanup.Tests.Support;

public sealed class NullFileLockInspector : IFileLockInspector
{
    public FileLockInfo? TryGetLockingProcess(string path) => null;
}
