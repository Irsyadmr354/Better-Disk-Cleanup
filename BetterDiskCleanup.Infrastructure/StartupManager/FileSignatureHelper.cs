using System.Security.Cryptography.X509Certificates;

namespace BetterDiskCleanup.Infrastructure.StartupManager;

/// <summary>
/// Helpers for extracting publisher info from executable files.
/// </summary>
internal static class FileSignatureHelper
{
    /// <summary>
    /// Extracts the publisher/subject name from the Authenticode signature of a file.
    /// Returns empty string if the file is not signed or cannot be read.
    /// </summary>
    public static string GetPublisher(string filePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return string.Empty;

            using var cert = X509Certificate2.CreateFromSignedFile(filePath);
            return cert.Subject;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Checks whether the file is digitally signed by Microsoft Corporation.
    /// </summary>
    public static bool IsSignedByMicrosoft(string filePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return false;

            using var cert = X509Certificate2.CreateFromSignedFile(filePath);
            var subject = cert.Subject ?? string.Empty;
            var issuer = cert.Issuer ?? string.Empty;

            // Check if both subject and issuer indicate Microsoft
            return subject.Contains("Microsoft", StringComparison.OrdinalIgnoreCase)
                   && issuer.Contains("Microsoft", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Extracts the executable path from a command-line string.
    /// Handles quoted paths ("C:\Program Files\App\app.exe" --flag) and bare paths.
    /// </summary>
    public static string ExtractExePath(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
            return string.Empty;

        commandLine = commandLine.Trim();

        // Quoted path: "C:\path\to\exe" args...
        if (commandLine.StartsWith('"'))
        {
            var endQuote = commandLine.IndexOf('"', 1);
            if (endQuote > 1)
                return commandLine[1..endQuote];
        }

        // Bare path: take first token
        var spaceIdx = commandLine.IndexOf(' ');
        return spaceIdx > 0 ? commandLine[..spaceIdx] : commandLine;
    }

    /// <summary>
    /// Tries to resolve the target path of a .lnk shortcut file.
    /// Uses COM Shell.Link interface.
    /// </summary>
    public static string ResolveShortcutTarget(string shortcutPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(shortcutPath) || !File.Exists(shortcutPath))
                return string.Empty;

            // Use WScript.Shell COM to resolve shortcut target
            try
            {
                var shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return string.Empty;
                dynamic? shell = Activator.CreateInstance(shellType);
                if (shell == null) return string.Empty;
                var shortcut = shell.CreateShortcut(shortcutPath);
                return (string)shortcut.TargetPath;
            }
            catch
            {
                return string.Empty;
            }
        }
        catch
        {
            return string.Empty;
        }
    }
}
