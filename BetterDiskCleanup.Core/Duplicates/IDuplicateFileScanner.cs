namespace BetterDiskCleanup.Core.Duplicates;

public interface IDuplicateFileScanner
{
    Task<DuplicateScanResult> ScanAsync(
        string rootPath,
        IProgress<DuplicateScanProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
