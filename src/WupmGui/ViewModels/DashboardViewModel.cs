using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WupmGui.Models;
using WupmGui.Services;

namespace WupmGui.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly IWupmApiClient _api;

    [ObservableProperty] private ObservableCollection<PackageItem> _updates = new();
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private string _statusMessage = "Ready";
    [ObservableProperty] private int _progressValue;

    public DashboardViewModel(IWupmApiClient api)
    {
        _api = api;
    }

    public IAsyncRelayCommand ScanCommand => new AsyncRelayCommand(ScanAsync, () => !IsScanning);
    public IAsyncRelayCommand<PackageItem> InstallCommand => new AsyncRelayCommand<PackageItem>(InstallAsync);
    public IAsyncRelayCommand CancelCommand => new AsyncRelayCommand(CancelAsync, () => IsScanning);

    private async Task ScanAsync(CancellationToken ct)
    {
        IsScanning = true;
        StatusMessage = "Scanning...";
        ProgressValue = 0;

        try
        {
            var result = await _api.ScanAsync(false, ct);
            Updates.Clear();
            foreach (var pkg in result.Packages)
                Updates.Add(new PackageItem(pkg));

            StatusMessage = $"Found {Updates.Count} updates";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Scan failed: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    private async Task InstallAsync(PackageItem? item, CancellationToken ct)
    {
        if (item is null) return;
        StatusMessage = $"Installing {item.Title}...";
        try
        {
            var result = await _api.InstallAsync(item.Package, ct);
            StatusMessage = result.Success ? $"Installed {item.Title}" : $"Install failed: {result.Message}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Install failed: {ex.Message}";
        }
    }

    private Task CancelAsync(CancellationToken ct)
    {
        StatusMessage = "Cancelled";
        return Task.CompletedTask;
    }
}
