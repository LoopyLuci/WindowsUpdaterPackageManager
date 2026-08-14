using System.IO;
using Microsoft.Extensions.DependencyInjection;
using WindowsUpdateAndPackageManager.Core;
using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

[Collection("Console")]
public sealed class PluginRegistryValidationTests
{
    private static IServiceProvider BuildServices(string dataRoot)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPluginRegistry>(new FilePluginRegistry(dataRoot));
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Validate_returns_no_issues_for_valid_registry()
    {
        var dataRoot = Path.Combine(Path.GetTempPath(), "wupm-registry-" + Guid.NewGuid().ToString("N"));
        var registry = new FilePluginRegistry(dataRoot);
        var pluginPath = Path.Combine(dataRoot, "plugins", "sample.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(pluginPath)!);
        File.WriteAllText(pluginPath, "sample");

        await registry.AddAsync("sample", "1.0.0", pluginPath, string.Empty);
        var issues = await registry.ValidateAsync();

        Assert.Empty(issues);
    }

    [Fact]
    public async Task Validate_reports_missing_file()
    {
        var dataRoot = Path.Combine(Path.GetTempPath(), "wupm-registry-" + Guid.NewGuid().ToString("N"));
        var registry = new FilePluginRegistry(dataRoot);
        await registry.AddAsync("missing", "1.0.0", Path.Combine(dataRoot, "plugins", "missing.dll"), string.Empty);
        var issues = await registry.ValidateAsync();

        Assert.Contains(issues, i => i.Contains("missing"));
    }

    [Fact]
    public async Task Validate_reports_duplicate_name()
    {
        var dataRoot = Path.Combine(Path.GetTempPath(), "wupm-registry-" + Guid.NewGuid().ToString("N"));
        var registry = new FilePluginRegistry(dataRoot);
        var pluginPath = Path.Combine(dataRoot, "plugins", "dup.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(pluginPath)!);
        File.WriteAllText(pluginPath, "dup");

        await registry.AddAsync("dup", "1.0.0", pluginPath, string.Empty);
        await registry.AddAsync("dup", "2.0.0", pluginPath, string.Empty);
        var issues = await registry.ValidateAsync();

        Assert.Contains(issues, i => i.Contains("Duplicate"));
    }

    [Fact]
    public async Task Validate_reports_missing_dependency()
    {
        var dataRoot = Path.Combine(Path.GetTempPath(), "wupm-registry-" + Guid.NewGuid().ToString("N"));
        var registry = new FilePluginRegistry(dataRoot);
        var pluginPath = Path.Combine(dataRoot, "plugins", "child.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(pluginPath)!);
        File.WriteAllText(pluginPath, "child");

        await registry.AddAsync("child", "1.0.0", pluginPath, "missing-dep");
        var issues = await registry.ValidateAsync();

        Assert.Contains(issues, i => i.Contains("missing-dep"));
    }
}
