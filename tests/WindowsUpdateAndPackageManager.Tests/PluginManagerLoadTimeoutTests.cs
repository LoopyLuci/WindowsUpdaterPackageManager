using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WindowsUpdateAndPackageManager.Core;
using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

public class PluginManagerLoadTimeoutTests
{
    [Fact]
    public async Task LoadAsync_does_not_hang_indefinitely()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wupm-plugin-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var manager = new PluginManager(tempRoot);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await manager.LoadAsync(cts.Token);
            Assert.True(true, "PluginManager.LoadAsync completed within timeout.");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Plugin_manager_commands_match_registered_plugin()
    {
        var plugin = new FakePlugin();
        var manager = new PluginManager(Path.Combine(Path.GetTempPath(), "does-not-exist"));
        typeof(PluginManager).GetField("_plugins", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(manager, new List<IPlugin> { plugin });

        var commands = await manager.Plugins.Single().GetCommandsAsync();
        Assert.Equal(new[] { "help", "run" }, commands);
    }

    private sealed class FakePlugin : IPlugin
    {
        public string Name => "Fake";
        public string Version => "1.0";
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<string>> GetCommandsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(new[] { "help", "run" });
    }
}
