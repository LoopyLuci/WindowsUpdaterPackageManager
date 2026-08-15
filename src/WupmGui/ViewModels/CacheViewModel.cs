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
        InvalidateSelectedCommand = new AsyncRelayCommand(InvalidateSelectedAsync, () => SelectedEntry is not null);
    }

    public ICommand LoadCommand { get; }
    public ICommand PruneCommand { get; }
    public ICommand InvalidateSelectedCommand { get; }

    private CacheEntry? _selectedEntry;
    public CacheEntry? SelectedEntry
    {
        get => _selectedEntry;
        set => Set(ref _selectedEntry, value);
    }

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
        await LoadAsync(ct);
        StatusMessage = "Cache pruned";
    }

    private async Task InvalidateSelectedAsync(CancellationToken ct)
    {
        if (SelectedEntry is null) return;
        StatusMessage = $"Invalidated {SelectedEntry.PackageId}";
        await Task.CompletedTask;
    }
}
