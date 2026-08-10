namespace WindowsUpdateAndPackageManager.Tests;

public class CompositionTests
{
    [Fact]
    public async Task Cli_help_does_not_crash()
    {
        var root = AppDomain.CurrentDomain.BaseDirectory;
        var services = WindowsUpdateAndPackageManager.Commands.Composition.Build(root);
        try
        {
            await Assert.ThrowsAsync<System.CommandLine.Invocation.CommandLineException>(async () =>
            {
                await WindowsUpdateAndPackageManager.Commands.Cli.Run(new[] { "--help" }, services);
            });
        }
        finally
        {
            if (services is IDisposable d) d.Dispose();
        }
    }
}
