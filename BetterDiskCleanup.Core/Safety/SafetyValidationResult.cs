namespace BetterDiskCleanup.Core.Safety;

public sealed class SafetyValidationResult
{
    public required bool IsAllowed { get; init; }
    public required RiskLevel RiskLevel { get; init; }
    public required string Reason { get; init; }

    public static SafetyValidationResult Allowed(RiskLevel riskLevel, string reason = "Path is on the cleanup whitelist.")
        => new()
        {
            IsAllowed = true,
            RiskLevel = riskLevel,
            Reason = reason
        };

    public static SafetyValidationResult Denied(string reason, RiskLevel riskLevel = RiskLevel.Expert)
        => new()
        {
            IsAllowed = false,
            RiskLevel = riskLevel,
            Reason = reason
        };
}
