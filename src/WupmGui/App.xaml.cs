using System.Net.Http.Headers;
using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WupmGui.Services;
using WupmGui.ViewModels;
using WupmGui.Views;

namespace WupmGui;

public partial class App : Application
{
    private readonly IHost _host;
    private static readonly string DiagPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WupmGui", "startup.log");
    private GuiControlServer? _controlServer;

    public App()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DiagPath)!);
            File.AppendAllText(DiagPath, $"[App] ctor start {DateTime.Now:O}{Environment.NewLine}");
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    var apiBase = Environment.GetEnvironmentVariable("WUPM_API_URL") ?? "http://localhost:5000";
                    var apiKey = Environment.GetEnvironmentVariable("WUPM_API_KEY");

                    services.AddHttpClient<IWupmApiClient, WupmApiClient>(client =>
                    {
                        client.BaseAddress = new Uri(apiBase);
                        client.Timeout = TimeSpan.FromSeconds(100);
                        if (!string.IsNullOrWhiteSpace(apiKey))
                        {
                            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                        }
                    });

                    services.AddSingleton<MainWindowViewModel>();
                    services.AddSingleton<DashboardViewModel>();
                    services.AddSingleton<DriversViewModel>();
                    services.AddSingleton<HistoryViewModel>();
                    services.AddSingleton<PluginsViewModel>();
                    services.AddSingleton<MarketplaceViewModel>();
                    services.AddSingleton<CacheViewModel>();
                    services.AddSingleton<SettingsViewModel>();
                    services.AddTransient<MainWindow>();
                })
                .Build();
            File.AppendAllText(DiagPath, $"[App] host built {DateTime.Now:O}{Environment.NewLine}");
        }
        catch (Exception ex)
        {
            File.AppendAllText(DiagPath, $"[App] ctor failed: {ex}{Environment.NewLine}");
            MessageBox.Show($"App ctor failed: {ex.Message}\n\n{ex.StackTrace}", "WupmGui Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        try
        {
            File.AppendAllText(DiagPath, $"[GUI] OnStartup begin {DateTime.Now:O}{Environment.NewLine}");
            await _host.StartAsync();
            File.AppendAllText(DiagPath, $"[GUI] host started {DateTime.Now:O}{Environment.NewLine}");

            var main = _host.Services.GetRequiredService<MainWindow>();
            File.AppendAllText(DiagPath, $"[GUI] MainWindow resolved {DateTime.Now:O}{Environment.NewLine}");

            File.AppendAllText(DiagPath, $"[GUI] about to resolve MainWindowViewModel {DateTime.Now:O}{Environment.NewLine}");
            var vm = _host.Services.GetRequiredService<MainWindowViewModel>();
            File.AppendAllText(DiagPath, $"[GUI] MainWindowViewModel resolved {DateTime.Now:O}{Environment.NewLine}");

            main.DataContext = vm;
            File.AppendAllText(DiagPath, $"[GUI] DataContext set {DateTime.Now:O}{Environment.NewLine}");

            main.WindowState = WindowState.Normal;
            main.Visibility = Visibility.Visible;
            main.Topmost = true;
            File.AppendAllText(DiagPath, $"[GUI] about to Show() {DateTime.Now:O}{Environment.NewLine}");

            main.Show();
            File.AppendAllText(DiagPath, $"[GUI] Show() called {DateTime.Now:O}{Environment.NewLine}");

            main.Activate();
            File.AppendAllText(DiagPath, $"[GUI] Activate() called {DateTime.Now:O}{Environment.NewLine}");

            var interop = new System.Windows.Interop.WindowInteropHelper(main);
            File.AppendAllText(DiagPath, $"[GUI] MainWindow shown, Handle={interop.Handle}, State={main.WindowState}, Visibility={main.Visibility} {DateTime.Now:O}{Environment.NewLine}");

            var controlServer = new GuiControlServer(main, vm);
            _controlServer = controlServer;
            File.AppendAllText(DiagPath, $"[GUI] control server started {DateTime.Now:O}{Environment.NewLine}");

            base.OnStartup(e);
        }
        catch (Exception ex)
        {
            File.AppendAllText(DiagPath, $"[GUI] Startup failed: {ex}{Environment.NewLine}");
            MessageBox.Show($"Startup failed: {ex.Message}\n\n{ex.StackTrace}", "WupmGui Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        try { _controlServer?.Dispose(); } catch { }
        using (_host)
        {
            await _host.StopAsync();
        }
        base.OnExit(e);
    }
}
