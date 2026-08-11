using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

public sealed class GitHubReleasePublisherTests
{
    [Fact]
    public async Task CreateReleaseAsync_returns_release_when_api_succeeds()
    {
        var handler = new FakeHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Post && request.RequestUri!.AbsoluteUri.Contains("/releases", StringComparison.OrdinalIgnoreCase))
            {
                var body = JsonSerializer.Serialize(new { id = 1L, html_url = "https://github.com/owner/repo/releases/1" });
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest));
        });
        using var http = new HttpClient(handler);
        var publisher = new WindowsUpdateAndPackageManager.Core.GitHubReleasePublisher(http);
        var result = await publisher.CreateReleaseAsync("owner", "repo", "v1.0.0", "title");
        Assert.NotNull(result);
        Assert.Equal(1L, result!.Id);
        Assert.Equal("https://github.com/owner/repo/releases/1", result.HtmlUrl);
    }

    [Fact]
    public async Task UploadAssetAsync_returns_asset_when_api_succeeds()
    {
        var handler = new FakeHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Post && request.RequestUri!.AbsoluteUri.Contains("/assets", StringComparison.OrdinalIgnoreCase))
            {
                var body = JsonSerializer.Serialize(new { browser_download_url = "https://github.com/owner/repo/releases/download/v1.0.0/asset.zip" });
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest));
        });
        using var http = new HttpClient(handler);
        var publisher = new WindowsUpdateAndPackageManager.Core.GitHubReleasePublisher(http);
        var result = await publisher.UploadAssetAsync("owner", "repo", 1, Path.GetTempFileName());
        Assert.NotNull(result);
        Assert.Equal("https://github.com/owner/repo/releases/download/v1.0.0/asset.zip", result!.BrowserDownloadUrl);
    }

    [Fact]
    public async Task PublishReleaseAsync_returns_false_when_api_returns_failure()
    {
        var handler = new FakeHttpMessageHandler(request =>
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest));
        });
        using var http = new HttpClient(handler);
        var publisher = new WindowsUpdateAndPackageManager.Core.GitHubReleasePublisher(http);
        var result = await publisher.PublishReleaseAsync("owner", "repo", "v1.0.0", Path.GetTempFileName());
        Assert.False(result);
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

        public FakeHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _handler(request);
        }
    }
}
