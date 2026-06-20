using BetterDiskCleanup.Core.Analysis;
using BetterDiskCleanup.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace BetterDiskCleanup.Tests.Scanning;

public sealed class TempFileScannerRecoveryExclusionTests : IDisposable
{
    private readonly IsolatedTestRun _isolatedRun;
    private readonly ServiceProvider _serviceProvider;

    public TempFileScannerRecoveryExclusionTests()
    {
        _isolatedRun = new IsolatedTestRun("scan-exclude");
        _serviceProvider = _isolatedRun.CreateServiceProvider();

        var recoveryRoot = _isolatedRun.GetRecoveryRoot();
        Directory.CreateDirectory(Path.Combine(recoveryRoot, "session-1", "files"));
    }

    [Fact]
    public async Task ScanAsync_ExcludesRecoveryStagingFiles()
    {
        var regularFile = Path.Combine(_isolatedRun.DataDirectory, "regular.tmp");
        var recoveryRoot = _isolatedRun.GetRecoveryRoot();
        var recoveryFile = Path.Combine(recoveryRoot, "session-1", "files", "staged-item");
        await File.WriteAllTextAsync(regularFile, "regular");
        await File.WriteAllTextAsync(recoveryFile, "staged");

        var scanner = _serviceProvider.GetRequiredService<ITempFileScanner>();
        var scanResult = await scanner.ScanAsync();

        Assert.Contains(scanResult.Items, item => item.Path.Equals(regularFile, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            scanResult.Items,
            item => item.Path.Equals(recoveryFile, StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        _isolatedRun.Dispose();
    }
}
