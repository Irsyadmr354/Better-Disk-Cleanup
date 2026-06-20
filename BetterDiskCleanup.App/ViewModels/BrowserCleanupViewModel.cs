using System.Collections.ObjectModel;
using System.Windows.Input;
using BetterDiskCleanup.Core.Analysis;
using BetterDiskCleanup.Core.Browsers;
using BetterDiskCleanup.Core.Cleanup;
using BetterDiskCleanup.Core.Safety;
using Microsoft.Extensions.Logging;

namespace BetterDiskCleanup.App.ViewModels;

public sealed class BrowserCleanupViewModel : ViewModelBase
{
    private readonly IBrowserDetector _browserDetector;
    private readonly IBrowserDataScanner _browserDataScanner;
    private readonly IBrowserProcessChecker _browserProcessChecker;
    private readonly ICleanupSimulator _simulator;
    private readonly ICleanupExecutor _executor;
    private readonly ILogger<BrowserCleanupViewModel> _logger;

    private string _statusMessage = "Ready. Click Detect Browsers to start.";
    private string _scanSummary = string.Empty;
    private string _previewSummary = string.Empty;
    private string _reportSummary = string.Empty;
    private string _progressText = string.Empty;
    private string _browserWarning = string.Empty;
    private bool _isScanning;
    private BrowserDataScanResult? _scanResult;
    private CleanupSimulationResult? _simulationResult;
    private CleanupReport? _cleanupReport;

    public BrowserCleanupViewModel(
        IBrowserDetector browserDetector,
        IBrowserDataScanner browserDataScanner,
        IBrowserProcessChecker browserProcessChecker,
        ICleanupSimulator simulator,
        ICleanupExecutor executor,
        ILogger<BrowserCleanupViewModel> logger)
    {
        _browserDetector = browserDetector;
        _browserDataScanner = browserDataScanner;
        _browserProcessChecker = browserProcessChecker;
        _simulator = simulator;
        _executor = executor;
        _logger = logger;

        BrowserGroups = [];
        DetectCommand = new AsyncRelayCommand(DetectAndScanAsync, () => !IsScanning);
        CancelScanCommand = new RelayCommand(CancelScan, () => IsScanning);
        PreviewCommand = new AsyncRelayCommand(PreviewAsync, () => _scanResult is not null && !IsScanning && HasSelectedItems);
        CleanCommand = new AsyncRelayCommand(CleanAsync, () => _scanResult is not null && !IsScanning && HasSelectedItems);
    }

    public ObservableCollection<BrowserGroupViewModel> BrowserGroups { get; }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string ScanSummary
    {
        get => _scanSummary;
        private set => SetProperty(ref _scanSummary, value);
    }

    public string PreviewSummary
    {
        get => _previewSummary;
        private set => SetProperty(ref _previewSummary, value);
    }

    public string ReportSummary
    {
        get => _reportSummary;
        private set => SetProperty(ref _reportSummary, value);
    }

    public string ProgressText
    {
        get => _progressText;
        private set => SetProperty(ref _progressText, value);
    }

    public string BrowserWarning
    {
        get => _browserWarning;
        private set
        {
            if (SetProperty(ref _browserWarning, value))
            {
                OnPropertyChanged(nameof(HasBrowserWarning));
            }
        }
    }

