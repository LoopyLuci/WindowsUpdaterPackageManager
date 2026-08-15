using System.Collections.ObjectModel;
using System.Windows.Input;
using WupmGui.Services;
using WindowsUpdateAndPackageManager.Models;
using WindowsUpdateAndPackageManager.Core;

namespace WupmGui.ViewModels;

public class PluginsViewModel : ViewModelBase
{
    private readonly IWupmApiClient _api;

    public ObservableCollection<PluginRegistryEntry> Plugins { get; } = new();

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
        var entries = await _api.GetPluginsAsync(ct);
        Plugins.Clear();
        foreach (var entry in entries)
            Plugins.Add(entry);

        StatusMessage = $"Loaded {Plugins.Count} plugins";
    }
}
