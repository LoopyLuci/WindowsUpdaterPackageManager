using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using WindowsUpdateAndPackageManager.Commands;
using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

public sealed class UpdateInitTests
{
    [Fact]
    public void Update_init_command_is_registered()
    {
        var services = new ServiceCollection();
        var root = Cli.BuildCommand(services.BuildServiceProvider());
        var update = root.Subcommands.First(c => c.Name.Equals("update", StringComparison.OrdinalIgnoreCase));
        var init = update.Subcommands.FirstOrDefault(c => c.Name.Equals("init", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(init);
    }

    [Fact]
    public void Update_init_command_has_expected_options()
    {
        var services = new ServiceCollection();
        var root = Cli.BuildCommand(services.BuildServiceProvider());
        var update = root.Subcommands.First(c => c.Name.Equals("update", StringComparison.OrdinalIgnoreCase));
        var init = update.Subcommands.First(c => c.Name.Equals("init", StringComparison.OrdinalIgnoreCase));

        var optionNames = init.Options.Select(o => o.Name).ToList();
        Assert.Contains("source", optionNames);
        Assert.Contains("id", optionNames);
        Assert.Contains("version", optionNames);
        Assert.Contains("for", optionNames);
        Assert.Contains("channel", optionNames);
        Assert.Contains("build-number", optionNames);
        Assert.Contains("display-name", optionNames);
    }
}
