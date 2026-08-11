using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

public class PublishCliTests
{
    [Fact]
    public async Task Publish_command_requires_zip_artifact()
    {
        var root = AppContext.BaseDirectory;
        var services = WindowsUpdateAndPackageManager.Commands.Composition.Build(root);
        try
        {
            var outputDir = Path.Combine(root, "publish");
            Directory.CreateDirectory(outputDir);
            File.WriteAllText(Path.Combine(outputDir, "dummy.zip"), "zip");

            var exitCode = await WindowsUpdateAndPackageManager.Commands.Cli.Run(new[] { "publish", "--tag", "v1.0.0-test" }, services);
            Assert.Equal(0, exitCode);
        }
        finally
        {
            if (services is IDisposable d) d.Dispose();
        }
    }
}
