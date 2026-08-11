using System;
using System.IO;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using WindowsUpdateAndPackageManager.Core;
using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

[Collection("Console")]
public sealed class ServiceCliTests
{
    [Fact]
    public async Task Service_install_prints_success()
    {
        var services = BuildServicesWithFakeServiceManager();
        var output = new StringWriter();
        var original = Console.Out;
        try
        {
            Console.SetOut(output);
            await WindowsUpdateAndPackageManager.Commands.Cli.Run(new[] { "service", "install", "--repo", "https://example.invalid/repo" }, services);
        }
        finally
        {
            Console.SetOut(original);
            if (services is IDisposable d) d.Dispose();
        }

        Assert.Contains("Service installed.", output.ToString());
    }

    [Fact]
    public async Task Service_uninstall_prints_removed()
    {
        var services = BuildServicesWithFakeServiceManager();
        var output = new StringWriter();
        var original = Console.Out;
        try
        {
            Console.SetOut(output);
            await WindowsUpdateAndPackageManager.Commands.Cli.Run(new[] { "service", "uninstall" }, services);
        }
        finally
        {
            Console.SetOut(original);
            if (services is IDisposable d) d.Dispose();
        }

        Assert.Contains("Service removed.", output.ToString());
    }

    [Fact]
    public async Task Service_status_prints_status()
    {
        var services = BuildServicesWithFakeServiceManager();
        var output = new StringWriter();
        var original = Console.Out;
        try
        {
            Console.SetOut(output);
            await WindowsUpdateAndPackageManager.Commands.Cli.Run(new[] { "service", "status" }, services);
        }
        finally
        {
            Console.SetOut(original);
            if (services is IDisposable d) d.Dispose();
        }

        Assert.Contains("STATUS:", output.ToString());
    }

    private static IServiceProvider BuildServicesWithFakeServiceManager()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        WindowsUpdateAndPackageManager.Commands.Composition.RegisterInto(services, AppContext.BaseDirectory);
        services.AddSingleton<IServiceManager, FakeServiceManager>();
        return services.BuildServiceProvider();
    }

    private sealed class FakeServiceManager : IServiceManager
    {
        public Task<bool> InstallAsync(string? repositoryUrl = null, string? schedule = null, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<bool> UninstallAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<string?> StatusAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<string?>("STATUS: ready");
    }
}