    public bool HasBrowserWarning => !string.IsNullOrEmpty(BrowserWarning);

    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            if (SetProperty(ref _isScanning, value))
            {
                ((AsyncRelayCommand)DetectCommand).RaiseCanExecuteChanged();
                ((AsyncRelayCommand)PreviewCommand).RaiseCanExecuteChanged();
                ((AsyncRelayCommand)CleanCommand).RaiseCanExecuteChanged();
                ((RelayCommand)CancelScanCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public ICommand DetectCommand { get; }
    public ICommand CancelScanCommand { get; }
    public ICommand PreviewCommand { get; }
    public ICommand CleanCommand { get; }

    private bool HasSelectedItems =>
        BrowserGroups.Any(g => g.Profiles.Any(p => p.DataEntries.Any(d => d.IsSelected)));

    private System.Threading.CancellationTokenSource? _scanCancellation;

    private async Task DetectAndScanAsync()
    {
        _scanCancellation = new System.Threading.CancellationTokenSource();
        IsScanning = true;
        StatusMessage = "Detecting installed browsers...";
        ProgressText = string.Empty;
        BrowserWarning = string.Empty;
        _simulationResult = null;
        _cleanupReport = null;
        PreviewSummary = string.Empty;
        ReportSummary = string.Empty;

        try
        {
            var profiles = _browserDetector.DetectInstalledBrowsers();

            if (profiles.Count == 0)
            {
                StatusMessage = "No browsers detected.";
                BrowserGroups.Clear();
                return;
            }

            // Check for running browsers
            var runningProcesses = _browserProcessChecker.GetRunningBrowserProcesses(profiles);
            if (runningProcesses.Count > 0)
            {
                var browserNames = string.Join(", ", runningProcesses);
                BrowserWarning = $"WARNING: The following browser(s) are currently running: {browserNames}. " +
                                 "Please close them before cleaning to avoid data corruption.";
            }

            StatusMessage = $"Scanning {profiles.Count} browser profile(s)...";

            var progress = new Progress<ScanProgress>(report =>
            {
                ProgressText = $"Scanned {report.FilesScanned} files ({FormatBytes(report.BytesScanned)})";
            });

            _scanResult = await _browserDataScanner.ScanAsync(
                profiles, progress, _scanCancellation.Token);

            PopulateBrowserGroups(_scanResult);

            ScanSummary = $"Entries: {_scanResult.Entries.Count}, Total: {FormatBytes(_scanResult.TotalSizeBytes)}";

            if (_scanResult.Warnings.Count > 0)
            {
                ScanSummary += $", Warnings: {_scanResult.Warnings.Count}";
            }

            StatusMessage = "Scan completed.";
            ProgressText = string.Empty;
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Scan cancelled.";
            ProgressText = string.Empty;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Scan failed: {ex.Message}";
            _logger.LogError(ex, "Browser scan failed.");
        }
        finally
        {
            IsScanning = false;
            _scanCancellation?.Dispose();
            _scanCancellation = null;
        }
    }

    private void CancelScan()
    {
        _scanCancellation?.Cancel();
        StatusMessage = "Cancelling scan...";
    }

    private void PopulateBrowserGroups(BrowserDataScanResult scanResult)
    {
        BrowserGroups.Clear();

        foreach (var profile in scanResult.Profiles)
        {
            var profileEntries = scanResult.Entries
                .Where(e => e.BrowserName == profile.BrowserName && e.ProfileName == profile.ProfileName)
                .ToList();

            if (profileEntries.Count == 0)
            {
                continue;
            }

            var dataEntryViewModels = new ObservableCollection<BrowserDataEntryViewModel>(
                profileEntries.Select(entry => new BrowserDataEntryViewModel(entry)
                {
                    // Auto-select only Safe/Recommended items; never auto-select Cookies/History
                    IsSelected = entry.DataType is BrowserDataType.Cache
                        or BrowserDataType.Temporary
                        or BrowserDataType.ServiceWorker
                        or BrowserDataType.Sessions
                }));

            var profileVm = new BrowserProfileViewModel(
                profile.BrowserName,
                profile.ProfileName,
                dataEntryViewModels);

            // Find existing group or create new
            var existingGroup = BrowserGroups.FirstOrDefault(
                g => g.BrowserName == profile.BrowserName);

            if (existingGroup is not null)
            {
                existingGroup.Profiles.Add(profileVm);
            }
            else
            {
                var group = new BrowserGroupViewModel(profile.BrowserName, [profileVm]);
                BrowserGroups.Add(group);
            }
        }
    }

    private async Task PreviewAsync()
    {
        if (_scanResult is null)
        {
            return;
        }

        StatusMessage = "Running preview...";

        try
        {
            var selectedScanResult = GetSelectedScanResult();
            _simulationResult = await _simulator.SimulateAsync(selectedScanResult);

            PreviewSummary =
                $"Recoverable files: {_simulationResult.FileCount}, " +
                $"Recoverable space: {FormatBytes(_simulationResult.RecoverableBytes)}";

            StatusMessage = "Preview completed.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Preview failed: {ex.Message}";
            _logger.LogError(ex, "Browser preview failed.");
        }
    }

    private async Task CleanAsync()
    {
        if (_scanResult is null)
        {
            return;
        }

        var confirmation = System.Windows.MessageBox.Show(
            "This will permanently delete the selected browser data. " +
            "Files will be backed up in Recovery before deletion. Continue?",
            "Confirm Browser Cleanup",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (confirmation != System.Windows.MessageBoxResult.Yes)
        {
            StatusMessage = "Cleanup cancelled by user.";
            return;
        }

        StatusMessage = "Cleaning browser data...";

        try
        {
            var selectedScanResult = GetSelectedScanResult();
            _cleanupReport = await _executor.ExecuteAsync(selectedScanResult);

            ReportSummary =
                $"Deleted: {_cleanupReport.FilesDeleted} files, " +
                $"Recovered: {FormatBytes(_cleanupReport.SpaceRecoveredBytes)}, " +
                $"Skipped (in use): {_cleanupReport.SkippedInUse.Count}, " +
                $"Errors: {_cleanupReport.Errors.Count}." +
                (_cleanupReport.RecoverySessionId is null
                    ? string.Empty
                    : $" Recovery session: {_cleanupReport.RecoverySessionId}.");

            StatusMessage = "Cleanup completed.";
            _scanResult = null;
            _simulationResult = null;
            BrowserGroups.Clear();
            ScanSummary = "Scan results cleared. Run scan again to refresh.";
            PreviewSummary = string.Empty;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Cleanup failed: {ex.Message}";
            _logger.LogError(ex, "Browser cleanup failed.");
        }
    }

    private ScanResult GetSelectedScanResult()
    {
        var selectedItems = BrowserGroups
            .SelectMany(g => g.Profiles)
            .SelectMany(p => p.DataEntries)
            .Where(d => d.IsSelected)
            .SelectMany(d => d.Entry.Files)
            .ToList();

        return new ScanResult
        {
            FileCount = selectedItems.Count,
            FolderCount = 0,
            TotalSizeBytes = selectedItems.Sum(item => item.SizeBytes),
            Items = selectedItems,
            Warnings = []
        };
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

public sealed class BrowserGroupViewModel : ViewModelBase
{
    public BrowserGroupViewModel(string browserName, ObservableCollection<BrowserProfileViewModel> profiles)
    {
        BrowserName = browserName;
        Profiles = profiles;
    }

    public string BrowserName { get; }
    public ObservableCollection<BrowserProfileViewModel> Profiles { get; }
    public bool IsExpanded { get; set; } = true;
}

public sealed class BrowserProfileViewModel : ViewModelBase
{
    public BrowserProfileViewModel(
        string browserName,
        string profileName,
        ObservableCollection<BrowserDataEntryViewModel> dataEntries)
    {
        BrowserName = browserName;
        ProfileName = profileName;
        DataEntries = dataEntries;
    }

    public string BrowserName { get; }
    public string ProfileName { get; }
    public ObservableCollection<BrowserDataEntryViewModel> DataEntries { get; }
}

public sealed class BrowserDataEntryViewModel : ViewModelBase
{
    private bool _isSelected;

    public BrowserDataEntryViewModel(BrowserScanEntry entry)
    {
        Entry = entry;
    }

    public BrowserScanEntry Entry { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string DisplayText => $"{Entry.DisplayName} ({FormatBytes(Entry.SizeBytes)})";

    public bool IsSensitiveData =>
        Entry.DataType is BrowserDataType.Cookies or BrowserDataType.History;

    public string RiskLabel => IsSensitiveData ? "⚠ Sensitive" : "Safe";

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

