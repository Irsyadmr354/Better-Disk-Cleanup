namespace BetterDiskCleanup.Core.Browsers;

public sealed class BrowserProfile
{
    public required string BrowserName { get; init; }
    public required string BrowserEngine { get; init; }
    public required string ProfileName { get; init; }
    public required string ProfilePath { get; init; }
    public required string ProcessName { get; init; }
}
