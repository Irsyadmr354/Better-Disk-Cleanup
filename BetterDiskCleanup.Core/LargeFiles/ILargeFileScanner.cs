namespace BetterDiskCleanup.Core.LargeFiles;

public interface ILargeFileScanner
{
    IReadOnlyList<string> GetAvailableDrives();

    Task<LargeFileScanResult> ScanAsync(
        string rootPath,
        long thresholdBytes,
        IProgress<LargeFileScanProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
