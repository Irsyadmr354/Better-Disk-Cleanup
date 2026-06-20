namespace BetterDiskCleanup.Core.Analysis;

public interface ITempFileScanner
{
    Task<ScanResult> ScanAsync(
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
