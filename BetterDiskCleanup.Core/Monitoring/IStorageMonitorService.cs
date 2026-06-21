namespace BetterDiskCleanup.Core.Monitoring;

public interface IStorageMonitorService
{
    Task CheckDiskSpaceAsync(CancellationToken cancellationToken = default);
}
