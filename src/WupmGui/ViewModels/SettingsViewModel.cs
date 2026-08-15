using System.IO;
using System.Windows;
using System.Windows.Input;
using WupmGui.Services;

namespace WupmGui.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly IWupmApiClient _api;
    private readonly string _diagPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WupmGui", "startup.log");

    public string Version { get; } = "1.0.0";

    private bool _startMinimized;
    public bool StartMinimized
    {
        get => _startMinimized;
        set => Set(ref _startMinimized, value);
    }

    private bool _checkForUpdatesOnStartup = true;
    public bool CheckForUpdatesOnStartup
    {
        get => _checkForUpdatesOnStartup;
        set => Set(ref _checkForUpdatesOnStartup, value);
    }

    private bool _telemetryEnabled;
    public bool TelemetryEnabled
    {
        get => _telemetryEnabled;
        set => Set(ref _telemetryEnabled, value);
    }

    private string _serviceStatus = "Not installed";
    public string ServiceStatus
    {
        get => _serviceStatus;
        set => Set(ref _serviceStatus, value);
    }

    public ICommand InstallServiceCommand => new AsyncRelayCommand(InstallServiceAsync);
    public ICommand UninstallServiceCommand => new AsyncRelayCommand(UninstallServiceAsync);
    public ICommand CopyDiagnosticsCommand => new AsyncRelayCommand(CopyDiagnosticsAsync);

    public SettingsViewModel(IWupmApiClient api)
    {
        _api = api;
        _ = LoadServiceStatusAsync();
    }

    private async Task LoadServiceStatusAsync()
    {
        try
        {
            var status = await _api.GetServiceStatusAsync();
            ServiceStatus = status["message"]?.ToString() ?? "Unknown";
        }
        catch
        {
            ServiceStatus = "Unable to load service status";
        }
    }

    private async Task InstallServiceAsync(CancellationToken ct)
    {
        try
        {
            ServiceStatus = "Installing...";
            await _api.InstallServiceAsync(ct);
            ServiceStatus = "Installed";
        }
        catch (Exception ex)
        {
            ServiceStatus = $"Install failed: {ex.Message}";
        }
    }

    private async Task UninstallServiceAsync(CancellationToken ct)
    {
        try
        {
            ServiceStatus = "Uninstalling...";
            await _api.UninstallServiceAsync(ct);
            ServiceStatus = "Uninstalled";
        }
        catch (Exception ex)
        {
            ServiceStatus = $"Uninstall failed: {ex.Message}";
        }
    }

    private Task CopyDiagnosticsAsync(CancellationToken ct)
    {
        try
        {
            if (File.Exists(_diagPath))
            {
                var text = File.ReadAllText(_diagPath);
                Clipboard.SetText(text);
                ServiceStatus = "Diagnostics copied to clipboard";
            }
            else
            {
                ServiceStatus = "No diagnostics found";
            }
        }
        catch (Exception ex)
        {
            ServiceStatus = $"Copy failed: {ex.Message}";
        }
        return Task.CompletedTask;
    }
}
