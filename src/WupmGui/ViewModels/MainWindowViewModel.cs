using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WupmGui.Models;
using WupmGui.Services;

namespace WupmGui.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IWupmApiClient _api;

    [ObservableProperty] private string _status = "Initializing...";
    [ObservableProperty] private bool _isConnected;

    public MainWindowViewModel(IWupmApiClient api)
    {
        _api = api;
        CurrentViewModel = this;
    }

    public ViewModelBase CurrentViewModel { get; }

    [RelayCommand]
    private async Task LoadedAsync()
    {
        try
        {
            var health = await _api.GetHealthAsync();
            IsConnected = string.Equals(health.Status, "ok", StringComparison.OrdinalIgnoreCase);
            Status = IsConnected ? "Connected" : "Disconnected";
        }
        catch
        {
            IsConnected = false;
            Status = "Disconnected";
        }
    }
}
