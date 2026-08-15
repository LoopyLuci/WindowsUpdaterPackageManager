using System.Collections.ObjectModel;
using System.Windows.Input;
using WupmGui.Services;
using WindowsUpdateAndPackageManager.Models;

namespace WupmGui.ViewModels;

public class DriversViewModel : ViewModelBase
{
    private readonly IWupmApiClient _api;

    public ObservableCollection<PackageManifest> Drivers { get; } = new();

    private string _statusMessage = "Ready";
    public string StatusMessage
    {
        get => _statusMessage;
        set => Set(ref _statusMessage, value);
    }

    public DriversViewModel(IWupmApiClient api)
    {
        _api = api;
        ScanCommand = new AsyncRelayCommand(ScanAsync);
    }

    public ICommand ScanCommand { get; }

    private async Task ScanAsync(CancellationToken ct)
    {
        StatusMessage = "Scanning drivers...";
        var result = await _api.ScanAsync(false, ct);
        Drivers.Clear();
        foreach (var pkg in result.Packages.Where(p => p.IsDriver))
            Drivers.Add(pkg);

        StatusMessage = $"Found {Drivers.Count} driver updates";
    }
}
