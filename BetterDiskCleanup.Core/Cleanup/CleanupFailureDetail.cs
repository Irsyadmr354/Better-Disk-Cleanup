namespace BetterDiskCleanup.Core.Cleanup;

public sealed class CleanupFailureDetail
{
    public required CleanupFailureStage Stage { get; init; }
    public required string Path { get; init; }
    public string? ExceptionType { get; init; }
    public string? ExceptionMessage { get; init; }
    public int? HResult { get; init; }
    public string? AdditionalContext { get; init; }
}
