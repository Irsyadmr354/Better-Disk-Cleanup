using BetterDiskCleanup.Core.Analysis;
using BetterDiskCleanup.Core.Browsers;
using BetterDiskCleanup.Core.Cleanup;
using BetterDiskCleanup.Core.Filesystem;
using BetterDiskCleanup.Core.LargeFiles;
using BetterDiskCleanup.Core.Recovery;
using BetterDiskCleanup.Core.Safety;
using BetterDiskCleanup.Infrastructure.Browsers;
using BetterDiskCleanup.Infrastructure.Cleanup;
using BetterDiskCleanup.Infrastructure.Filesystem;
using BetterDiskCleanup.Infrastructure.LargeFiles;
using BetterDiskCleanup.Infrastructure.Recovery;
using BetterDiskCleanup.Infrastructure.Safety;
using BetterDiskCleanup.Infrastructure.Scanning;
using Microsoft.Extensions.DependencyInjection;

namespace BetterDiskCleanup.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBetterDiskCleanupInfrastructure(this IServiceCollection services)
    {
        services.AddOptions<RecoveryOptions>();
        services.AddSingleton<IPathSafetyValidator, PathSafetyValidator>();
        services.AddSingleton<IFileSystemGateway, FileSystemGateway>();
        services.AddSingleton<ICleanupFailureDetailLogger, CleanupFailureDetailFileLogger>();
        services.AddSingleton<ITempFileScanner, TempFileScanner>();
        services.AddSingleton<ICleanupSimulator, CleanupSimulator>();
        services.AddSingleton<IRecoverySnapshotService, RecoverySnapshotService>();
        services.AddSingleton<RecoveryService>();
        services.AddSingleton<IRecoveryService>(provider => provider.GetRequiredService<RecoveryService>());
        services.AddSingleton<IRecoveryCleanupService, RecoveryCleanupService>();
        services.AddSingleton<ICleanupExecutor, CleanupExecutor>();
        services.AddSingleton<IBrowserDetector, BrowserDetector>();
        services.AddSingleton<IBrowserProcessChecker, BrowserProcessChecker>();
        services.AddSingleton<IBrowserDataScanner, BrowserDataScanner>();
        services.AddSingleton<ILargeFileScanner, LargeFileScanner>();
        return services;
    }
}
