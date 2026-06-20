using BetterDiskCleanup.Core.Safety;
using BetterDiskCleanup.Infrastructure.Safety;
using BetterDiskCleanup.Tests.Support;

namespace BetterDiskCleanup.Tests.Safety;

public sealed class PathSafetyValidatorTests : IDisposable
{
    private readonly IPathSafetyValidator _validator = new PathSafetyValidator();
    private readonly IsolatedTestRun _isolatedRun;
    private readonly string _junctionPath;

    public PathSafetyValidatorTests()
    {
        _isolatedRun = new IsolatedTestRun("junction");
        _junctionPath = Path.Combine(_isolatedRun.DataDirectory, "system32-junction");
    }

    [Fact]
    public void Validate_System32Path_IsRejected()
    {
        var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var system32Path = Path.Combine(systemRoot, "System32");

        var result = _validator.Validate(system32Path);

        Assert.False(result.IsAllowed);
        Assert.Contains("protected", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_WindowsTempPath_IsAllowed()
    {
        var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var windowsTempPath = Path.Combine(systemRoot, "Temp");

        var result = _validator.Validate(windowsTempPath);

        Assert.True(result.IsAllowed);
        Assert.Equal(RiskLevel.Recommended, result.RiskLevel);
    }

    [Fact]
    public void Validate_JunctionPointingToSystem32_IsRejected()
    {
        var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var system32Path = Path.Combine(systemRoot, "System32");

        CreateDirectoryJunction(_junctionPath, system32Path);

        var result = _validator.Validate(_junctionPath);

        Assert.False(result.IsAllowed);
        Assert.Contains("protected", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_UserDocumentsPath_IsRejected()
    {
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        var result = _validator.Validate(documentsPath);

        Assert.False(result.IsAllowed);
        Assert.Contains("protected", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_DriveRoot_IsRejected()
    {
        var systemDrive = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System))!;

        var result = _validator.Validate(systemDrive);

        Assert.False(result.IsAllowed);
        Assert.Contains("Drive root", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_UnknownPath_IsRejectedByDefaultDeny()
    {
        var systemDrive = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System))!;
        var unknownPath = Path.Combine(systemDrive.TrimEnd('\\'), "BetterDiskCleanup", "UnknownCleanupTarget");

        var result = _validator.Validate(unknownPath);

        Assert.False(result.IsAllowed);
        Assert.Contains("whitelist", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_RelativePathToWindows_IsResolvedThenRejected()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var relativeWindowsPath = Path.Combine(userProfile, "..", "..", "Windows");

        var result = _validator.Validate(relativeWindowsPath);

        Assert.False(result.IsAllowed);
        Assert.Contains("protected", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        RemoveDirectoryJunction(_junctionPath);
        _isolatedRun.Dispose();
    }

    private static void CreateDirectoryJunction(string junctionPath, string targetPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(junctionPath)!);

        var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c mklink /J \"{junctionPath}\" \"{targetPath}\"",
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        }) ?? throw new InvalidOperationException("Failed to start junction creation process.");

        process.WaitForExit();

        if (process.ExitCode != 0 || !Directory.Exists(junctionPath))
        {
            var error = process.StandardError.ReadToEnd();
            throw new InvalidOperationException($"Failed to create junction '{junctionPath}': {error}");
        }
    }

    private static void RemoveDirectoryJunction(string junctionPath)
    {
        if (!Directory.Exists(junctionPath))
        {
            return;
        }

        var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c rmdir \"{junctionPath}\"",
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });

        process?.WaitForExit();
    }
}
