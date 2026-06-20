using System.Collections.ObjectModel;
using Serilog.Core;
using Serilog.Events;

namespace BetterDiskCleanup.App.ViewModels;

/// <summary>
/// Singleton store for live log entries displayed in the UI.
/// Acts as both a Serilog sink (receives log events) and a ViewModel data source.
/// </summary>
public sealed class LogStore : ILogEventSink
{
    private const int MaxEntries = 200;

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

        // Must be on UI thread for ObservableCollection
        System.Windows.Application.Current?.Dispatcher?.BeginInvoke(() =>
        {
            Entries.Add(entry);
            // Trim oldest entries to keep memory bounded
            while (Entries.Count > MaxEntries)
            {
                Entries.RemoveAt(0);
            }
        });
    }

    public void Clear()
    {
        System.Windows.Application.Current?.Dispatcher?.BeginInvoke(() =>
        {
            Entries.Clear();
        });
    }
}
