using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using BetterDiskCleanup.Core.Analysis;
using BetterDiskCleanup.Core.Cleanup;
using BetterDiskCleanup.Core.Filesystem;
using BetterDiskCleanup.Core.LargeFiles;
using BetterDiskCleanup.Core.Safety;
using Microsoft.Extensions.Logging;

namespace BetterDiskCleanup.App.ViewModels;

public sealed class LargeFileFinderViewModel : ViewModelBase
{
    private readonly ILargeFileScanner _scanner;
    private readonly IFileSystemGateway _fileSystem;
    private readonly ICleanupSimulator _simulator;
    private readonly ICleanupExecutor _executor;
    private readonly IPathSafetyValidator _safetyValidator;
    private readonly IFileLockInspector _fileLockInspector;
    private readonly ILogger<LargeFileFinderViewModel> _logger;

    private string _statusMessage = "Ready. Select a drive and click Scan.";
    private string _progressText = string.Empty;
    private string _summaryText = string.Empty;
    private string _reportSummary = string.Empty;
    private string _searchText = string.Empty;
    private bool _isScanning;
    private string? _selectedDrive;
    private ThresholdOption _selectedThreshold;
    private FileCategory? _selectedFileTypeFilter;
    private string _sortColumn = "Size";
    private bool _sortAscending = false;

    private LargeFileScanResult? _scanResult;
    private CancellationTokenSource? _scanCancellation;

    public LargeFileFinderViewModel(
        ILargeFileScanner scanner,
        IFileSystemGateway fileSystem,
        ICleanupSimulator simulator,
        ICleanupExecutor executor,
        IPathSafetyValidator safetyValidator,
        IFileLockInspector fileLockInspector,
        ILogger<LargeFileFinderViewModel> logger)
    {
        _scanner = scanner;
        _fileSystem = fileSystem;
        _simulator = simulator;
        _executor = executor;
        _safetyValidator = safetyValidator;
        _fileLockInspector = fileLockInspector;
        _logger = logger;

        _selectedThreshold = ThresholdOptions[0]; // default 100 MB

        AvailableDrives = [];
        Results = [];

        ScanCommand = new AsyncRelayCommand(ScanAsync, () => !IsScanning && !string.IsNullOrEmpty(SelectedDrive));
        CancelScanCommand = new RelayCommand(CancelScan, () => IsScanning);
        OpenFileCommand = new RelayCommand<LargeFileItemViewModel>(OpenFile);
        OpenFolderCommand = new RelayCommand<LargeFileItemViewModel>(OpenFolder);
        MoveFileCommand = new AsyncRelayCommand<LargeFileItemViewModel>(MoveFileAsync);
        DeleteCommand = new AsyncRelayCommand(DeleteSelectedAsync, () => !IsScanning && HasSelectedItems);
        CheckLockCommand = new RelayCommand<LargeFileItemViewModel>(CheckLock);
        ApplyFilterCommand = new RelayCommand(ApplyFilter);
        SortCommand = new ParameterizedRelayCommand(Sort);

        LoadDrives();
    }

    public ObservableCollection<string> AvailableDrives { get; }
    public ObservableCollection<LargeFileItemViewModel> Results { get; }

    public long LastRecoverableBytes => Results.Sum(r => r.Entry.SizeBytes);
    public int LastJunkFileCount => Results.Count;

    public static IReadOnlyList<ThresholdOption> ThresholdOptions { get; } =
    [
        new ThresholdOption("100 MB", 100L * 1024 * 1024),
        new ThresholdOption("500 MB", 500L * 1024 * 1024),
        new ThresholdOption("1 GB", 1024L * 1024 * 1024),
        new ThresholdOption("5 GB", 5L * 1024 * 1024 * 1024),
    ];

    public static IReadOnlyList<FileCategory?> FileTypeFilters { get; } =
    [
        null, // All
        FileCategory.Video,
        FileCategory.Archive,
        FileCategory.DiskImage,
        FileCategory.Document,
        FileCategory.Other,
    ];

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string ProgressText
    {
        get => _progressText;
        private set => SetProperty(ref _progressText, value);
    }

    public string SummaryText
    {
        get => _summaryText;
        private set => SetProperty(ref _summaryText, value);
    }

