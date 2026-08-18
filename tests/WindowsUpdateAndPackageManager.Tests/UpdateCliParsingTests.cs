using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using WindowsUpdateAndPackageManager.Commands;
using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

public sealed class UpdateCliParsingTests
{
    private static RootCommand BuildRoot()
    {
        var services = new ServiceCollection();
        return Cli.BuildCommand(services.BuildServiceProvider());
    }

    [Fact]
    public void Update_command_is_registered()
    {
        var root = BuildRoot();
        var update = root.Subcommands.FirstOrDefault(c => c.Name.Equals("update", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(update);
    }

    [Fact]
    public void Update_push_command_is_registered()
    {
        var root = BuildRoot();
        var update = root.Subcommands.First(c => c.Name.Equals("update", StringComparison.OrdinalIgnoreCase));
        var push = update.Subcommands.FirstOrDefault(c => c.Name.Equals("push", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(push);
    }

    [Fact]
    public void Update_pull_command_is_registered()
    {
        var root = BuildRoot();
        var update = root.Subcommands.First(c => c.Name.Equals("update", StringComparison.OrdinalIgnoreCase));
        var pull = update.Subcommands.FirstOrDefault(c => c.Name.Equals("pull", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(pull);
    }

    [Fact]
    public void Update_push_command_has_expected_options()
    {
        var root = BuildRoot();
        var update = root.Subcommands.First(c => c.Name.Equals("update", StringComparison.OrdinalIgnoreCase));
        var push = update.Subcommands.First(c => c.Name.Equals("push", StringComparison.OrdinalIgnoreCase));

        var optionNames = push.Options.Select(o => o.Name).ToList();
        Assert.Contains("source", optionNames);
        Assert.Contains("id", optionNames);
        Assert.Contains("version", optionNames);
        Assert.Contains("for", optionNames);
        Assert.Contains("channel", optionNames);
        Assert.Contains("token", optionNames);
        Assert.Contains("build-number", optionNames);
        Assert.Contains("display-name", optionNames);
    }

    [Fact]
    public void Update_pull_command_has_expected_options()
    {
        var root = BuildRoot();
        var update = root.Subcommands.First(c => c.Name.Equals("update", StringComparison.OrdinalIgnoreCase));
        var pull = update.Subcommands.First(c => c.Name.Equals("pull", StringComparison.OrdinalIgnoreCase));

        var optionNames = pull.Options.Select(o => o.Name).ToList();
        Assert.Contains("repo", optionNames);
        Assert.Contains("for", optionNames);
        Assert.Contains("channel", optionNames);
        Assert.Contains("build-number", optionNames);
    }
}
