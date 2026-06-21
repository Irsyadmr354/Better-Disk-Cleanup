using System.IO;
using System.Threading.Tasks;
using BetterDiskCleanup.Core.Settings;
using BetterDiskCleanup.Infrastructure.Settings;
using Xunit;

namespace BetterDiskCleanup.Tests.Settings;

public class JsonSettingsServiceTests : IDisposable
{
    private readonly string _tempFile;

    public JsonSettingsServiceTests()
    {
        _tempFile = Path.GetTempFileName();
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
            File.Delete(_tempFile);
    }

    [Fact]
    public void Constructor_WhenFileDoesNotExist_CreatesDefaultSettings()
    {
        File.Delete(_tempFile);
        var service = new JsonSettingsService(_tempFile);
        
        Assert.NotNull(service.Current);
        Assert.True(service.Current.EnableNotifications);
        Assert.Equal(10, service.Current.CriticalDiskSpaceThresholdGb);
    }

    [Fact]
    public async Task SaveAsync_WritesSettingsToFile()
    {
        var service = new JsonSettingsService(_tempFile);
        service.Current.EnableNotifications = false;
        service.Current.CriticalDiskSpaceThresholdGb = 5;

        await service.SaveAsync(service.Current);

        var content = await File.ReadAllTextAsync(_tempFile);
        Assert.Contains("\"EnableNotifications\": false", content);
        Assert.Contains("\"CriticalDiskSpaceThresholdGb\": 5", content);
    }

    [Fact]
    public async Task SaveAsync_ThenReload_ReadsSettingsFromFile()
    {
        var service = new JsonSettingsService(_tempFile);
        service.Current.EnableNotifications = false;
        
        await service.SaveAsync(service.Current);

        var service2 = new JsonSettingsService(_tempFile);
        Assert.False(service2.Current.EnableNotifications);
    }
}
