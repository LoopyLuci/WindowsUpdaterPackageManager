using System.Windows;
using System.Windows.Controls;
using WupmGui.ViewModels;

namespace WupmGui.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
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
            "Plugins" => vm.Dashboard,
            "Marketplace" => vm.Dashboard,
            "Settings" => vm.Settings,
            _ => vm.Dashboard
        };
    }
}
