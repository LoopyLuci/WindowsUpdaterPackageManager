using System.Net.Http.Headers;
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

    public App()
    {
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
                services.AddTransient<WupmGui.ViewModels.MainWindowViewModel>(sp => sp.GetRequiredService<MainWindowViewModel>());
                services.AddTransient<MainWindow>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await _host.StartAsync();

        var main = _host.Services.GetRequiredService<MainWindow>();
        main.DataContext = _host.Services.GetRequiredService<MainWindowViewModel>();
        main.Show();

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        using (_host)
        {
            await _host.StopAsync();
        }

        base.OnExit(e);
    }
}
