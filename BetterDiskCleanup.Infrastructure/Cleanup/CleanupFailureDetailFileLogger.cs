using System.Text;
using BetterDiskCleanup.Core.Cleanup;

namespace BetterDiskCleanup.Infrastructure.Cleanup;

public sealed class CleanupFailureDetailFileLogger : ICleanupFailureDetailLogger, IDisposable
{
    private readonly object _writeLock = new();
    private readonly string _logDirectory;
    private StreamWriter? _writer;

    public CleanupFailureDetailFileLogger()
    {
        _logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BetterDiskCleanup",
            "logs");

        Directory.CreateDirectory(_logDirectory);
        LogFilePath = string.Empty;
    }

    public string LogFilePath { get; private set; }

    public void LogSessionStart(int itemCount)
    {
        lock (_writeLock)
        {
            _writer?.Dispose();

            LogFilePath = Path.Combine(
                _logDirectory,
                $"cleanup-detail-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.log");

            _writer = new StreamWriter(LogFilePath, append: false, Encoding.UTF8)
            {
                AutoFlush = true
            };

            _writer.WriteLine(
                $"SESSION_START | Items={itemCount} | LogFile={LogFilePath} | Utc={DateTimeOffset.UtcNow:O}");
        }
    }

    public void LogFailure(CleanupFailureDetail detail)
    {
        var builder = new StringBuilder();
        builder.Append($"FAILURE | Stage={detail.Stage}");
        builder.Append($" | Path={detail.Path}");

        if (!string.IsNullOrWhiteSpace(detail.ExceptionType))
        {
            builder.Append($" | ExceptionType={detail.ExceptionType}");
        }

        if (!string.IsNullOrWhiteSpace(detail.ExceptionMessage))
        {
            builder.Append($" | Message={detail.ExceptionMessage}");
        }

        if (detail.HResult is not null)
        {
            builder.Append($" | HResult=0x{detail.HResult.Value:X8}");
        }

        if (!string.IsNullOrWhiteSpace(detail.AdditionalContext))
        {
            builder.Append($" | Context={detail.AdditionalContext}");
        }

        WriteLine(builder.ToString());
    }

    public void LogSessionEnd(CleanupReport report)
    {
        WriteLine(
            $"SESSION_END | Deleted={report.FilesDeleted} | RecoveredBytes={report.SpaceRecoveredBytes} | " +
            $"Warnings={report.Warnings.Count} | SkippedInUse={report.SkippedInUse.Count} | Errors={report.Errors.Count} | " +
            $"Started={report.StartedAt:O} | Completed={report.CompletedAt:O}");
    }

    public void Dispose()
    {
        lock (_writeLock)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }

    private void WriteLine(string line)
    {
        lock (_writeLock)
        {
            _writer?.WriteLine(line);
        }
    }
}
