namespace BetterDiskCleanup.Core.Safety;

public interface IPathSafetyValidator
{
    SafetyValidationResult Validate(string path);
}
