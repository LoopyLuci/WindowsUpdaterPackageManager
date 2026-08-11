using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using WindowsUpdateAndPackageManager.Commands;
using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

public sealed class DoctorCliTests
{
    [Fact]
    public void Doctor_command_prints_environment_diagnostics()
    {
        var provider = new ServiceCollection().BuildServiceProvider();
        var output = new StringWriter();
        Console.SetOut(output);

        var args = new[] { "doctor" };
        var result = Cli.Run(args, provider).GetAwaiter().GetResult();
        Assert.Equal(0, result);

        var text = output.ToString();
        Assert.Contains("WUPM_API_KEY:", text);
        Assert.Contains("WUPM_API_MTLS_ENABLED:", text);
        Assert.Contains("WUPM_API_MTLS_ALLOWED_THUMBPRINTS:", text);
        Assert.Contains("GITHUB_TOKEN:", text);
    }
}
