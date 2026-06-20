using BetterDiskCleanup.Core.Analysis;

namespace BetterDiskCleanup.Core.Browsers;

public interface IBrowserDataScanner
{
    Task<BrowserDataScanResult> ScanAsync(
        IReadOnlyList<BrowserProfile> profiles,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
