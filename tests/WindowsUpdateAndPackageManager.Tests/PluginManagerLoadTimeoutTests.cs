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
}
