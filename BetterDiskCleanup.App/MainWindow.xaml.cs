using BetterDiskCleanup.App.ViewModels;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BetterDiskCleanup.App;

public partial class MainWindow : Window
{
    public MainWindow(MainShellViewModel shellViewModel)
    {
        InitializeComponent();
        DataContext = shellViewModel;
        Loaded += async (_, _) => await shellViewModel.RecoveryHistoryViewModel.RefreshAsync();

        // Auto-scroll log to bottom when new entries are added
        shellViewModel.LogStore.Entries.CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Add && LogListBox.Items.Count > 0)
            {
                try
                {
                    LogListBox.ScrollIntoView(LogListBox.Items[^1]);
                }
                catch
                {
                    // VirtualizingStackPanel may throw during rapid updates — safe to ignore
                }
            }
        };
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
        }
        else
        {
            if (WindowState == WindowState.Maximized)
            {
                var point = PointToScreen(e.GetPosition(this));
                WindowState = WindowState.Normal;
                Left = point.X - Width / 2;
                Top = point.Y - 20;
            }
            DragMove();
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e)
        => ToggleMaximize();

    private void Close_Click(object sender, RoutedEventArgs e)
        => Close();

    private void ToggleMaximize()
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            MaximizeBtn.Content = "□";
        }
        else
        {
            WindowState = WindowState.Maximized;
            MaximizeBtn.Content = "❐";
        }
    }
}
