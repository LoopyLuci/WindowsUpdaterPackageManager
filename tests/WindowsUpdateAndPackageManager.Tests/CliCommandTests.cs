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
            var delta = Path.Combine(output, new DirectoryInfo(source).Name + ".delta.json");
            Assert.True(File.Exists(zip));
            Assert.True(new FileInfo(zip).Length > 0);
            Assert.True(File.Exists(delta));
            Assert.Contains("\"Sha256\":", File.ReadAllText(delta));
            var json = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(delta));
            var root = json.RootElement;
            Assert.Equal(new DirectoryInfo(source).Name, root.GetProperty("Id").GetString());
            Assert.Equal(new DirectoryInfo(source).Name, root.GetProperty("Version").GetString());
            Assert.Equal(64, root.GetProperty("Sha256").GetString()!.Length);
        }
        finally
        {
            if (Directory.Exists(source)) Directory.Delete(source, true);
            if (Directory.Exists(output)) Directory.Delete(output, true);
        }
    }

    [Fact]
    public async Task PackPackage_delta_manifest_contains_required_fields()
    {
        var source = Path.Combine(Path.GetTempPath(), "wupm-pack-source-" + Guid.NewGuid().ToString("N"));
        var output = Path.Combine(Path.GetTempPath(), "wupm-pack-output-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(source, "manifest.json"), "{\"id\":\"test\",\"version\":\"1.0\"}");
            Directory.CreateDirectory(output);

            await WindowsUpdateAndPackageManager.Commands.Cli.PackPackage(null!, source, output);

            var delta = Path.Combine(output, new DirectoryInfo(source).Name + ".delta.json");
            var json = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(delta));
            var root = json.RootElement;
            Assert.Equal(new DirectoryInfo(source).Name, root.GetProperty("Id").GetString());
            Assert.Equal(new DirectoryInfo(source).Name, root.GetProperty("Version").GetString());
            Assert.Equal(64, root.GetProperty("Sha256").GetString()!.Length);
        }
        finally
        {
            if (Directory.Exists(source)) Directory.Delete(source, true);
            if (Directory.Exists(output)) Directory.Delete(output, true);
        }
    }
}
