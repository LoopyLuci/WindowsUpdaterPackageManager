using System.Windows.Input;
using WupmGui.Services;

namespace WupmGui.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly IWupmApiClient _api;

    public DashboardViewModel Dashboard { get; }
    public DriversViewModel Drivers { get; }
    public HistoryViewModel History { get; }
    public PluginsViewModel Plugins { get; }
    public MarketplaceViewModel Marketplace { get; }
    public CacheViewModel Cache { get; }
    public SettingsViewModel Settings { get; }

    private ViewModelBase _currentViewModel = null!;
    public ViewModelBase CurrentViewModel
    {
        get => _currentViewModel;
        set => Set(ref _currentViewModel, value);
    }

    private string _status = "Initializing...";
    public string Status
    {
        get => _status;
        set => Set(ref _status, value);
    }

    private bool _isConnected;
    public bool IsConnected
    {
        get => _isConnected;
        set => Set(ref _isConnected, value);
    }

    public MainWindowViewModel(IWupmApiClient api)
    {
        _api = api;
        Dashboard = new DashboardViewModel(api);
        Drivers = new DriversViewModel(api);
        History = new HistoryViewModel(api);
        Plugins = new PluginsViewModel(api);
        Marketplace = new MarketplaceViewModel(api);
        Cache = new CacheViewModel(api);
        Settings = new SettingsViewModel();
        CurrentViewModel = Dashboard;
    }

    public ICommand LoadedCommand => new AsyncRelayCommand(LoadedAsync);

    private async Task LoadedAsync(CancellationToken ct)
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
