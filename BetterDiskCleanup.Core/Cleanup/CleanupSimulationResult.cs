namespace BetterDiskCleanup.Core.Cleanup;

public sealed class CleanupSimulationResult
{
    public required int FileCount { get; init; }
    public required long RecoverableBytes { get; init; }
    public required IReadOnlyList<string> SkippedPaths { get; init; }
}
