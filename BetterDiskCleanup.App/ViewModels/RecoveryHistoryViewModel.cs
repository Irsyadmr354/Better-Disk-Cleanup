using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using BetterDiskCleanup.Core.Recovery;
using Microsoft.Extensions.Logging;

namespace BetterDiskCleanup.App.ViewModels;

public sealed class RecoveryHistoryViewModel : ViewModelBase
{
    private readonly IRecoveryService _recoveryService;
    private readonly IRecoveryCleanupService _recoveryCleanupService;
    private readonly ILogger<RecoveryHistoryViewModel> _logger;
    private string _statusMessage = "Load recovery sessions to begin.";

    public RecoveryHistoryViewModel(
        IRecoveryService recoveryService,
        IRecoveryCleanupService recoveryCleanupService,
        ILogger<RecoveryHistoryViewModel> logger)
    {
        _recoveryService = recoveryService;
        _recoveryCleanupService = recoveryCleanupService;
        _logger = logger;

        Sessions = [];
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        PurgeExpiredCommand = new AsyncRelayCommand(PurgeExpiredAsync);
    }

    public ObservableCollection<RecoverySessionViewModel> Sessions { get; }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand PurgeExpiredCommand { get; }

    public async Task RefreshAsync()
    {
        StatusMessage = "Loading recovery sessions...";

        try
        {
            Sessions.Clear();
            foreach (var summary in _recoveryService.ListSessions())
            {
                var sessionViewModel = new RecoverySessionViewModel(summary, _recoveryService, _logger);
                Sessions.Add(sessionViewModel);
                await sessionViewModel.LoadItemsAsync();
            }

            StatusMessage = $"Loaded {Sessions.Count} recovery session(s).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load recovery sessions: {ex.Message}";
            _logger.LogError(ex, "Failed to refresh recovery history.");
        }
    }

    private async Task PurgeExpiredAsync()
    {
        StatusMessage = "Purging expired recovery sessions...";

        try
        {
            var result = await _recoveryCleanupService.PurgeExpiredSessionsAsync();
            StatusMessage = $"Purged {result.PurgedSessionIds.Count} expired session(s).";
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to purge expired sessions: {ex.Message}";
            _logger.LogError(ex, "Failed to purge expired recovery sessions.");
        }
    }
}

public sealed class RecoverySessionViewModel : ViewModelBase
{
    private readonly IRecoveryService _recoveryService;
    private readonly ILogger _logger;
    private bool _isExpanded;

    public RecoverySessionViewModel(
        RecoverySessionSummary summary,
        IRecoveryService recoveryService,
        ILogger logger)
    {
        _recoveryService = recoveryService;
        _logger = logger;
        SessionId = summary.SessionId;
        CreatedAtUtc = summary.CreatedAtUtc;
        ExpiresAtUtc = summary.ExpiresAtUtc;
        // Compute effective status: manifest may still say "Active" past expiry
        Status = summary.Status == RecoverySessionStatus.Active && summary.ExpiresAtUtc <= DateTimeOffset.UtcNow
            ? RecoverySessionStatus.Expired
            : summary.Status;
        FileCount = summary.FileCount;
        TotalSizeBytes = summary.TotalSizeBytes;
        Items = [];

        RestoreSessionCommand = new AsyncRelayCommand(RestoreSessionAsync, () => Status == RecoverySessionStatus.Active);
        PurgeSessionCommand = new AsyncRelayCommand(PurgeSessionAsync, () => Status == RecoverySessionStatus.Active);
        ToggleExpandedCommand = new RelayCommand(ToggleExpanded);
    }

