using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using WindowsUpdateAndPackageManager.Infrastructure;
using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

public sealed class GitHubRepoClientTests
{
    [Fact]
    public async Task DownloadIndexAsync_fetches_index_from_latest_release_asset()
    {
        var releaseJson = JsonSerializer.Serialize(new
        {
            tag_name = "v1.0",
            assets = new[]
            {
                new { name = "index.json", browser_download_url = "https://example.com/index.json" }
            }
        });

        var indexJson = "{\"schemaVersion\":\"1.0\",\"packages\":[]}";
        var responses = new Queue<HttpResponseMessage>(new[]
        {
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(releaseJson, Encoding.UTF8, "application/json") },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(indexJson, Encoding.UTF8, "application/json") }
        });

        var handler = new MockHttpHandler(_ => responses.Dequeue());
        var httpClient = new HttpClient(handler);
        var client = new GitHubRepoClient(httpClient, "https://github.com/owner/repo");

        var result = await client.DownloadIndexAsync("https://github.com/owner/repo");

        Assert.Contains("schemaVersion", result);
    }

    [Fact]
    public async Task DownloadPackageAsync_sends_user_agent_and_returns_stream()
    {
        var streamContent = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes("PK")));
        var handler = new MockHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = streamContent });
        var httpClient = new HttpClient(handler);
        var client = new GitHubRepoClient(httpClient, "https://github.com/owner/repo");

        await using var result = await client.DownloadPackageAsync("https://example.com/package.zip");

        using var reader = new StreamReader(result);
        var content = await reader.ReadToEndAsync();
        Assert.Equal("PK", content);
    }

    [Fact]
    public async Task GetLatestReleaseAsync_returns_release_json()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"tag_name\":\"v1.0\"}", Encoding.UTF8, "application/json")
        };
        var handler = new MockHttpHandler(_ => response);
        var httpClient = new HttpClient(handler);
        var client = new GitHubRepoClient(httpClient, "https://github.com/owner/repo");

        var result = await client.GetLatestReleaseAsync("https://github.com/owner/repo");

        Assert.Contains("v1.0", result);
    }

    private sealed class MockHttpHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public MockHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = _respond(request);
            return Task.FromResult(response);
        }
    }
}
