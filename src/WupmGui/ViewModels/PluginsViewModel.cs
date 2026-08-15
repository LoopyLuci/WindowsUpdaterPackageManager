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

    private PluginRegistryEntry? _selectedPlugin;
    public PluginRegistryEntry? SelectedPlugin
    {
        get => _selectedPlugin;
        set => Set(ref _selectedPlugin, value);
    }

    public PluginsViewModel(IWupmApiClient api)
    {
        _api = api;
        LoadCommand = new AsyncRelayCommand(LoadAsync);
        ToggleSelectedCommand = new AsyncRelayCommand(ToggleSelectedAsync, () => SelectedPlugin is not null);
        ExecuteSelectedCommand = new AsyncRelayCommand(ExecuteSelectedAsync, () => SelectedPlugin is not null);
    }

    public ICommand LoadCommand { get; }
    public ICommand ToggleSelectedCommand { get; }

    private async Task LoadAsync(CancellationToken ct)
    {
        StatusMessage = "Loading plugins...";
        var entries = await _api.GetPluginsAsync(ct);
        Plugins.Clear();
        foreach (var entry in entries)
            Plugins.Add(entry);

        StatusMessage = $"Loaded {Plugins.Count} plugins";
    }

    private async Task ToggleSelectedAsync(CancellationToken ct)
    {
        if (SelectedPlugin is null) return;
        var next = !SelectedPlugin.Enabled;
        await _api.TogglePluginAsync(SelectedPlugin.Name, next, ct);
        SelectedPlugin.Enabled = next;
        StatusMessage = $"{(next ? "Enabled" : "Disabled")} {SelectedPlugin.Name}";
    }

    public ICommand ExecuteSelectedCommand { get; }

    private async Task ExecuteSelectedAsync(CancellationToken ct)
    {
        if (SelectedPlugin is null) return;
        try
        {
            var result = await _api.ExecutePluginAsync(SelectedPlugin.Name, "default", string.Empty, ct);
            StatusMessage = $"Executed: {result?["output"]?.ToString() ?? "ok"}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Execute failed: {ex.Message}";
        }
    }
}
