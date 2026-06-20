using BetterDiskCleanup.Core.Analysis;
using BetterDiskCleanup.Core.Cleanup;
using BetterDiskCleanup.Infrastructure;
using BetterDiskCleanup.Infrastructure.Cleanup;
using BetterDiskCleanup.Infrastructure.Scanning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var services = new ServiceCollection();
services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
services.AddBetterDiskCleanupInfrastructure();

var provider = services.BuildServiceProvider();
var scanner = provider.GetRequiredService<ITempFileScanner>();
var executor = provider.GetRequiredService<ICleanupExecutor>();
var detailLogger = provider.GetRequiredService<ICleanupFailureDetailLogger>();

Console.WriteLine("Scanning temp files...");
var scanResult = await scanner.ScanAsync();
Console.WriteLine($"Scan complete: {scanResult.FileCount} files, {scanResult.TotalSizeBytes} bytes");

var snapshot = new ScanResult
{
    FileCount = scanResult.FileCount,
    FolderCount = scanResult.FolderCount,
    TotalSizeBytes = scanResult.TotalSizeBytes,
    Items = scanResult.Items.ToList(),
    Warnings = scanResult.Warnings.ToList()
};

Console.WriteLine($"Executing cleanup on snapshot of {snapshot.Items.Count} items...");
var report = await executor.ExecuteAsync(snapshot);

Console.WriteLine(
    $"Cleanup done. Deleted={report.FilesDeleted}, Errors={report.Errors.Count}, Warnings={report.Warnings.Count}");
Console.WriteLine($"Detail log: {detailLogger.LogFilePath}");
