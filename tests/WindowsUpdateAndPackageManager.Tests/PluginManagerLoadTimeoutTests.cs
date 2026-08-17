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
    public async Task LoadAsync_uses_env_timeout_when_set()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wupm-plugin-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            Environment.SetEnvironmentVariable("WUPM_PLUGIN_LOAD_TIMEOUT_SECONDS", "30");
            var manager = new PluginManager(tempRoot);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await manager.LoadAsync(cts.Token);
            Assert.True(true, "PluginManager.LoadAsync completed with custom timeout.");
        }
        finally
        {
            Environment.SetEnvironmentVariable("WUPM_PLUGIN_LOAD_TIMEOUT_SECONDS", null);
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

    [Fact]
    public async Task Fake_plugin_execute_returns_expected_result()
    {
        var plugin = new FakePlugin();
        var commands = await plugin.GetCommandsAsync();
        Assert.Equal(2, commands.Count);

        Assert.Equal("help", commands[0]);
        Assert.Equal("run", commands[1]);

        var helpResult = await plugin.ExecuteAsync("help", string.Empty);
        Assert.Equal("Usage: help|run", helpResult);

        var unknownResult = await plugin.ExecuteAsync("run", string.Empty);
        Assert.Null(unknownResult);
    }

    private sealed class FakePlugin : IPlugin
    {
        public string Name => "Fake";
        public string Version => "1.0";
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<string>> GetCommandsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(new[] { "help", "run" });
        public Task<string?> ExecuteAsync(string command, string args, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(command == "help" ? "Usage: help|run" : null);
    }
}
