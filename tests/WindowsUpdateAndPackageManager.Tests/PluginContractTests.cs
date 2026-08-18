using System.Reflection;
using WindowsUpdateAndPackageManager.Core;
using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

public sealed class PluginContractTests
{
    [Fact]
    public void SamplePlugin_implements_required_contract()
    {
        var baseDir = AppContext.BaseDirectory;
        var pluginPath = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", "src", "plugins", "SamplePlugin", "bin", "Release", "net10.0-windows7.0", "SamplePlugin.dll"));
        Assert.True(File.Exists(pluginPath), $"Plugin DLL not found at {pluginPath}");
        var asm = Assembly.LoadFrom(pluginPath);
        var pluginType = asm.GetTypes().First(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract);
        var plugin = Activator.CreateInstance(pluginType) as IPlugin;

        Assert.NotNull(plugin);
        Assert.False(string.IsNullOrWhiteSpace(plugin!.Name));
        Assert.NotNull(plugin.GetCommandsAsync().Result);
        Assert.True(plugin.GetCommandsAsync().Result.Count > 0);
    }
}
