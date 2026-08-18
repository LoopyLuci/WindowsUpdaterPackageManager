using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WindowsUpdateAndPackageManager.Core;
using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

public sealed class SelfUpdaterE2ETests
{
    [Fact]
    public async Task SelfUpdateAsync_returns_true_with_valid_release_and_asset()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"wupm-selfupdate-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        var fakeExe = Path.Combine(tempRoot, "wupm.exe");
        await File.WriteAllTextAsync(fakeExe, "fake-exe");

        var zipPath = Path.Combine(tempRoot, "wupm-cli.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry("wupm.exe");
            await using var stream = entry.Open();
            await stream.WriteAsync(Encoding.UTF8.GetBytes("new-exe"));
        }

        var updater = new SelfUpdater(
            currentExePath: fakeExe,
            githubOwner: "owner",
            githubRepo: "repo",
            assetName: "wupm-cli.zip",
            fetchReleaseAsync: (_, _) => Task.FromResult<string?>(JsonSerializer.Serialize(new
            {
                tag_name = "v2.0.0",
                assets = new[]
                {
                    new { name = "wupm-cli.zip", browser_download_url = "file:///" + zipPath.Replace('\\', '/') }
                }
            })),
            downloadAssetAsync: (_, destination, _) =>
            {
                File.Copy(zipPath, destination, overwrite: true);
                return Task.CompletedTask;
            },
            processStartAsync: _ => Task.FromResult(true));

        var result = await updater.SelfUpdateAsync();

        Assert.True(result);

        try { Directory.Delete(tempRoot, recursive: true); } catch { }
    }
}
