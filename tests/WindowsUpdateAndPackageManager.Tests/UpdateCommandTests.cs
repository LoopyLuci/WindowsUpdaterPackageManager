using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WindowsUpdateAndPackageManager.Core;
using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

public sealed class UpdateCommandTests
{
    [Fact]
    public async Task PushUpdateAsync_throws_when_package_missing()
    {
        var service = new UpdateDistributionService(null, new GitHubReleasePublisher(new HttpClient()), "owner", "repo");
        await Assert.ThrowsAsync<FileNotFoundException>(() => service.PushUpdateAsync("missing.zip", "pkg", "1.0.0", "10.0", "x64", "stable"));
    }

    [Fact]
    public async Task PushUpdateAsync_throws_when_windows_version_unsupported()
    {
        var service = new UpdateDistributionService(null, new GitHubReleasePublisher(new HttpClient()), "owner", "repo");
        var tempFile = Path.Combine(Path.GetTempPath(), $"wupm-test-{Guid.NewGuid():N}.zip");
        await File.WriteAllTextAsync(tempFile, "package");

        var ex = await Assert.ThrowsAsync<NotSupportedException>(() => service.PushUpdateAsync(tempFile, "pkg", "1.0.0", "6.1", "x64", "stable"));
        Assert.Contains("not supported", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UpdateManifest_defaults()
    {
        var manifest = new UpdateManifest("10.0", "x64", "pkg", "1.0.0", "sha", "url", DateTimeOffset.UtcNow);
        Assert.Equal("stable", manifest.Channel);
        Assert.Equal("stable", manifest.Channels[0]);
        Assert.Null(manifest.BuildNumber);
        Assert.Null(manifest.DisplayName);
    }

    [Fact]
    public void UpdateManifest_roundtrip()
    {
        var manifest = new UpdateManifest("11.0", "arm64", "pkg", "2.0.0", "sha2", "url2", DateTimeOffset.UtcNow, "beta", "Beta Package", "22621")
        {
            Channels = new List<string> { "beta", "release" }
        };

        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        var parsed = JsonSerializer.Deserialize<UpdateManifest>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(parsed);
        Assert.Equal("11.0", parsed!.WindowsVersion);
        Assert.Equal("arm64", parsed.Architecture);
        Assert.Equal("2.0.0", parsed.Version);
        Assert.Equal("beta", parsed.Channel);
        Assert.Equal("Beta Package", parsed.DisplayName);
        Assert.Equal("22621", parsed.BuildNumber);
        Assert.Contains("beta", parsed.Channels, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("release", parsed.Channels, StringComparer.OrdinalIgnoreCase);
    }
}
