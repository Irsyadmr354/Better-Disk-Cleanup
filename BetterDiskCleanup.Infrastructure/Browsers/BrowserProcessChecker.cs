using System.Diagnostics;
using BetterDiskCleanup.Core.Browsers;

namespace BetterDiskCleanup.Infrastructure.Browsers;

public sealed class BrowserProcessChecker : IBrowserProcessChecker
{
    private readonly Func<string, bool>? _processExistsOverride;

    public BrowserProcessChecker()
    {
    }

    /// <summary>
    /// Constructor for testing — allows injecting a fake process checker.
    /// </summary>
    internal BrowserProcessChecker(Func<string, bool> processExistsOverride)
    {
        _processExistsOverride = processExistsOverride;
    }

    public IReadOnlyList<string> GetRunningBrowserProcesses(IReadOnlyList<BrowserProfile> profiles)
    {
        var runningProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var profile in profiles)
        {
            if (IsProcessRunning(profile.ProcessName))
            {
                runningProcesses.Add(profile.ProcessName);
            }
        }

        return runningProcesses.ToList();
    }

    private bool IsProcessRunning(string processName)
    {
        if (_processExistsOverride is not null)
        {
            return _processExistsOverride(processName);
        }

        try
        {
            return Process.GetProcessesByName(processName).Length > 0;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
