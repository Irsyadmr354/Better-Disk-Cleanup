namespace BetterDiskCleanup.Core.Safety;

public interface ICriticalFileGuard
{
    CriticalFileCheckResult Check(string path);
}

public sealed class CriticalFileCheckResult
{
    public required bool IsCritical { get; init; }
    public required string? Reason { get; init; }
}
