using System;
using System.Threading.Tasks;
using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

public class CompositionTests
{
    [Fact]
    public async Task Cli_help_returns_nonzero()
    {
        var root = AppDomain.CurrentDomain.BaseDirectory;
        var services = WindowsUpdateAndPackageManager.Commands.Composition.Build(root);
        try
        {
            var exitCode = await WindowsUpdateAndPackageManager.Commands.Cli.Run(new[] { "--help" }, services);
            Assert.NotEqual(0, exitCode);
        }
        finally
        {
            if (services is IDisposable d) d.Dispose();
        }
    }
}
