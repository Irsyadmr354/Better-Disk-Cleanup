namespace BetterDiskCleanup.Core.LargeFiles;

public sealed class LargeFileEntry
{
    public required string Path { get; init; }
    public required string FileName { get; init; }
    public required string Extension { get; init; }
    public required FileCategory Category { get; init; }
    public required long SizeBytes { get; init; }
    public required DateTime LastModifiedUtc { get; init; }
    public bool IsProtected { get; init; }
    public string? ProtectionReason { get; init; }
}
