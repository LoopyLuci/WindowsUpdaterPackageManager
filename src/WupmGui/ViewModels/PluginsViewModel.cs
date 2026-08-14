using System.Collections.ObjectModel;
using System.Windows.Input;
using WupmGui.Services;

namespace WupmGui.ViewModels;

public class PluginsViewModel : ViewModelBase
{
    private readonly IWupmApiClient _api;

    public ObservableCollection<object> Plugins { get; } = new();

    private string _statusMessage = "Ready";
    public string StatusMessage
    {
        get => _statusMessage;
        set => Set(ref _statusMessage, value);
    }

    public PluginsViewModel(IWupmApiClient api)
    {
        _api = api;
        LoadCommand = new AsyncRelayCommand(LoadAsync);
    }

    public ICommand LoadCommand { get; }

    private async Task LoadAsync(CancellationToken ct)
    {
        StatusMessage = "Loading plugins...";
        Plugins.Clear();
        StatusMessage = $"Loaded {Plugins.Count} plugins";
    }
}
