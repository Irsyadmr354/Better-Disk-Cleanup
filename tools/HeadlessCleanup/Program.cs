using BetterDiskCleanup.Core.Analysis;
using BetterDiskCleanup.Core.Browsers;
using BetterDiskCleanup.Core.Cleanup;
using BetterDiskCleanup.Core.Recovery;
using BetterDiskCleanup.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var services = new ServiceCollection();
services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
services.AddBetterDiskCleanupInfrastructure();

var provider = services.BuildServiceProvider();
var tempScanner = provider.GetRequiredService<ITempFileScanner>();
var browserScanner = provider.GetRequiredService<IBrowserDataScanner>();
var simulator = provider.GetRequiredService<ICleanupSimulator>();
var executor = provider.GetRequiredService<ICleanupExecutor>();
var recoveryService = provider.GetRequiredService<IRecoveryService>();

string command = args.Length > 0 ? args[0].ToLowerInvariant() : "scan";
string target = args.Length > 1 ? args[1].ToLowerInvariant() : "temp";

if (args.Contains("--help") || args.Contains("-h"))
{
    PrintHelp();
    return;
}

try
{
    if (command == "scan" && target == "temp")
    {
        await ScanTempAsync(tempScanner, browserScanner);
    }
    else if (command == "clean" && target == "temp")
    {
        bool confirm = args.Contains("--confirm");
        await CleanTempAsync(tempScanner, browserScanner, simulator, executor, confirm);
    }
    else if (command == "recovery" && target == "list")
    {
        ListRecovery(recoveryService);
    }
    else if (command == "recovery" && target == "restore")
    {
        if (args.Length < 3)
        {
            Console.WriteLine("Error: Please provide a session ID.");
            Console.WriteLine("Usage: recovery restore <sessionId>");
            return;
        }
        string sessionId = args[2];
        await RestoreRecoveryAsync(recoveryService, sessionId);
    }
    else
    {
        Console.WriteLine($"Unknown command or target: {command} {target}");
        PrintHelp();
    }
}
catch (Exception ex)
{
    Console.WriteLine($"An error occurred: {ex.Message}");
}

static void PrintHelp()
{
    Console.WriteLine("BetterDiskCleanup Headless CLI");
    Console.WriteLine("Usage:");
    Console.WriteLine("  scan temp                  Scan Temp files & Browser data");
    Console.WriteLine("  clean temp                 Simulate cleanup of Temp & Browser data");
    Console.WriteLine("  clean temp --confirm       Execute real cleanup of Temp & Browser data");
    Console.WriteLine("  recovery list              List available recovery sessions");
    Console.WriteLine("  recovery restore <id>      Restore a specific recovery session");
    Console.WriteLine("  --help, -h                 Show this help message");
}

static async Task<ScanResult> ScanTempAsync(ITempFileScanner tempScanner, IBrowserDataScanner browserScanner)
{
    Console.WriteLine("Scanning temporary files and browser data...");
    var tempResult = await tempScanner.ScanAsync();
    var browserResult = await browserScanner.ScanAsync();

    var combinedItems = tempResult.Items.Concat(browserResult.Items).ToList();
    var combinedResult = new ScanResult
    {
        FileCount = tempResult.FileCount + browserResult.FileCount,
        FolderCount = tempResult.FolderCount + browserResult.FolderCount,
        TotalSizeBytes = tempResult.TotalSizeBytes + browserResult.TotalSizeBytes,
        Items = combinedItems,
        Warnings = tempResult.Warnings.Concat(browserResult.Warnings).ToList()
    };

    Console.WriteLine($"Scan complete: {combinedResult.FileCount} files, {FormatBytes(combinedResult.TotalSizeBytes)}.");
    return combinedResult;
}

static async Task CleanTempAsync(ITempFileScanner tempScanner, IBrowserDataScanner browserScanner, ICleanupSimulator simulator, ICleanupExecutor executor, bool confirm)
{
    var scanResult = await ScanTempAsync(tempScanner, browserScanner);
    if (scanResult.FileCount == 0)
    {
        Console.WriteLine("Nothing to clean.");
        return;
    }

    if (!confirm)
    {
        Console.WriteLine("\nRunning SIMULATION only (use --confirm to execute real cleanup).");
        var report = await simulator.SimulateAsync(scanResult);
        Console.WriteLine($"Simulation complete. Simulated Deletions: {report.FilesDeleted}, Recoverable space: {FormatBytes(report.SpaceRecoveredBytes)}");
    }
    else
    {
        Console.WriteLine("\nExecuting REAL cleanup...");
        var report = await executor.ExecuteAsync(scanResult);
        Console.WriteLine($"Cleanup complete. Deleted: {report.FilesDeleted}, Errors: {report.Errors.Count}, In Use: {report.SkippedInUse.Count}. Space recovered: {FormatBytes(report.SpaceRecoveredBytes)}");
        if (report.RecoverySessionId != null)
        {
            Console.WriteLine($"Recovery session created: {report.RecoverySessionId}");
        }
    }
}

static void ListRecovery(IRecoveryService recoveryService)
{
    var sessions = recoveryService.ListSessions();
    Console.WriteLine($"Found {sessions.Count} recovery sessions:");
    foreach (var session in sessions.OrderByDescending(s => s.CreatedAtUtc))
    {
        Console.WriteLine($"- ID: {session.SessionId}");
        Console.WriteLine($"  Created: {session.CreatedAtUtc.ToLocalTime()}");
        Console.WriteLine($"  Size: {FormatBytes(session.TotalSizeBytes)}");
        Console.WriteLine($"  File Count: {session.FileCount}");
    }
}

static async Task RestoreRecoveryAsync(IRecoveryService recoveryService, string sessionId)
{
    Console.WriteLine($"Restoring session: {sessionId}...");
    var report = await recoveryService.RestoreSessionAsync(sessionId);
    var restored = report.Items.Count(i => i.Restored);
    var skipped = report.Items.Count(i => i.Skipped);
    var failed = report.Items.Count(i => !i.Restored && !i.Skipped);
    Console.WriteLine($"Restore complete. Restored: {restored}, Skipped: {skipped}, Failed: {failed}.");
    foreach (var item in report.Items.Where(i => !i.Restored && !i.Skipped))
    {
        Console.WriteLine($"  FAILED: {item.OriginalPath} — {item.Message}");
    }
}

static string FormatBytes(long bytes)
{
    string[] units = { "B", "KB", "MB", "GB", "TB" };
    double size = bytes;
    int unitIndex = 0;
    while (size >= 1024 && unitIndex < units.Length - 1)
    {
        size /= 1024;
        unitIndex++;
    }
    return $"{size:0.##} {units[unitIndex]}";
}
