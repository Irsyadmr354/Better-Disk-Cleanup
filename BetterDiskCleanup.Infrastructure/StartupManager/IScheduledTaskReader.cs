namespace BetterDiskCleanup.Infrastructure.StartupManager;

/// <summary>
/// Internal abstraction over Task Scheduler COM API, enabling testability.
/// </summary>
internal interface IScheduledTaskReader
{
    /// <summary>
    /// Returns all scheduled tasks that have a logon or startup trigger.
    /// </summary>
    IReadOnlyList<ScheduledTaskInfo> GetStartupTasks();
}

/// <summary>
/// DTO for scheduled task information retrieved from Task Scheduler.
/// </summary>
internal sealed class ScheduledTaskInfo
{
    public required string Name { get; init; }
    public required string TaskPath { get; init; }
    public required string ExePath { get; init; }
    public required string Arguments { get; init; }
    public required bool IsEnabled { get; init; }
    public required string Xml { get; init; }
}
