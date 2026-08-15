using System.Collections.ObjectModel;
using System.Windows.Input;
using WupmGui.Services;
using WindowsUpdateAndPackageManager.Models;

namespace WupmGui.ViewModels;

public class HistoryViewModel : ViewModelBase
{
    private readonly IWupmApiClient _api;

    public ObservableCollection<AuditEntry> History { get; } = new();

    private string _statusMessage = "Ready";
    public string StatusMessage
    {
        get => _statusMessage;
        set => Set(ref _statusMessage, value);
    }

    public HistoryViewModel(IWupmApiClient api)
    {
        _api = api;
        LoadCommand = new AsyncRelayCommand(LoadAsync);
    }

    public ICommand LoadCommand { get; }

    private async Task LoadAsync(CancellationToken ct)
    {
        StatusMessage = "Loading history...";
        var entries = await _api.GetAuditAsync(null, null, null, ct);
        History.Clear();
        foreach (var entry in entries)
            History.Add(entry);

        StatusMessage = $"Loaded {History.Count} entries";
    }
}
