using System.IO;
using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

[Collection("Console")]
public sealed class DeltaVerifyIntegrationTests
{
    [Fact]
    public async Task DeltaVerify_command_reports_package_details_for_existing_file()
    {
        var services = WindowsUpdateAndPackageManager.Commands.Composition.Build(AppContext.BaseDirectory);
        try
        {
            var packagePath = Path.Combine(Path.GetTempPath(), "wupm-delta-verify-" + Guid.NewGuid().ToString("N") + ".txt");
            File.WriteAllText(packagePath, "wupm-package");

            var output = new StringWriter();
            var original = Console.Out;
            try
            {
                Console.SetOut(output);
                await WindowsUpdateAndPackageManager.Commands.Cli.Run(new[] { "delta-verify", "--path", packagePath }, services);
            }
            finally
            {
                Console.SetOut(original);
            }

            var text = output.ToString();
            Assert.Contains("Package:", text);
            Assert.Contains("Path:", text);
            Assert.Contains("SHA256:", text);
            Assert.Contains("Verification: passed", text);
        }
        finally
        {
            if (services is IDisposable d) d.Dispose();
        }
    }

    [Fact]
    public async Task DeltaVerify_command_reports_failure_when_path_missing()
    {
        var services = WindowsUpdateAndPackageManager.Commands.Composition.Build(AppContext.BaseDirectory);
        try
        {
            var output = new StringWriter();
            var original = Console.Out;
            try
            {
                Console.SetOut(output);
                await WindowsUpdateAndPackageManager.Commands.Cli.Run(new[] { "delta-verify", "--path", @"C:\missing\package.wupkg" }, services);
            }
            finally
            {
                Console.SetOut(original);
            }

            Assert.Contains("Package path could not be resolved.", output.ToString());
        }
        finally
        {
            if (services is IDisposable d) d.Dispose();
        }
    }
}
