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
        var services = BuildServicesWithFakeServiceManager(installResult: true);
        var output = RunCli(services, "service install --repo https://example.invalid/repo");
        Assert.Equal("Service installed.", output.Text.Trim());
    }

    [Fact]
    public async Task Service_uninstall_prints_removed()
    {
        var services = BuildServicesWithFakeServiceManager(uninstallResult: true);
        var output = RunCli(services, "service uninstall");
        Assert.Equal("Service removed.", output.Text.Trim());
    }

    [Fact]
    public async Task Service_status_prints_status()
    {
        var services = BuildServicesWithFakeServiceManager(statusText: "Ready");
        var output = RunCli(services, "service status");
        Assert.Equal("Ready", output.Text.Trim());
    }

    private static IServiceProvider BuildServicesWithFakeServiceManager(bool? installResult = null, bool? uninstallResult = null, string? statusText = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IServiceManager, FakeServiceManager>(_ => new FakeServiceManager(installResult, uninstallResult, statusText));
        return services.BuildServiceProvider();
    }

    private static (string Text, int ExitCode) RunCli(IServiceProvider services, string args)
    {
        var originalOut = Console.Out;
        try
        {
            var sb = new StringBuilder();
            using var writer = new StringWriter(sb);
            Console.SetOut(writer);

            var exit = WindowsUpdateAndPackageManager.Commands.Cli.Run(args.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), services).GetAwaiter().GetResult();
            writer.Flush();
            return (sb.ToString(), exit);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    private sealed class FakeServiceManager : IServiceManager
    {
        private readonly bool? _installResult;
        private readonly bool? _uninstallResult;
        private readonly string? _statusText;

        public FakeServiceManager(bool? installResult, bool? uninstallResult, string? statusText)
        {
            _installResult = installResult;
            _uninstallResult = uninstallResult;
            _statusText = statusText;
        }

        public Task<bool> InstallAsync(string? repositoryUrl = null, string? schedule = null, CancellationToken cancellationToken = default)
            => Task.FromResult(_installResult ?? false);

        public Task<bool> UninstallAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_uninstallResult ?? false);

        public Task<string?> StatusAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(_statusText);
    }
}
