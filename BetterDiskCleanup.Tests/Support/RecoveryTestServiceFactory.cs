using BetterDiskCleanup.Core.Cleanup;
using BetterDiskCleanup.Core.Recovery;
using BetterDiskCleanup.Infrastructure;
using BetterDiskCleanup.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BetterDiskCleanup.Tests.Support;

public static class RecoveryTestServiceFactory
{
    public static ServiceProvider Create(string stagingFolderName, int retentionDays = 30)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Options.Create(new RecoveryOptions
        {
            StagingFolderName = stagingFolderName,
            RetentionDays = retentionDays
        }));
        services.AddBetterDiskCleanupInfrastructure();
        services.AddSingleton<ICleanupFailureDetailLogger, NullCleanupFailureDetailLogger>();
        return services.BuildServiceProvider();
    }
}
