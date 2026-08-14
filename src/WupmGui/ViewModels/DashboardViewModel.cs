using System.Collections.ObjectModel;
using System.Windows.Input;
using WupmGui.Services;

namespace WupmGui.ViewModels;

public class DashboardViewModel : ViewModelBase
{
    private readonly IWupmApiClient _api;

    public ObservableCollection<object> Updates { get; } = new();

    private bool _isScanning;
    public bool IsScanning
    {
        get => _isScanning;
        set => Set(ref _isScanning, value);
    }

    private string _statusMessage = "Ready";
    public string StatusMessage
    {
        get => _statusMessage;
        set => Set(ref _statusMessage, value);
    }

    private int _progressValue;
    public int ProgressValue
    {
        get => _progressValue;
        set => Set(ref _progressValue, value);
    }

    public DashboardViewModel(IWupmApiClient api)
    {
        _api = api;
        ScanCommand = new AsyncRelayCommand(ScanAsync, () => !IsScanning);
        CancelCommand = new AsyncRelayCommand(CancelAsync, () => IsScanning);
    }

    public ICommand ScanCommand { get; }
    public ICommand CancelCommand { get; }

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
                Updates.Add(pkg);

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

    private async Task CancelAsync(CancellationToken ct)
    {
        StatusMessage = "Cancelled";
        await Task.CompletedTask;
    }
}
