using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using WindowsUpdateAndPackageManager.Commands;
using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

public sealed class DoctorCliTests
{
    [Fact]
    public async Task Doctor_command_prints_environment_and_connectivity_diagnostics()
    {
        var provider = new ServiceCollection().BuildServiceProvider();
        var output = new StringWriter();
        Console.SetOut(output);

        var args = new[] { "doctor" };
        var result = await Cli.Run(args, provider);
        Assert.Equal(0, result);

        var text = output.ToString();
        Assert.Contains("WUPM_API_KEY:", text);
        Assert.Contains("WUPM_API_MTLS_ENABLED:", text);
        Assert.Contains("WUPM_API_MTLS_ALLOWED_THUMBPRINTS:", text);
        Assert.Contains("GITHUB_TOKEN:", text);
        Assert.Contains("API connectivity:", text);
    }
}