    public string ReportSummary
    {
        get => _reportSummary;
        private set => SetProperty(ref _reportSummary, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilter();
            }
        }
    }

    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            if (SetProperty(ref _isScanning, value))
            {
                ((AsyncRelayCommand)ScanCommand).RaiseCanExecuteChanged();
                ((RelayCommand)CancelScanCommand).RaiseCanExecuteChanged();
                ((AsyncRelayCommand)DeleteCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string? SelectedDrive
    {
        get => _selectedDrive;
        set
        {
            if (SetProperty(ref _selectedDrive, value))
            {
                ((AsyncRelayCommand)ScanCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public ThresholdOption SelectedThreshold
    {
        get => _selectedThreshold;
        set => SetProperty(ref _selectedThreshold, value);
    }

    public FileCategory? SelectedFileTypeFilter
    {
        get => _selectedFileTypeFilter;
        set
        {
            if (SetProperty(ref _selectedFileTypeFilter, value))
            {
                ApplyFilter();
            }
        }
    }

    public string SortColumn
    {
        get => _sortColumn;
        set => SetProperty(ref _sortColumn, value);
    }

    public bool SortAscending
    {
        get => _sortAscending;
        set => SetProperty(ref _sortAscending, value);
    }

    public bool HasSelectedItems => Results.Any(r => r.IsSelected);

    public ICommand ScanCommand { get; }
    public ICommand CancelScanCommand { get; }
    public ICommand OpenFileCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand MoveFileCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand CheckLockCommand { get; }
    public ICommand ApplyFilterCommand { get; }
    public ICommand SortCommand { get; }

    private void LoadDrives()
    {
        try
        {
            var drives = _scanner.GetAvailableDrives();
            AvailableDrives.Clear();
            foreach (var drive in drives)
            {
                AvailableDrives.Add(drive);
            }
            if (AvailableDrives.Count > 0)
            {
                SelectedDrive = AvailableDrives[0];
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load available drives.");
        }
    }

    private async Task ScanAsync()
    {
        if (string.IsNullOrEmpty(SelectedDrive))
        {
            return;
        }

        _scanCancellation = new CancellationTokenSource();
        IsScanning = true;
        StatusMessage = $"Scanning {SelectedDrive}...";
        ProgressText = string.Empty;
        ReportSummary = string.Empty;
        _scanResult = null;
        Results.Clear();

        try
        {
            var progress = new Progress<LargeFileScanProgress>(report =>
            {
                ProgressText = $"Scanned {report.DirectoriesScanned:N0} dirs | Found {report.FilesFound} file(s) ({FormatBytes(report.BytesScanned)})";
            });

            _scanResult = await _scanner.ScanAsync(
                SelectedDrive,
                SelectedThreshold.ThresholdBytes,
                progress,
                _scanCancellation.Token);

            PopulateResults(_scanResult.Entries);

            if (_scanResult.Entries.Count == 0)
            {
                SummaryText = $"No files over {SelectedThreshold.Label} found. Try a lower threshold.";
                StatusMessage = "Scan completed. No large files found — try lowering the threshold.";
            }
            else
            {
                SummaryText = $"Found {_scanResult.Entries.Count} file(s) over {SelectedThreshold.Label}, " +
                              $"total: {FormatBytes(_scanResult.TotalSizeBytes)}" +
                              (_scanResult.Warnings.Count > 0 ? $", {_scanResult.Warnings.Count} warning(s)" : string.Empty);
                StatusMessage = "Scan completed.";
            }

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
            _logger.LogError(ex, "Large file scan failed.");
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

    private void PopulateResults(IReadOnlyList<LargeFileEntry> entries)
    {
        Results.Clear();
        var filtered = ApplyFilterLogic(entries);
        var sorted = ApplySortLogic(filtered);

        foreach (var entry in sorted)
        {
            var item = new LargeFileItemViewModel(entry);
            item.PropertyChanged += OnResultItemPropertyChanged;
            Results.Add(item);
        }
    }

    private void OnResultItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LargeFileItemViewModel.IsSelected))
        {
            ((AsyncRelayCommand)DeleteCommand).RaiseCanExecuteChanged();
        }
    }

    private void ApplyFilter()
    {
        if (_scanResult is null)
        {
            return;
        }

        PopulateResults(_scanResult.Entries);
    }

    private IEnumerable<LargeFileEntry> ApplyFilterLogic(IReadOnlyList<LargeFileEntry> entries)
    {
        IEnumerable<LargeFileEntry> filtered = entries;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            filtered = filtered.Where(e =>
                e.FileName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                e.Path.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        if (SelectedFileTypeFilter.HasValue)
        {
            filtered = filtered.Where(e => e.Category == SelectedFileTypeFilter.Value);
        }

        return filtered;
    }

    private IEnumerable<LargeFileEntry> ApplySortLogic(IEnumerable<LargeFileEntry> entries)
    {
        return SortColumn switch
        {
            "Size" => SortAscending ? entries.OrderBy(e => e.SizeBytes) : entries.OrderByDescending(e => e.SizeBytes),
            "Name" => SortAscending ? entries.OrderBy(e => e.FileName, StringComparer.OrdinalIgnoreCase) : entries.OrderByDescending(e => e.FileName, StringComparer.OrdinalIgnoreCase),
            "Date" => SortAscending ? entries.OrderBy(e => e.LastModifiedUtc) : entries.OrderByDescending(e => e.LastModifiedUtc),
            "Type" => SortAscending ? entries.OrderBy(e => e.Category.ToString()) : entries.OrderByDescending(e => e.Category.ToString()),
            _ => entries.OrderByDescending(e => e.SizeBytes)
        };
    }

    private void Sort(object? parameter)
    {
        var column = parameter?.ToString();
        if (string.IsNullOrEmpty(column))
        {
            return;
        }

        if (SortColumn == column)
        {
            SortAscending = !SortAscending;
        }
        else
        {
            SortColumn = column;
            SortAscending = false;
        }

        ApplyFilter(); // Re-apply with new sort
    }

    private static void OpenFile(LargeFileItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(item.Entry.Path) { UseShellExecute = true });
        }
        catch
        {
            // File may have been deleted or moved
        }
    }

    private static void OpenFolder(LargeFileItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        try
        {
            Process.Start("explorer.exe", $"/select,\"{item.Entry.Path}\"");
        }
        catch
        {
            // Explorer may fail silently
        }
    }

    private async Task MoveFileAsync(LargeFileItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        // In a real app, this would open a SaveFileDialog
        // For now, the ViewModel exposes the command and the UI handles the dialog
        // This is a placeholder that the UI layer will call back into
        await Task.CompletedTask;
    }

    public void MoveFileToDestination(LargeFileItemViewModel item, string destinationPath)
    {
        try
        {
            _fileSystem.MoveFile(item.Entry.Path, destinationPath);
            Results.Remove(item);
            StatusMessage = $"Moved {item.Entry.FileName} successfully.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Move failed: {ex.Message}";
            _logger.LogError(ex, "Failed to move file {Path}", item.Entry.Path);
        }
    }

    private void CheckLock(LargeFileItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        var lockInfo = _fileLockInspector.TryGetLockingProcess(item.Entry.Path);
        if (lockInfo is not null)
        {
            StatusMessage = $"File is locked by: {lockInfo.ProcessName} (PID: {lockInfo.ProcessId})";
        }
        else
        {
            StatusMessage = "File is not currently locked by any detectable process.";
        }
    }

    private async Task DeleteSelectedAsync()
    {
        var selectedItems = Results.Where(r => r.IsSelected).ToList();
        if (selectedItems.Count == 0)
        {
            return;
        }

        var totalSize = selectedItems.Sum(i => i.Entry.SizeBytes);
        var confirmation = System.Windows.MessageBox.Show(
            $"Delete {selectedItems.Count} file(s) totaling {FormatBytes(totalSize)}?\n\n" +
            "Files will be backed up in Recovery before deletion.",
            "Confirm Delete",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (confirmation != System.Windows.MessageBoxResult.Yes)
        {
            StatusMessage = "Delete cancelled by user.";
            return;
        }

        StatusMessage = "Deleting selected files...";

        try
        {
            var scanItems = selectedItems.Select(item => new ScanItem
            {
                Path = item.Entry.Path,
                SizeBytes = item.Entry.SizeBytes,
                LastModifiedUtc = item.Entry.LastModifiedUtc,
                RiskLevel = RiskLevel.Safe
            }).ToList();

            var scanResult = new ScanResult
            {
                FileCount = scanItems.Count,
                FolderCount = 0,
                TotalSizeBytes = scanItems.Sum(i => i.SizeBytes),
                Items = scanItems,
                Warnings = []
            };

            var report = await _executor.ExecuteAsync(scanResult);

            ReportSummary =
                $"Deleted: {report.FilesDeleted} file(s), " +
                $"Recovered: {FormatBytes(report.SpaceRecoveredBytes)}, " +
                $"Skipped (in use): {report.SkippedInUse.Count}, " +
                $"Errors: {report.Errors.Count}." +
                (report.RecoverySessionId is null
                    ? string.Empty
                    : $" Recovery session: {report.RecoverySessionId}.");

            // Remove deleted items from results
            foreach (var item in selectedItems)
            {
                Results.Remove(item);
            }

            StatusMessage = "Delete completed.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Delete failed: {ex.Message}";
            _logger.LogError(ex, "Large file delete failed.");
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
}

public sealed class LargeFileItemViewModel : ViewModelBase
{
    private bool _isSelected;

    public LargeFileItemViewModel(LargeFileEntry entry)
    {
        Entry = entry;
    }

    public LargeFileEntry Entry { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (Entry.IsProtected && value)
            {
                // Silently ignore selection if the file is protected
                return;
            }
            SetProperty(ref _isSelected, value);
        }
    }

    public bool IsProtected => Entry.IsProtected;
    public string? ProtectionReason => Entry.ProtectionReason;

    public string SizeDisplay => FormatBytes(Entry.SizeBytes);
    public string DateDisplay => Entry.LastModifiedUtc.ToString("yyyy-MM-dd HH:mm");
    public string CategoryDisplay => Entry.Category.ToString();
    public string LocationDisplay => Path.GetDirectoryName(Entry.Path) ?? Entry.Path;

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

public sealed record ThresholdOption(string Label, long ThresholdBytes)
{
    public override string ToString() => Label;
}

