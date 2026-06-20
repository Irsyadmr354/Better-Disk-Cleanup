namespace BetterDiskCleanup.Core.Cleanup;

public sealed class CleanupMessage
{
    public required string Path { get; init; }
    public required string Message { get; init; }
}
