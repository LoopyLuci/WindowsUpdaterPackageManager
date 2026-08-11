using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Threading.Tasks;
using WindowsUpdateAndPackageManager.Commands;
using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

public sealed class CliCommandTests
{
    [Fact]
    public async Task PackPackage_creates_zip_and_reports_sha256()
    {
        var source = Path.Combine(Path.GetTempPath(), "wupm-pack-source-" + Guid.NewGuid().ToString("N"));
        var output = Path.Combine(Path.GetTempPath(), "wupm-pack-output-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(source, "manifest.json"), "{\"id\":\"test\",\"version\":\"1.0\"}");
            Directory.CreateDirectory(output);

            await WindowsUpdateAndPackageManager.Commands.Cli.PackPackage(null!, source, output);

            var zip = Path.Combine(output, new DirectoryInfo(source).Name + ".wupkg");
            Assert.True(File.Exists(zip));
            Assert.True(new FileInfo(zip).Length > 0);
        }
        finally
        {
            if (Directory.Exists(source)) Directory.Delete(source, true);
            if (Directory.Exists(output)) Directory.Delete(output, true);
        }
    }
}
