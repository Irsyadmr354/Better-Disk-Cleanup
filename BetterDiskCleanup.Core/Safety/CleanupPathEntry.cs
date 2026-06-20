namespace BetterDiskCleanup.Core.Safety;

public sealed class CleanupPathEntry
{
    public required string Id { get; init; }
    public required WhitelistPathTemplate Template { get; init; }
    public required RiskLevel RiskLevel { get; init; }
    public required string Description { get; init; }
}
