namespace BetterDiskCleanup.Core.Duplicates;

public sealed class DuplicateFileEntry
{
    public required string Path { get; init; }
    public required string FileName { get; init; }
    public required long SizeBytes { get; init; }
    public required DateTime LastModifiedUtc { get; init; }
    public required DateTime CreatedUtc { get; init; }
}
