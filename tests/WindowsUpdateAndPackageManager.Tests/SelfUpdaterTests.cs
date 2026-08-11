using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using WindowsUpdateAndPackageManager.Core;
using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

public sealed class SelfUpdaterTests
{
    [Fact]
    public async Task SelfUpdateAsync_returns_true_and_launches_updater()
    {
        string? capturedPs1 = null;
        var capturedArgs = new List<string>();
        Func<ProcessStartInfo, Task<bool>> fakeProcessStart = psi =>
        {
            capturedPs1 = psi.FileName;
            foreach (var arg in psi.ArgumentList)
            {
                capturedArgs.Add(arg);
            }
            return Task.FromResult(true);
        };

        var tempRoot = Path.Combine(Path.GetTempPath(), "wupm-selfupdate-tests");
        Directory.CreateDirectory(tempRoot);
        var zipPath = Path.Combine(tempRoot, $"wupm-cli-{Guid.NewGuid():N}.zip");

        await using (var fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Create, true))
        {
            var entry = zip.CreateEntry("Wupm.Cli.exe");
            await using var entryStream = entry.Open();
            using var writer = new BinaryWriter(entryStream);
            writer.Write(Encoding.ASCII.GetBytes("MZFAKEEXE"));
        }

        var updater = new SelfUpdater(
            currentExePath: Path.Combine(tempRoot, "wupm-test.exe"),
            githubOwner: "owner",
            githubRepo: "repo",
            assetName: "wupm-cli.zip",
            processStartAsync: fakeProcessStart,
            fetchReleaseAsync: (_, _) => Task.FromResult<string?>("{\"assets\":[{\"name\":\"wupm-cli.zip\",\"browser_download_url\":\"https://example.invalid/wupm-cli.zip\"}]}"),
            downloadAssetAsync: (releaseJson, destinationPath, _) =>
            {
                File.Copy(zipPath, destinationPath, true);
                return Task.CompletedTask;
            });

        var result = await updater.SelfUpdateAsync();
        Assert.True(result);
        Assert.NotNull(capturedPs1);
        Assert.True(capturedPs1.IndexOf("powershell", StringComparison.OrdinalIgnoreCase) >= 0);
        Assert.True(capturedArgs.Contains("-File"));
        Assert.True(capturedArgs.Any(a => a.EndsWith("apply-update.ps1", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task SelfUpdateAsync_returns_false_when_no_executable_in_zip()
    {
        var updater = new SelfUpdater(
            currentExePath: Path.Combine(Path.GetTempPath(), "wupm-test.exe"),
            githubOwner: "owner",
            githubRepo: "repo",
            assetName: "wupm-cli.zip",
            fetchReleaseAsync: (_, _) => Task.FromResult<string?>("{\"assets\":[{\"name\":\"wupm-cli.zip\",\"browser_download_url\":\"https://example.invalid/wupm-cli.zip\"}]}"),
            downloadAssetAsync: (releaseJson, destinationPath, _) =>
            {
                using var ms = new MemoryStream();
                using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
                {
                    var entry = zip.CreateEntry("readme.txt");
                    using var entryStream = entry.Open();
                    using var writer = new BinaryWriter(entryStream);
                    writer.Write(Encoding.ASCII.GetBytes("hello"));
                }
                ms.Position = 0;
                return File.WriteAllBytesAsync(destinationPath, ms.ToArray(), default);
            });

        var result = await updater.SelfUpdateAsync();
        Assert.False(result);
    }
}
