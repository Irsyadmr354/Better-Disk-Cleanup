using System.Windows.Input;

namespace BetterDiskCleanup.App.ViewModels;

public sealed class MainShellViewModel : ViewModelBase
{
    private int _selectedIndex;

    public MainShellViewModel(
        MainViewModel cleanupViewModel,
        RecoveryHistoryViewModel recoveryHistoryViewModel,
        BrowserCleanupViewModel browserCleanupViewModel,
        LargeFileFinderViewModel largeFileFinderViewModel)
    {
        CleanupViewModel = cleanupViewModel;
        RecoveryHistoryViewModel = recoveryHistoryViewModel;
        BrowserCleanupViewModel = browserCleanupViewModel;
        LargeFileFinderViewModel = largeFileFinderViewModel;
        SelectPageCommand = new ParameterizedRelayCommand(
            p => SelectedIndex = int.TryParse(p?.ToString(), out var i) ? i : 0);
    }

    public MainViewModel CleanupViewModel { get; }

    public RecoveryHistoryViewModel RecoveryHistoryViewModel { get; }

    public BrowserCleanupViewModel BrowserCleanupViewModel { get; }

    public LargeFileFinderViewModel LargeFileFinderViewModel { get; }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (SetProperty(ref _selectedIndex, value))
            {
                OnPropertyChanged(nameof(IsTempPage));
                OnPropertyChanged(nameof(IsBrowserPage));
                OnPropertyChanged(nameof(IsRecoveryPage));
                OnPropertyChanged(nameof(IsLargeFilesPage));
            }
        }
    }

    public bool IsTempPage => _selectedIndex == 0;
    public bool IsBrowserPage => _selectedIndex == 1;
    public bool IsRecoveryPage => _selectedIndex == 2;
    public bool IsLargeFilesPage => _selectedIndex == 3;

    public ICommand SelectPageCommand { get; }
}
