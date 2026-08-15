using System.IO;
using System.Windows;
using System.Windows.Controls;
using WupmGui.ViewModels;

namespace WupmGui.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        File.AppendAllText("C:/Users/limpi/AppData/Local/WupmGui/startup.log", $"[GUI] MainWindow ctor start {DateTime.Now:O}{Environment.NewLine}");
        InitializeComponent();
        File.AppendAllText("C:/Users/limpi/AppData/Local/WupmGui/startup.log", $"[GUI] MainWindow ctor end {DateTime.Now:O}{Environment.NewLine}");
        Loaded += (_, __) =>
        {
            ShowInTaskbar = true;
            Visibility = Visibility.Visible;
            Activate();
            Topmost = true;
            Topmost = false;
            Focus();
        };
    }

    private void OnTabChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        var tab = (e.OriginalSource as TabControl)?.SelectedItem as TabItem;
        if (tab is null) return;

        vm.CurrentViewModel = tab.Header switch
        {
            "Dashboard" => vm.Dashboard,
            "Drivers" => vm.Drivers,
            "History" => vm.History,
            "Plugins" => vm.Plugins,
            "Marketplace" => vm.Marketplace,
            "Cache" => vm.Cache,
            "Settings" => vm.Settings,
            _ => vm.Dashboard
        };
    }
}
