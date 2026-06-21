namespace BetterDiskCleanup.Core.Filesystem;

public interface IFileLockInspector
{
    FileLockInfo? TryGetLockingProcess(string path);
}

public sealed class FileLockInfo
{
    public required string ProcessName { get; init; }
    public required int ProcessId { get; init; }
}
