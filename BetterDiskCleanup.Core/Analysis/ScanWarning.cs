namespace BetterDiskCleanup.Core.Analysis;

public sealed class ScanWarning
{
    public required string Path { get; init; }
    public required string Message { get; init; }
}
