using System;
using System.Threading;
using System.Threading.Tasks;
using BetterDiskCleanup.Core.Settings;
using BetterDiskCleanup.Infrastructure.Monitoring;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BetterDiskCleanup.Tests.Monitoring;

public class StorageMonitorServiceTests
{
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly Mock<ILogger<StorageMonitorService>> _mockLogger;

    public StorageMonitorServiceTests()
    {
        _mockSettingsService = new Mock<ISettingsService>();
        _mockLogger = new Mock<ILogger<StorageMonitorService>>();
    }

    [Fact]
    public async Task CheckDiskSpaceAsync_WhenNotificationsDisabled_DoesNothing()
    {
        // Arrange
        var settings = new AppSettings { EnableNotifications = false, CriticalDiskSpaceThresholdGb = 10000 };
        _mockSettingsService.Setup(s => s.Current).Returns(settings);
        var service = new StorageMonitorService(_mockSettingsService.Object, _mockLogger.Object);

        // Act
        await service.CheckDiskSpaceAsync(CancellationToken.None);

        // Assert
        // The service should return early and not throw any exception or log warnings
        _mockSettingsService.Verify(s => s.Current, Times.Once);
    }
    
    [Fact]
    public async Task CheckDiskSpaceAsync_WhenNotificationsEnabled_ExecutesSuccessfully()
    {
        // Arrange
        var settings = new AppSettings { EnableNotifications = true, CriticalDiskSpaceThresholdGb = 0.001 }; // Very low to not trigger toast
        _mockSettingsService.Setup(s => s.Current).Returns(settings);
        var service = new StorageMonitorService(_mockSettingsService.Object, _mockLogger.Object);

        // Act
        await service.CheckDiskSpaceAsync(CancellationToken.None);

        // Assert
        _mockSettingsService.Verify(s => s.Current, Times.Once);
    }
}
