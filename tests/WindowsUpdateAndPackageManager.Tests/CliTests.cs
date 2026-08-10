using System;
using System.Threading.Tasks;
using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

public class CompositionTests
{
    [Fact]
    public async Task Cli_sync_command_is_wired()
    {
        var root = AppContext.BaseDirectory;
        var services = WindowsUpdateAndPackageManager.Commands.Composition.Build(root);
        try
        {
            var exitCode = await WindowsUpdateAndPackageManager.Commands.Cli.Run(new[] { "sync", "--repo", "https://example.invalid/repo" }, services);
            Assert.Equal(0, exitCode);
        }
        finally
        {
            if (services is IDisposable d) d.Dispose();
        }
    }
}
