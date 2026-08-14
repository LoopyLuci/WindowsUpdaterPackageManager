using System.Collections.ObjectModel;
using System.Windows.Input;
using WupmGui.Services;

namespace WupmGui.ViewModels;

public class CacheViewModel : ViewModelBase
{
    private readonly IWupmApiClient _api;

    public ObservableCollection<object> Entries { get; } = new();

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
        Entries.Clear();
        StatusMessage = $"Loaded {Entries.Count} cache entries";
    }

    private async Task PruneAsync(CancellationToken ct)
    {
        StatusMessage = "Pruning cache...";
        Entries.Clear();
        StatusMessage = "Cache pruned";
    }
}
