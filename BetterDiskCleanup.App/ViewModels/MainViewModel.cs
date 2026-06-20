using System.Windows.Input;
using BetterDiskCleanup.Core.Analysis;
using BetterDiskCleanup.Core.Cleanup;
using Microsoft.Extensions.Logging;

namespace BetterDiskCleanup.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly ITempFileScanner _scanner;
    private readonly ICleanupSimulator _simulator;
    private readonly ICleanupExecutor _executor;
    private readonly ICleanupFailureDetailLogger _failureDetailLogger;
    private readonly ILogger<MainViewModel> _logger;

    private ScanResult? _scanResult;
    private CleanupSimulationResult? _simulationResult;
    private CleanupReport? _cleanupReport;
    private CancellationTokenSource? _scanCancellation;
    private string _statusMessage = "Ready.";
    private string _scanSummary = "No scan performed yet.";
    private string _previewSummary = "No preview performed yet.";
    private string _reportSummary = "No cleanup performed yet.";
    private string _progressText = string.Empty;
    private bool _isScanning;

    public MainViewModel(
        ITempFileScanner scanner,
        ICleanupSimulator simulator,
        ICleanupExecutor executor,
        ICleanupFailureDetailLogger failureDetailLogger,
        ILogger<MainViewModel> logger)
    {
        _scanner = scanner;
        _simulator = simulator;
        _executor = executor;
        _failureDetailLogger = failureDetailLogger;
        _logger = logger;

        ScanCommand = new AsyncRelayCommand(ScanAsync, () => !IsScanning);
        CancelScanCommand = new RelayCommand(CancelScan, () => IsScanning);
        PreviewCommand = new AsyncRelayCommand(PreviewAsync, () => _scanResult is not null && !IsScanning);
        CleanCommand = new AsyncRelayCommand(CleanAsync, () => _scanResult is not null && !IsScanning);
    }

    public string ApplicationTitle => "Better Disk Cleanup";

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

    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            if (SetProperty(ref _isScanning, value))
            {
                ((AsyncRelayCommand)ScanCommand).RaiseCanExecuteChanged();
                ((AsyncRelayCommand)PreviewCommand).RaiseCanExecuteChanged();
                ((AsyncRelayCommand)CleanCommand).RaiseCanExecuteChanged();
                ((RelayCommand)CancelScanCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public ICommand ScanCommand { get; }
    public ICommand CancelScanCommand { get; }
    public ICommand PreviewCommand { get; }
    public ICommand CleanCommand { get; }

    private async Task ScanAsync()
    {
        _scanCancellation = new CancellationTokenSource();
        IsScanning = true;
        StatusMessage = "Scanning temporary files...";
        ProgressText = "Starting scan...";
        _simulationResult = null;
        _cleanupReport = null;
        PreviewSummary = "No preview performed yet.";
        ReportSummary = "No cleanup performed yet.";

        var progress = new Progress<ScanProgress>(report =>
        {
            ProgressText =
                $"Scanned {report.FilesScanned} files in {report.FoldersScanned} folders ({FormatBytes(report.BytesScanned)})";
        });

        try
        {
            _scanResult = await _scanner.ScanAsync(progress, _scanCancellation.Token);

            ScanSummary =
                $"Files: {_scanResult.FileCount}, Folders: {_scanResult.FolderCount}, Total size: {FormatBytes(_scanResult.TotalSizeBytes)}";

            if (_scanResult.Warnings.Count > 0)
            {
                ScanSummary += $", Warnings: {_scanResult.Warnings.Count}";
            }

            StatusMessage = "Scan completed.";
            ProgressText = string.Empty;
            _logger.LogInformation(
                "UI scan completed with {FileCount} files and {TotalBytes} bytes",
                _scanResult.FileCount,
                _scanResult.TotalSizeBytes);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Scan cancelled.";
            ProgressText = string.Empty;
        }
        catch (Exception ex)
        {
            StatusMessage = "Scan failed.";
            ScanSummary = ex.Message;
            _logger.LogError(ex, "Scan failed in UI.");
        }
        finally
        {
            IsScanning = false;
            _scanCancellation?.Dispose();
            _scanCancellation = null;
            ((AsyncRelayCommand)PreviewCommand).RaiseCanExecuteChanged();
            ((AsyncRelayCommand)CleanCommand).RaiseCanExecuteChanged();
        }
    }

    private void CancelScan()
    {
        _scanCancellation?.Cancel();
        StatusMessage = "Cancelling scan...";
    }

    private async Task PreviewAsync()
    {
        if (_scanResult is null)
        {
            return;
        }

        StatusMessage = "Running cleanup preview...";

        try
        {
            _simulationResult = await _simulator.SimulateAsync(_scanResult);

            PreviewSummary =
                $"Recoverable files: {_simulationResult.FileCount}, Recoverable space: {FormatBytes(_simulationResult.RecoverableBytes)}";

            if (_simulationResult.SkippedPaths.Count > 0)
            {
                PreviewSummary += $", Skipped: {_simulationResult.SkippedPaths.Count}";
            }

            StatusMessage = "Preview completed.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Preview failed.";
            PreviewSummary = ex.Message;
            _logger.LogError(ex, "Preview failed in UI.");
        }
    }

    private async Task CleanAsync()
    {
        if (_scanResult is null)
        {
            return;
        }

        var confirmation = System.Windows.MessageBox.Show(
            "This will permanently delete scanned temporary files that pass safety validation. Continue?",
            "Confirm Cleanup",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (confirmation != System.Windows.MessageBoxResult.Yes)
        {
            StatusMessage = "Cleanup cancelled by user.";
            return;
        }

        StatusMessage = "Cleaning temporary files...";

        try
        {
            var scanSnapshot = SnapshotScanResult(_scanResult);
            _logger.LogInformation(
                "Cleanup dispatching snapshot with {ItemCount} items before UI invalidates scan state.",
                scanSnapshot.Items.Count);

            _cleanupReport = await _executor.ExecuteAsync(scanSnapshot);

            ReportSummary =
                $"Deleted: {_cleanupReport.FilesDeleted} files, Recovered: {FormatBytes(_cleanupReport.SpaceRecoveredBytes)}, " +
                $"Skipped (in use): {_cleanupReport.SkippedInUse.Count}, " +
                $"Errors: {_cleanupReport.Errors.Count}, Warnings: {_cleanupReport.Warnings.Count}. " +
                (_cleanupReport.RecoverySessionId is null
                    ? string.Empty
                    : $"Recovery session: {_cleanupReport.RecoverySessionId}. ") +
                $"Detail log: {_failureDetailLogger.LogFilePath}";

            StatusMessage = "Cleanup completed.";
            _logger.LogInformation(
                "UI cleanup completed. Deleted={FilesDeleted}, RecoveredBytes={RecoveredBytes}",
                _cleanupReport.FilesDeleted,
                _cleanupReport.SpaceRecoveredBytes);

            _scanResult = null;
            _simulationResult = null;
            ScanSummary = "Scan results cleared after cleanup. Run scan again to refresh.";
            PreviewSummary = "Preview invalidated after cleanup.";
            ((AsyncRelayCommand)PreviewCommand).RaiseCanExecuteChanged();
            ((AsyncRelayCommand)CleanCommand).RaiseCanExecuteChanged();
        }
        catch (Exception ex)
        {
            StatusMessage = "Cleanup failed.";
            ReportSummary = ex.Message;
            _logger.LogError(ex, "Cleanup failed in UI.");
        }
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

    private static ScanResult SnapshotScanResult(ScanResult scanResult) =>
        new()
        {
            FileCount = scanResult.FileCount,
            FolderCount = scanResult.FolderCount,
            TotalSizeBytes = scanResult.TotalSizeBytes,
            Items = scanResult.Items.ToList(),
            Warnings = scanResult.Warnings.ToList()
        };
}
