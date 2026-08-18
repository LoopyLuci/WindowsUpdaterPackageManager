using System.Collections.ObjectModel;
using System.Windows;
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

    private double _progress;
    public double Progress
    {
        get => _progress;
        set => Set(ref _progress, value);
    }

    private bool _isProgressVisible;
    public bool IsProgressVisible
    {
        get => _isProgressVisible;
        set => Set(ref _isProgressVisible, value);
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
        IsProgressVisible = false;
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
            Progress = 0;
            IsProgressVisible = true;
            var result = await _api.InstallUpdateAsync(SelectedUpdate, ct);
            Progress = 100;
            StatusMessage = result.Success ? "Install completed" : $"Install failed: {result.Message}";
            if (!result.Success)
            {
                MessageBox.Show(StatusMessage, "Update install", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Install error: {ex.Message}";
            MessageBox.Show(StatusMessage, "Update install", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsProgressVisible = false;
        }
    }
}
