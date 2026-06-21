using System.IO;
using BetterDiskCleanup.Core.Monitoring;
using BetterDiskCleanup.Core.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Toolkit.Uwp.Notifications;

namespace BetterDiskCleanup.Infrastructure.Monitoring;

public class StorageMonitorService : BackgroundService, IStorageMonitorService
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<StorageMonitorService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(30);

    public StorageMonitorService(ISettingsService settingsService, ILogger<StorageMonitorService> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("StorageMonitorService is starting.");
        
        using var timer = new PeriodicTimer(_checkInterval);
        
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await CheckDiskSpaceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during disk space check.");
            }
        }
    }

    public Task CheckDiskSpaceAsync(CancellationToken cancellationToken = default)
    {
        var settings = _settingsService.Current;
        if (!settings.EnableNotifications)
        {
            return Task.CompletedTask;
        }

        try
        {
            var systemDrive = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System)) ?? "C:\\";
            var driveInfo = new DriveInfo(systemDrive);

            if (driveInfo.IsReady)
            {
                double freeSpaceGb = driveInfo.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                
                if (freeSpaceGb < settings.CriticalDiskSpaceThresholdGb)
                {
                    ShowToastNotification(
                        "Critical Disk Space", 
                        $"System drive ({systemDrive}) only has {freeSpaceGb:F1} GB free space remaining! Consider running Better Disk Cleanup."
                    );
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check disk space.");
        }

        return Task.CompletedTask;
    }

    private void ShowToastNotification(string title, string content)
    {
        try
        {
            new ToastContentBuilder()
                .AddText(title)
                .AddText(content)
                .Show();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to show toast notification.");
        }
    }
}
