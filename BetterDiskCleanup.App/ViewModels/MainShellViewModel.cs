using System.Windows.Input;

namespace BetterDiskCleanup.App.ViewModels;

public sealed class MainShellViewModel : ViewModelBase
{
    private int _selectedIndex;

    public MainShellViewModel(
        DashboardViewModel dashboardViewModel,
        MainViewModel cleanupViewModel,
        BrowserCleanupViewModel browserCleanupViewModel,
        LargeFileFinderViewModel largeFileFinderViewModel,
        DuplicateFinderViewModel duplicateFinderViewModel,
        StartupManagerViewModel startupManagerViewModel,
        StorageAnalyzerViewModel storageAnalyzerViewModel,
        ReportsViewModel reportsViewModel,
        SettingsViewModel settingsViewModel,
        LogStore logStore)
    {
        DashboardViewModel = dashboardViewModel;
        CleanupViewModel = cleanupViewModel;
        BrowserCleanupViewModel = browserCleanupViewModel;
        LargeFileFinderViewModel = largeFileFinderViewModel;
        DuplicateFinderViewModel = duplicateFinderViewModel;
        StartupManagerViewModel = startupManagerViewModel;
        StorageAnalyzerViewModel = storageAnalyzerViewModel;
        ReportsViewModel = reportsViewModel;
        SettingsViewModel = settingsViewModel;
        LogStore = logStore;
        SelectPageCommand = new ParameterizedRelayCommand(
            p => SelectedIndex = int.TryParse(p?.ToString(), out var i) ? i : 0);
        ToggleLogCommand = new RelayCommand(() =>
        {
            IsLogVisible = !IsLogVisible;
            OnPropertyChanged(nameof(IsLogVisible));
        });
        ClearLogCommand = new RelayCommand(() => logStore.Clear());
    }

    public DashboardViewModel DashboardViewModel { get; }

    public MainViewModel CleanupViewModel { get; }

    public BrowserCleanupViewModel BrowserCleanupViewModel { get; }

    public LargeFileFinderViewModel LargeFileFinderViewModel { get; }

    public DuplicateFinderViewModel DuplicateFinderViewModel { get; }

    public StartupManagerViewModel StartupManagerViewModel { get; }

    public StorageAnalyzerViewModel StorageAnalyzerViewModel { get; }

    public ReportsViewModel ReportsViewModel { get; }

    public SettingsViewModel SettingsViewModel { get; }

    public LogStore LogStore { get; }

    public bool IsLogVisible { get; private set; } = true;

    public ICommand ToggleLogCommand { get; }
    public ICommand ClearLogCommand { get; }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (SetProperty(ref _selectedIndex, value))
            {
                OnPropertyChanged(nameof(IsDashboardPage));
                OnPropertyChanged(nameof(IsTempPage));
                OnPropertyChanged(nameof(IsBrowserPage));
                OnPropertyChanged(nameof(IsRecoveryPage));
                OnPropertyChanged(nameof(IsLargeFilesPage));
                OnPropertyChanged(nameof(IsDuplicatesPage));
                OnPropertyChanged(nameof(IsStartupPage));
                OnPropertyChanged(nameof(IsStorageAnalyzerPage));
                OnPropertyChanged(nameof(IsReportsPage));
                OnPropertyChanged(nameof(IsSettingsPage));

                if (_selectedIndex == 0)
                {
                    DashboardViewModel.RefreshCommand.Execute(null);
                }
            }
        }
    }

    public bool IsDashboardPage => _selectedIndex == 0;
    public bool IsTempPage => _selectedIndex == 1;
    public bool IsBrowserPage => _selectedIndex == 2;
    public bool IsRecoveryPage => _selectedIndex == 3;
    public bool IsLargeFilesPage => _selectedIndex == 4;
    public bool IsDuplicatesPage => _selectedIndex == 5;
    public bool IsStartupPage => _selectedIndex == 6;
    public bool IsStorageAnalyzerPage => _selectedIndex == 7;
    public bool IsReportsPage => _selectedIndex == 8;
    public bool IsSettingsPage => _selectedIndex == 9;

    public ICommand SelectPageCommand { get; }
}
