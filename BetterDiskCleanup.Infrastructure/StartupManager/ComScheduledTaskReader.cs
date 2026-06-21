using Microsoft.Extensions.Logging;

namespace BetterDiskCleanup.Infrastructure.StartupManager;

/// <summary>
/// Reads scheduled tasks via Task Scheduler COM API (TaskScheduler.TaskScheduler).
/// Filters for tasks with logon or boot triggers.
/// </summary>
internal sealed class ComScheduledTaskReader : IScheduledTaskReader
{
    private readonly ILogger<ComScheduledTaskReader> _logger;

    // Task Scheduler trigger type constants
    private const int TaskTriggerBoot = 8;
    private const int TaskTriggerLogon = 9;

    // Action type constant
    private const int TaskActionExec = 0;

    public ComScheduledTaskReader(ILogger<ComScheduledTaskReader> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<ScheduledTaskInfo> GetStartupTasks()
    {
        var results = new List<ScheduledTaskInfo>();

        try
        {
            var tsType = Type.GetTypeFromProgID("Schedule.Service");
            if (tsType == null)
            {
                _logger.LogWarning("Task Scheduler COM type not available.");
                return results;
            }

            dynamic? ts = Activator.CreateInstance(tsType);
            if (ts == null) return results;

            try
            {
                ts.Connect();
                dynamic rootFolder = ts.GetFolder("\\");
                CollectTasksFromFolder(rootFolder, results);
            }
            finally
            {
                // COM cleanup handled by GC
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enumerate scheduled tasks via COM.");
        }

        return results;
    }

    private void CollectTasksFromFolder(dynamic folder, List<ScheduledTaskInfo> results)
    {
        try
        {
            // GetTasks(1) includes hidden tasks
            dynamic tasks = folder.GetTasks(1);

            foreach (dynamic task in tasks)
            {
                try
                {
                    dynamic definition = task.Definition;
                    dynamic triggers = definition.Triggers;

                    bool isStartupTrigger = false;
                    foreach (dynamic trigger in triggers)
                    {
                        int triggerType = (int)trigger.Type;
                        if (triggerType == TaskTriggerBoot || triggerType == TaskTriggerLogon)
                        {
                            isStartupTrigger = true;
                            break;
                        }
                    }

                    if (!isStartupTrigger)
                        continue;

                    // Extract executable path from first Exec action
                    string exePath = string.Empty;
                    string arguments = string.Empty;
                    dynamic actions = definition.Actions;

                    foreach (dynamic action in actions)
                    {
                        try
                        {
                            int actionType = (int)action.Type;
                            if (actionType == TaskActionExec)
                            {
                                exePath = (string)action.Path ?? string.Empty;
                                arguments = (string)(action.Arguments ?? string.Empty);
                                break;
                            }
                        }
                        catch { /* skip non-exec actions */ }
                    }

                    if (string.IsNullOrWhiteSpace(exePath))
                        continue;

                    // Get task XML
                    string xml = string.Empty;
                    try { xml = (string)task.Xml; } catch { }

                    string taskPath = string.Empty;
                    try { taskPath = (string)task.Path; } catch { }

                    string taskName = string.Empty;
                    try { taskName = (string)task.Name; } catch { }

                    bool isEnabled = true;
                    try { isEnabled = (bool)task.Enabled; } catch { }

                    results.Add(new ScheduledTaskInfo
                    {
                        Name = taskName,
                        TaskPath = taskPath,
                        ExePath = exePath,
                        Arguments = arguments,
                        IsEnabled = isEnabled,
                        Xml = xml
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Skipping task that could not be read.");
                }
            }

            // Recurse into subfolders
            try
            {
                dynamic subfolders = folder.GetFolders(1);
                foreach (dynamic subfolder in subfolders)
                {
                    try
                    {
                        CollectTasksFromFolder(subfolder, results);
                    }
                    catch { }
                }
            }
            catch { }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to enumerate tasks in folder.");
        }
    }
}
