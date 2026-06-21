using System.Threading.Tasks;
using BetterDiskCleanup.App.ViewModels;
using BetterDiskCleanup.Core.Settings;
using Moq;
using Xunit;

namespace BetterDiskCleanup.Tests.Settings;

public class SettingsViewModelTests
{
    [Fact]
    public void Constructor_LoadsSettingsFromService()
    {
        // Arrange
        var mockService = new Mock<ISettingsService>();
        var settings = new AppSettings
        {
            EnableNotifications = false,
            CriticalDiskSpaceThresholdGb = 15,
            JunkFileWarningThresholdMb = 1000,
            LargeFileThresholdMb = 500
        };
        mockService.Setup(s => s.Current).Returns(settings);

        // Act
        var vm = new SettingsViewModel(mockService.Object);

        // Assert
        Assert.False(vm.EnableNotifications);
        Assert.Equal(15, vm.CriticalDiskSpaceThresholdGb);
        Assert.Equal(1000, vm.JunkFileWarningThresholdMb);
        Assert.Equal(500, vm.LargeFileThresholdMb);
    }

    [Fact]
    public async Task SaveCommand_SavesSettingsToService()
    {
        // Arrange
        var mockService = new Mock<ISettingsService>();
        var settings = new AppSettings();
        mockService.Setup(s => s.Current).Returns(settings);
        
        var vm = new SettingsViewModel(mockService.Object)
        {
            EnableNotifications = true,
            CriticalDiskSpaceThresholdGb = 20,
            JunkFileWarningThresholdMb = 2000,
            LargeFileThresholdMb = 1000
        };

        // Act
        vm.SaveCommand.Execute(null);

        // Allow some time for the async command to complete
        await Task.Delay(100);

        // Assert
        mockService.Verify(s => s.SaveAsync(It.Is<AppSettings>(a => 
            a.EnableNotifications == true &&
            a.CriticalDiskSpaceThresholdGb == 20 &&
            a.JunkFileWarningThresholdMb == 2000 &&
            a.LargeFileThresholdMb == 1000
        )), Times.Once);
    }
}
