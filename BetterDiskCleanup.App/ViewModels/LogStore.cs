using System.Collections.ObjectModel;
using System.Windows.Threading;
using Serilog.Core;
using Serilog.Events;

namespace BetterDiskCleanup.App.ViewModels;

/// <summary>
/// Singleton store for live log entries displayed in the UI.
/// Acts as both a Serilog sink (receives log events) and a ViewModel data source.
/// Buffers entries from background threads and flushes in a single dispatcher call
/// to avoid desynchronizing the WPF VirtualizingStackPanel.
/// </summary>
public sealed class LogStore : ILogEventSink
{
    private const int MaxEntries = 200;
    private readonly object _lock = new();
    private readonly List<string> _buffer = [];
    private DispatcherOperation? _pendingFlush;

    public ObservableCollection<string> Entries { get; } = [];

    public void Emit(LogEvent logEvent)
    {
        var message = logEvent.RenderMessage();
        var timestamp = logEvent.Timestamp.ToString("HH:mm:ss");
        var level = logEvent.Level switch
        {
            LogEventLevel.Debug => "DBG",
            LogEventLevel.Information => "INF",
            LogEventLevel.Warning => "WRN",
            LogEventLevel.Error => "ERR",
            LogEventLevel.Fatal => "FTL",
            _ => "???"
        };

        var entry = $"[{timestamp}] [{level}] {message}";

        lock (_lock)
        {
            _buffer.Add(entry);

            // Only schedule one dispatcher call — it will flush all buffered entries
            if (_pendingFlush is null)
            {
                _pendingFlush = System.Windows.Application.Current?.Dispatcher?.BeginInvoke(FlushBuffer);
            }
        }
    }

    public void Clear()
    {
        System.Windows.Application.Current?.Dispatcher?.BeginInvoke(() =>
        {
            lock (_lock)
            {
                _buffer.Clear();
            }
            Entries.Clear();
        });
    }

    private void FlushBuffer()
    {
        List<string> batch;
        lock (_lock)
        {
            batch = [.. _buffer];
            _buffer.Clear();
            _pendingFlush = null;
        }

        foreach (var entry in batch)
        {
            Entries.Add(entry);
        }

        // Trim oldest entries to keep memory bounded
        while (Entries.Count > MaxEntries)
        {
            Entries.RemoveAt(0);
        }
    }
}
