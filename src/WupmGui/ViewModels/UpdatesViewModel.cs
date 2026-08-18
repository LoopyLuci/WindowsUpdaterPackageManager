using System.Collections.ObjectModel;
using System.Windows.Input;
using WupmGui.Services;
using WindowsUpdateAndPackageManager.Models;

namespace WupmGui.ViewModels;

public class UpdatesViewModel : ViewModelBase
{
    private readonly IWupmApiClient _api;
    public ObservableCollection<UpdateItem> Items { get; } = new();

    private string _statusMessage = "Ready";
    public string StatusMessage
    {
        get => _statusMessage;
        set => Set(ref _statusMessage, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand InstallSelectedCommand { get; }

    private UpdateItem? _selectedUpdate;
    public UpdateItem? SelectedUpdate
    {
        get => _selectedUpdate;
        set => Set(ref _selectedUpdate, value);
    }

    public UpdatesViewModel(IWupmApiClient api)
    {
        _api = api;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        InstallSelectedCommand = new AsyncRelayCommand(InstallSelectedAsync, () => SelectedUpdate is not null);
    }

    private async Task RefreshAsync(CancellationToken ct)
    {
        StatusMessage = "Refreshing updates...";
        try
        {
            var updates = await _api.GetUpdatesAsync("10.0");
            Items.Clear();
            foreach (var u in updates)
            {
                Items.Add(u);
            }
            StatusMessage = $"Loaded {Items.Count} update(s)";
        }
        catch
        {
            StatusMessage = "Refresh failed";
        }
    }

    private async Task InstallSelectedAsync(CancellationToken ct)
    {
        try
        {
            if (SelectedUpdate is null) return;
            StatusMessage = $"Installing {SelectedUpdate.PackageId}@{SelectedUpdate.Version}...";
            var result = await _api.InstallUpdateAsync(SelectedUpdate, ct);
            StatusMessage = result.Success ? "Install completed" : $"Install failed: {result.Message}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Install error: {ex.Message}";
        }
    }
}
