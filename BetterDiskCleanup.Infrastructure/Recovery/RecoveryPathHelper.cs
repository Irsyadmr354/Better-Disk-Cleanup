using BetterDiskCleanup.Core.Recovery;
using BetterDiskCleanup.Core.Safety;

namespace BetterDiskCleanup.Infrastructure.Recovery;

internal static class RecoveryPathHelper
{
    internal const string RecoverySubFolder = "Recovery";

    internal static string GetRecoveryRoot(IRecoveryOptions options)
    {
        var userTempRoot = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return Path.Combine(userTempRoot, options.StagingFolderName, RecoverySubFolder);
    }

    internal static string GetSessionDirectory(IRecoveryOptions options, string sessionId) =>
        Path.Combine(GetRecoveryRoot(options), sessionId);

    internal static string GetManifestPath(IRecoveryOptions options, string sessionId) =>
        Path.Combine(GetSessionDirectory(options, sessionId), "manifest.json");

    internal static string GetStagedFilePath(string sessionDirectory, string itemId) =>
        Path.Combine(sessionDirectory, "files", itemId);

    internal static void EnsureRecoveryRootIsSafe(IPathSafetyValidator safetyValidator, string recoveryRoot)
    {
        var validation = safetyValidator.Validate(recoveryRoot);
        if (!validation.IsAllowed)
        {
            throw new InvalidOperationException(
                $"Recovery staging root '{recoveryRoot}' failed safety validation: {validation.Reason}");
        }
    }

    internal static bool IsUnderRecoveryStaging(string path, IRecoveryOptions options)
    {
        var recoveryRoot = Path.GetFullPath(GetRecoveryRoot(options));
        var candidatePath = Path.GetFullPath(path);

        if (candidatePath.Equals(recoveryRoot, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var recoveryPrefix = recoveryRoot.EndsWith('\\') || recoveryRoot.EndsWith('/')
            ? recoveryRoot
            : recoveryRoot + Path.DirectorySeparatorChar;

        return candidatePath.StartsWith(recoveryPrefix, StringComparison.OrdinalIgnoreCase);
    }

    internal const string TestArtifactsFolderName = "BetterDiskCleanup-tests";

    internal static bool IsUnderTestStaging(string path)
    {
        var userTempRoot = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var testRootPrefix = Path.Combine(userTempRoot, TestArtifactsFolderName) + Path.DirectorySeparatorChar;
        var candidatePath = Path.GetFullPath(path);

        if (!candidatePath.StartsWith(testRootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var relative = candidatePath[testRootPrefix.Length..];
        var stagingDirectorySuffix = $"{Path.DirectorySeparatorChar}staging";
        var stagingDirectoryPrefix = stagingDirectorySuffix + Path.DirectorySeparatorChar;

        return relative.Contains(stagingDirectoryPrefix, StringComparison.OrdinalIgnoreCase)
            || relative.EndsWith(stagingDirectorySuffix, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsExcludedFromTempScan(string path, IRecoveryOptions options) =>
        IsUnderRecoveryStaging(path, options) || IsUnderTestStaging(path);
}

public interface IRecoveryOptions
{
    int RetentionDays { get; }

    string StagingFolderName { get; }
}

internal sealed class RecoveryOptionsAdapter : IRecoveryOptions
{
    private readonly RecoveryOptions _options;

    public RecoveryOptionsAdapter(Microsoft.Extensions.Options.IOptions<RecoveryOptions> options)
    {
        _options = options.Value;
    }

    public int RetentionDays => _options.RetentionDays;

    public string StagingFolderName => _options.StagingFolderName;
}
