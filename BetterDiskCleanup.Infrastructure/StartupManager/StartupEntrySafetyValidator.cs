using BetterDiskCleanup.Core.StartupManager;
using Microsoft.Extensions.Logging;

namespace BetterDiskCleanup.Infrastructure.StartupManager;

/// <summary>
/// Determines whether a startup entry is a protected system component.
///
/// Protection heuristic (both conditions must be true):
///   1. The executable is digitally signed by Microsoft Corporation
///   2. The executable resides in a Windows system directory
///      (System32, SysWOW64, or the Windows directory root)
///
/// This is evaluated at BOTH the service level (business logic) and the UI level.
/// Protected entries CANNOT be disabled or removed — the operation is rejected
/// regardless of how the request reaches the service.
/// </summary>
internal sealed class StartupEntrySafetyValidator : IStartupEntrySafetyValidator
{
    private readonly ILogger<StartupEntrySafetyValidator> _logger;

    // Injectable function for checking Microsoft signature (allows test override)
    private readonly Func<string, bool> _isMicrosoftSigned;

    // Directories considered "system" locations
    private static readonly string[] SystemDirectoryNames =
    {
        "system32",
        "syswow64",
        "windows"
    };

    public StartupEntrySafetyValidator(ILogger<StartupEntrySafetyValidator> logger)
        : this(logger, FileSignatureHelper.IsSignedByMicrosoft)
    {
    }

    /// <summary>
    /// Constructor that allows injecting the Microsoft-signature check for testing.
    /// </summary>
    internal StartupEntrySafetyValidator(
        ILogger<StartupEntrySafetyValidator> logger,
        Func<string, bool> isMicrosoftSigned)
    {
        _logger = logger;
        _isMicrosoftSigned = isMicrosoftSigned;
    }

    public bool IsProtected(StartupEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.FilePath))
            return false;

        var filePath = entry.FilePath;

        // Condition 1: Signed by Microsoft
        if (!_isMicrosoftSigned(filePath))
            return false;

        // Condition 2: In a system directory
        if (!IsInSystemDirectory(filePath))
            return false;

        _logger.LogDebug("Entry '{Name}' is protected (Microsoft-signed in system directory).", entry.Name);
        return true;
    }

    public void ValidateActionAllowed(StartupEntry entry, StartupChangeAction action)
    {
        if (action == StartupChangeAction.Enable)
            return; // Enable is always allowed

        if (IsProtected(entry))
        {
            throw new InvalidOperationException(
                $"Cannot {action.ToString().ToLowerInvariant()} entry '{entry.Name}': " +
                $"it is a protected system component (Microsoft-signed executable in a system directory).");
        }
    }

    internal static bool IsInSystemDirectory(string filePath)
    {
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (string.IsNullOrEmpty(directory))
                return false;

            var dirName = Path.GetFileName(directory).ToLowerInvariant();

            foreach (var sysDir in SystemDirectoryNames)
            {
                if (dirName == sysDir)
                    return true;
            }

            // Also check if it's directly in the Windows directory
            var windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (!string.IsNullOrEmpty(windowsDir))
            {
                var normalizedPath = Path.GetFullPath(filePath).ToLowerInvariant();
                var normalizedWinDir = Path.GetFullPath(windowsDir).ToLowerInvariant();

                // Direct child of Windows dir (not in a subfolder like System32)
                if (normalizedPath.StartsWith(normalizedWinDir + Path.DirectorySeparatorChar))
                {
                    var relativePath = normalizedPath[(normalizedWinDir.Length + 1)..];
                    if (!relativePath.Contains(Path.DirectorySeparatorChar))
                        return true;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}
