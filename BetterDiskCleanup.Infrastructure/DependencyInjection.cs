using BetterDiskCleanup.Core.Analysis;
using BetterDiskCleanup.Core.Browsers;
using BetterDiskCleanup.Core.Cleanup;
using BetterDiskCleanup.Core.Duplicates;
using BetterDiskCleanup.Core.Filesystem;
using BetterDiskCleanup.Core.LargeFiles;
using BetterDiskCleanup.Core.Recovery;
using BetterDiskCleanup.Core.Safety;
using BetterDiskCleanup.Core.StartupManager;
using BetterDiskCleanup.Core.StorageAnalyzer;
using BetterDiskCleanup.Infrastructure.Browsers;
using BetterDiskCleanup.Infrastructure.Cleanup;
using BetterDiskCleanup.Infrastructure.Duplicates;
using BetterDiskCleanup.Infrastructure.Filesystem;
using BetterDiskCleanup.Infrastructure.LargeFiles;
using BetterDiskCleanup.Infrastructure.Recovery;
using BetterDiskCleanup.Infrastructure.Safety;
using BetterDiskCleanup.Infrastructure.Scanning;
using BetterDiskCleanup.Infrastructure.StartupManager;
using BetterDiskCleanup.Infrastructure.StorageAnalyzer;
using BetterDiskCleanup.Infrastructure.Settings;
using BetterDiskCleanup.Infrastructure.Monitoring;
using BetterDiskCleanup.Core.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace BetterDiskCleanup.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBetterDiskCleanupInfrastructure(this IServiceCollection services)
    {
        services.AddOptions<RecoveryOptions>();
        services.AddSingleton<IPathSafetyValidator, PathSafetyValidator>();
        services.AddSingleton<ICriticalFileGuard, CriticalFileGuard>();
        services.AddSingleton<IFileSystemGateway, FileSystemGateway>();
        services.AddSingleton<IFileLockInspector, RestartManagerFileLockInspector>();
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
        services.AddSingleton<IDuplicateFileScanner, DuplicateFileScanner>();
        services.AddSingleton<IDuplicateSelectionStrategy, KeepNewestStrategy>();
        services.AddSingleton<IDuplicateSelectionStrategy, KeepOldestStrategy>();
        services.AddSingleton<IDuplicateSelectionStrategy, KeepOriginalStrategy>();
        services.AddSingleton<IDuplicateSelectionStrategy, ManualStrategy>();

        // Fase 3D — Startup Manager
        services.AddSingleton<IScheduledTaskReader, ComScheduledTaskReader>();
        services.AddSingleton<IStartupEntrySafetyValidator, StartupEntrySafetyValidator>();
        services.AddSingleton<IStartupImpactEstimator, StartupImpactEstimator>();
        services.AddSingleton<IStartupScanner, StartupScanner>();
        services.AddSingleton<IStartupChangeRecoveryService, StartupChangeRecoveryService>();
        services.AddSingleton<IStartupEntryManager, StartupEntryManager>();

        // Fase 4 — Storage Analyzer
        services.AddSingleton<IStorageAnalyzer, FolderSizeAnalyzer>();

        // Fase 5 — Settings & Monitoring
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddSingleton<IUserExclusionService, UserExclusionService>();
        services.AddHostedService<StorageMonitorService>();

        return services;
    }
}
