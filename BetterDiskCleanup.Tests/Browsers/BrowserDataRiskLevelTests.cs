using BetterDiskCleanup.Core.Browsers;
using BetterDiskCleanup.Core.Safety;
using BetterDiskCleanup.Infrastructure.Browsers;

namespace BetterDiskCleanup.Tests.Browsers;

public sealed class BrowserDataRiskLevelTests
{
    [Theory]
    [InlineData(BrowserDataType.Cache, RiskLevel.Safe)]
    [InlineData(BrowserDataType.Temporary, RiskLevel.Safe)]
    [InlineData(BrowserDataType.ServiceWorker, RiskLevel.Recommended)]
    [InlineData(BrowserDataType.Sessions, RiskLevel.Recommended)]
    [InlineData(BrowserDataType.Cookies, RiskLevel.Advanced)]
    [InlineData(BrowserDataType.History, RiskLevel.Advanced)]
    public void GetRiskLevel_ReturnsExpectedLevel(BrowserDataType dataType, RiskLevel expectedLevel)
    {
        var result = BrowserDataScanner.GetRiskLevel(dataType);

        Assert.Equal(expectedLevel, result);
    }

    [Fact]
    public void Cookies_HasHigherRiskThan_Cache()
    {
        var cacheLevel = BrowserDataScanner.GetRiskLevel(BrowserDataType.Cache);
        var cookiesLevel = BrowserDataScanner.GetRiskLevel(BrowserDataType.Cookies);

        Assert.True(cookiesLevel > cacheLevel,
            $"Cookies ({cookiesLevel}) should have higher risk than Cache ({cacheLevel})");
    }

    [Fact]
    public void History_HasHigherRiskThan_Cache()
    {
        var cacheLevel = BrowserDataScanner.GetRiskLevel(BrowserDataType.Cache);
        var historyLevel = BrowserDataScanner.GetRiskLevel(BrowserDataType.History);

        Assert.True(historyLevel > cacheLevel,
            $"History ({historyLevel}) should have higher risk than Cache ({cacheLevel})");
    }

    [Fact]
    public void Cookies_IsAdvanced_NotAutoSelectable()
    {
        var level = BrowserDataScanner.GetRiskLevel(BrowserDataType.Cookies);

        Assert.Equal(RiskLevel.Advanced, level);
    }

    [Fact]
    public void History_IsAdvanced_NotAutoSelectable()
    {
        var level = BrowserDataScanner.GetRiskLevel(BrowserDataType.History);

        Assert.Equal(RiskLevel.Advanced, level);
    }
}
