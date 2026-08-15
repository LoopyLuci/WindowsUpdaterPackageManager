using System.Collections.ObjectModel;
using System.Windows.Input;
using WupmGui.Services;
using WindowsUpdateAndPackageManager.Models;

namespace WupmGui.ViewModels;

public class CacheViewModel : ViewModelBase
{
    private readonly IWupmApiClient _api;

    public ObservableCollection<CacheEntry> Entries { get; } = new();

    private string _statusMessage = "Ready";
    public string StatusMessage
    {
        get => _statusMessage;
        set => Set(ref _statusMessage, value);
    }

    public CacheViewModel(IWupmApiClient api)
    {
        _api = api;
        LoadCommand = new AsyncRelayCommand(LoadAsync);
        PruneCommand = new AsyncRelayCommand(PruneAsync);
    }

    public ICommand LoadCommand { get; }
    public ICommand PruneCommand { get; }

    private async Task LoadAsync(CancellationToken ct)
    {
        StatusMessage = "Loading cache...";
        var entries = await _api.GetCacheEntriesAsync(ct);
        Entries.Clear();
        foreach (var entry in entries)
            Entries.Add(entry);

        StatusMessage = $"Loaded {Entries.Count} cache entries";
    }

    private async Task PruneAsync(CancellationToken ct)
    {
        StatusMessage = "Pruning cache...";
        await _api.PruneCacheAsync(ct);
        Entries.Clear();
        StatusMessage = "Cache pruned";
    }
}
