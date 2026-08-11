using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

[Collection("Console")]
public class PublishCliTests
{
    [Fact]
    public async Task Publish_command_dry_run_prints_planned_artifacts()
    {
        var services = WindowsUpdateAndPackageManager.Commands.Composition.Build(AppContext.BaseDirectory);
        try
        {
            var artifact = Path.Combine(Environment.CurrentDirectory, "wupm-cli.zip");
            File.WriteAllText(artifact, "zip");

            var output = new StringWriter();
            var original = Console.Out;
            try
            {
                Console.SetOut(output);
                await WindowsUpdateAndPackageManager.Commands.Cli.Run(new[] { "publish", "--tag", "v1.0.0-test", "--dry-run" }, services);
            }
            finally
            {
                Console.SetOut(original);
            }

            Assert.Contains("Dry run completed. No release was created.", output.ToString());
            Assert.Contains("Artifacts to upload:", output.ToString());
            Assert.Contains("Repository:", output.ToString());
        }
        finally
        {
            if (File.Exists(Path.Combine(Environment.CurrentDirectory, "wupm-cli.zip"))) File.Delete(Path.Combine(Environment.CurrentDirectory, "wupm-cli.zip"));
            if (services is IDisposable d) d.Dispose();
        }
    }
}
