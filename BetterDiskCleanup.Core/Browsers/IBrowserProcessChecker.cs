namespace BetterDiskCleanup.Core.Browsers;

public interface IBrowserProcessChecker
{
    /// <summary>
    /// Returns the process names (from the provided profiles) that currently
    /// have running processes on this machine.
    /// </summary>
    IReadOnlyList<string> GetRunningBrowserProcesses(IReadOnlyList<BrowserProfile> profiles);
}
