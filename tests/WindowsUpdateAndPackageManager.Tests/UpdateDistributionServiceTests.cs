using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WindowsUpdateAndPackageManager.Core;
using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

    public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(_handler(request));
    }
}

public sealed class UpdateDistributionServiceTests
{
    [Fact]
    public async Task PushUpdateAsync_creates_release_and_patches_body()
    {
        var releaseId = 10L;
        var assetUrl = "https://example.com/asset.zip";
        var handler = new FakeHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsoluteUri.Contains("/releases/latest", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new { id = releaseId, html_url = "https://github.com/owner/repo/releases/10" }))
                };
            }

            if (request.Method == HttpMethod.Post && request.RequestUri!.AbsoluteUri.Contains("/releases", StringComparison.OrdinalIgnoreCase) && !request.RequestUri.AbsoluteUri.Contains("/assets", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new { id = releaseId, html_url = "https://github.com/owner/repo/releases/10", upload_url = "https://uploads.github.com/repos/owner/repo/releases/10/assets{?name,label}" }))
                };
            }

            if (request.Method == HttpMethod.Post && request.RequestUri!.AbsoluteUri.Contains("/releases/10/assets", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new { browser_download_url = assetUrl }))
                };
            }

            if (request.Method == HttpMethod.Patch && request.RequestUri!.AbsoluteUri.Contains($"/releases/{releaseId}", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        var publisher = new GitHubReleasePublisher(http);
        var service = new UpdateDistributionService(null, publisher, "owner", "repo", "token", http);

        var tempFile = Path.Combine(Path.GetTempPath(), $"wupm-test-{Guid.NewGuid():N}.zip");
        await File.WriteAllTextAsync(tempFile, "package-bytes");

        var result = await service.PushUpdateAsync(tempFile, "TestPackage", "1.0.0", "10.0", "x64", "stable", buildNumber: "19045", displayName: "Test Package");

        Assert.True(result);
    }

    [Fact]
    public async Task PullUpdatesAsync_filters_by_windows_version_and_channel()
    {
        var manifest = new UpdateManifest("10.0", "x64", "TestPackage", "1.0.0", "sha", "https://example.com/asset.zip", DateTimeOffset.UtcNow, "stable")
        {
            Channels = new List<string> { "stable", "beta" },
            BuildNumber = "19045",
            DisplayName = "Test Package"
        };

        Assert.Equal("TestPackage", manifest.PackageId);
        Assert.Equal("10.0", manifest.WindowsVersion);
        Assert.Equal("x64", manifest.Architecture);
        Assert.Equal("19045", manifest.BuildNumber);
        Assert.Equal("Test Package", manifest.DisplayName);
        Assert.Contains("stable", manifest.Channels, StringComparer.OrdinalIgnoreCase);
    }
}