    public string SessionId { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public DateTimeOffset ExpiresAtUtc { get; }
    public RecoverySessionStatus Status { get; private set; }
    public int FileCount { get; private set; }
    public long TotalSizeBytes { get; private set; }

    public string SummaryText =>
        $"{CreatedAtUtc.ToLocalTime():g} | {FileCount} files | {FormatBytes(TotalSizeBytes)} | {Status}";

    public bool IsExpanded
    {
        get => _isExpanded;
        private set => SetProperty(ref _isExpanded, value);
    }

    public ObservableCollection<RecoveryItemViewModel> Items { get; }

    public ICommand RestoreSessionCommand { get; }
    public ICommand PurgeSessionCommand { get; }
    public ICommand ToggleExpandedCommand { get; }

    public Task LoadItemsAsync()
    {
        Items.Clear();
        var manifest = _recoveryService.GetSessionManifest(SessionId);
        if (manifest is null)
        {
            return Task.CompletedTask;
        }

        foreach (var item in manifest.Items.Where(item => item.Status == RecoveryItemStatus.Staged))
        {
            Items.Add(new RecoveryItemViewModel(SessionId, item, _recoveryService, _logger, RefreshAsync));
        }

        return Task.CompletedTask;
    }

    private void ToggleExpanded()
    {
        IsExpanded = !IsExpanded;
    }

    private async Task RestoreSessionAsync()
    {
        var confirmation = System.Windows.MessageBox.Show(
            "Restore all staged files in this session?",
            "Confirm Restore",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (confirmation != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        var result = await _recoveryService.RestoreSessionAsync(SessionId);
        var restoredCount = result.Items.Count(item => item.Restored);
        var skippedCount = result.Items.Count(item => item.Skipped);

        System.Windows.MessageBox.Show(
            $"Restore completed. Restored: {restoredCount}, Skipped: {skippedCount}",
            "Restore Result",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);

        await RefreshAsync();
    }

    private async Task PurgeSessionAsync()
    {
        var confirmation = System.Windows.MessageBox.Show(
            "Permanently delete all staged files in this session? This cannot be undone.",
            "Confirm Purge",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (confirmation != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        await _recoveryService.PurgeSessionAsync(SessionId);
        Status = RecoverySessionStatus.Purged;
        FileCount = 0;
        TotalSizeBytes = 0;
        OnPropertyChanged(nameof(SummaryText));
        Items.Clear();
        ((AsyncRelayCommand)RestoreSessionCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)PurgeSessionCommand).RaiseCanExecuteChanged();
    }

    private async Task RefreshAsync()
    {
        var summary = _recoveryService.ListSessions().FirstOrDefault(session => session.SessionId == SessionId);
        if (summary is not null)
        {
            // Compute effective status considering expiration
            Status = summary.Status == RecoverySessionStatus.Active && summary.ExpiresAtUtc <= DateTimeOffset.UtcNow
                ? RecoverySessionStatus.Expired
                : summary.Status;
            FileCount = summary.FileCount;
            TotalSizeBytes = summary.TotalSizeBytes;
            OnPropertyChanged(nameof(SummaryText));
        }

        await LoadItemsAsync();
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        var unitIndex = 0;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:0.##} {units[unitIndex]}";
    }
}

public sealed class RecoveryItemViewModel : ViewModelBase
{
    private readonly string _sessionId;
    private readonly IRecoveryService _recoveryService;
    private readonly ILogger _logger;
    private readonly Func<Task> _refreshSessionAsync;

    public RecoveryItemViewModel(
        string sessionId,
        RecoveryManifestItem item,
        IRecoveryService recoveryService,
        ILogger logger,
        Func<Task> refreshSessionAsync)
    {
        _sessionId = sessionId;
        _recoveryService = recoveryService;
        _logger = logger;
        _refreshSessionAsync = refreshSessionAsync;

        ItemId = item.ItemId;
        OriginalPath = item.OriginalPath;
        SizeBytes = item.SizeBytes;
        RestoreItemCommand = new AsyncRelayCommand(RestoreItemAsync);
    }

    public string ItemId { get; }
    public string OriginalPath { get; }
    public long SizeBytes { get; }

    public string DisplayText => $"{Path.GetFileName(OriginalPath)} ({SizeBytes} bytes)";

    public ICommand RestoreItemCommand { get; }

    private async Task RestoreItemAsync()
    {
        var result = await _recoveryService.RestoreItemAsync(_sessionId, ItemId);
        System.Windows.MessageBox.Show(
            result.Message ?? (result.Restored ? "Item restored." : "Item skipped."),
            "Restore Item",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);

        await _refreshSessionAsync();
    }
}
