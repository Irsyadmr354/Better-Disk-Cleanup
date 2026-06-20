using BetterDiskCleanup.Core.Safety;

namespace BetterDiskCleanup.Infrastructure.Safety;

public sealed class PathSafetyValidator : IPathSafetyValidator
{
    private readonly IReadOnlyList<(string Path, RiskLevel RiskLevel, string Description)> _whitelistEntries;
    private readonly IReadOnlyList<string> _blacklistRoots;
    private readonly HashSet<string> _driveRoots;

    public PathSafetyValidator()
    {
        _whitelistEntries = WhitelistPathResolver.ResolveAll(CleanupPathWhitelist.Entries);
        _blacklistRoots = BuildBlacklistRoots();
        _driveRoots = BuildDriveRoots();
    }

    public SafetyValidationResult Validate(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return SafetyValidationResult.Denied("Path is empty or whitespace.");
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException)
        {
            return SafetyValidationResult.Denied($"Unable to resolve path safely: {ex.Message}");
        }

        var normalizedFullPath = NormalizePath(fullPath);
        if (_driveRoots.Contains(normalizedFullPath))
        {
            return SafetyValidationResult.Denied("Drive root paths cannot be cleaned.");
        }

        string canonicalPath;
        try
        {
            canonicalPath = ResolveLinkTargetRecursive(fullPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return SafetyValidationResult.Denied($"Unable to resolve path safely: {ex.Message}");
        }

        var normalizedPath = NormalizePath(canonicalPath);

        if (_driveRoots.Contains(normalizedPath))
        {
            return SafetyValidationResult.Denied("Drive root paths cannot be cleaned.");
        }

        foreach (var whitelistEntry in _whitelistEntries)
        {
            if (IsSameOrSubPath(normalizedPath, whitelistEntry.Path))
            {
                return SafetyValidationResult.Allowed(
                    whitelistEntry.RiskLevel,
                    whitelistEntry.Description);
            }
        }

        foreach (var blacklistRoot in _blacklistRoots)
        {
            if (IsSameOrSubPath(normalizedPath, blacklistRoot))
            {
                return SafetyValidationResult.Denied(
                    $"Path resolves inside protected location '{blacklistRoot}'.");
            }
        }

        return SafetyValidationResult.Denied("Path is not on the cleanup whitelist.");
    }

    internal static string NormalizePath(string path)
    {
        var fullPath = Path.GetFullPath(path);

        if (fullPath.Length >= 2 && fullPath[1] == ':')
        {
            var root = Path.GetPathRoot(fullPath)
                ?? throw new ArgumentException($"Unable to determine drive root for '{path}'.", nameof(path));

            if (fullPath.Equals(root, StringComparison.OrdinalIgnoreCase))
            {
                return root.TrimEnd('\\').ToUpperInvariant() + "\\";
            }
        }

        return fullPath.TrimEnd('\\', '/');
    }

    private static IReadOnlyList<string> BuildBlacklistRoots()
    {
        var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        var roots = new List<string>
        {
            systemRoot,
            programFiles,
            programFilesX86,
            Path.Combine(systemRoot, "System32", "drivers"),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)
        };

        return roots
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Select(NormalizePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static HashSet<string> BuildDriveRoots()
    {
        return DriveInfo.GetDrives()
            .Select(drive => NormalizePath(drive.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string ResolveLinkTargetRecursive(string path)
    {
        var normalizedInput = Path.GetFullPath(path);

        if (Directory.Exists(normalizedInput) || File.Exists(normalizedInput))
        {
            var linkTarget = TryResolveLinkTarget(normalizedInput);
            if (linkTarget is not null)
            {
                var resolvedTarget = Path.GetFullPath(linkTarget);
                if (!resolvedTarget.Equals(normalizedInput, StringComparison.OrdinalIgnoreCase))
                {
                    return ResolveLinkTargetRecursive(resolvedTarget);
                }
            }

            return normalizedInput;
        }

        var parent = Directory.GetParent(normalizedInput);
        if (parent is null)
        {
            return normalizedInput;
        }

        var resolvedParent = ResolveLinkTargetRecursive(parent.FullName);
        if (resolvedParent.Equals(parent.FullName, StringComparison.OrdinalIgnoreCase))
        {
            return normalizedInput;
        }

        var relativeSuffix = normalizedInput[parent.FullName.Length..]
            .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return Path.GetFullPath(Path.Combine(resolvedParent, relativeSuffix));
    }

    private static bool IsSameOrSubPath(string candidatePath, string rootPath)
    {
        if (candidatePath.Equals(rootPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var rootPrefix = rootPath.EndsWith('\\') || rootPath.EndsWith('/')
            ? rootPath
            : rootPath + Path.DirectorySeparatorChar;

        return candidatePath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryResolveLinkTarget(string path)
    {
        try
        {
            FileSystemInfo fileSystemInfo = Directory.Exists(path)
                ? new DirectoryInfo(path)
                : new FileInfo(path);

            return fileSystemInfo.ResolveLinkTarget(returnFinalTarget: true)?.FullName;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
