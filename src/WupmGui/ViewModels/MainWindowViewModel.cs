using System.IO;
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
    public UpdatesViewModel Updates { get; }
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
        File.AppendAllText("C:/Users/limpi/AppData/Local/WupmGui/startup.log", $"[GUI] MainWindowViewModel ctor start {DateTime.Now:O}{Environment.NewLine}");
        _api = api;
        Dashboard = new DashboardViewModel(api);
        Drivers = new DriversViewModel(api);
        History = new HistoryViewModel(api);
        Plugins = new PluginsViewModel(api);
        Marketplace = new MarketplaceViewModel(api);
        Cache = new CacheViewModel(api);
        Updates = new UpdatesViewModel(api);
        Settings = new SettingsViewModel(api);
        CurrentViewModel = Dashboard;
        File.AppendAllText("C:/Users/limpi/AppData/Local/WupmGui/startup.log", $"[GUI] MainWindowViewModel ctor end {DateTime.Now:O}{Environment.NewLine}");
    }

    public ICommand LoadedCommand => new AsyncRelayCommand(LoadedAsync);
    public ICommand SwitchTabCommand => new AsyncRelayCommand<string>(SwitchTabAsync);

    private async Task SwitchTabAsync(string? tab, CancellationToken ct)
    {
        await Task.CompletedTask;
        if (string.Equals(tab, nameof(Dashboard), StringComparison.OrdinalIgnoreCase))
        {
            CurrentViewModel = Dashboard;
        }
        else if (string.Equals(tab, nameof(Drivers), StringComparison.OrdinalIgnoreCase))
        {
            CurrentViewModel = Drivers;
        }
        else if (string.Equals(tab, nameof(History), StringComparison.OrdinalIgnoreCase))
        {
            CurrentViewModel = History;
        }
        else if (string.Equals(tab, nameof(Plugins), StringComparison.OrdinalIgnoreCase))
        {
            CurrentViewModel = Plugins;
        }
        else if (string.Equals(tab, nameof(Marketplace), StringComparison.OrdinalIgnoreCase))
        {
            CurrentViewModel = Marketplace;
        }
        else if (string.Equals(tab, nameof(Cache), StringComparison.OrdinalIgnoreCase))
        {
            CurrentViewModel = Cache;
        }
        else if (string.Equals(tab, nameof(Updates), StringComparison.OrdinalIgnoreCase))
        {
            CurrentViewModel = Updates;
        }
        else if (string.Equals(tab, nameof(Settings), StringComparison.OrdinalIgnoreCase))
        {
            CurrentViewModel = Settings;
        }
    }

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
