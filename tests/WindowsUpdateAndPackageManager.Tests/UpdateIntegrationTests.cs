using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WindowsUpdateAndPackageManager.Core;
using WindowsUpdateAndPackageManager.Infrastructure;
using WindowsUpdateAndPackageManager.Models;
using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

public sealed class FakeRepoClient : IRepoClient
{
    private readonly string _releaseJson;

    public FakeRepoClient(string releaseJson)
    {
        _releaseJson = releaseJson;
    }

    public Task<string> DownloadIndexAsync(string repositoryUrl, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<Stream> DownloadPackageAsync(string packageUrl, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetLatestReleaseAsync(string repositoryUrl, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_releaseJson);
    }
}

public sealed class UpdateIntegrationTests
{
    [Fact]
    public async Task PullUpdatesAsync_returns_empty_when_no_repo_client()
    {
        var service = new UpdateDistributionService(null, new GitHubReleasePublisher(new HttpClient()), "owner", "repo");
        var updates = await service.PullUpdatesAsync("10.0");
        Assert.Empty(updates);
    }

    [Fact]
    public async Task PullUpdatesAsync_returns_empty_when_body_empty()
    {
        var repoClient = new FakeRepoClient(JsonSerializer.Serialize(new { body = string.Empty }));
        var service = new UpdateDistributionService(repoClient, new GitHubReleasePublisher(new HttpClient()), "owner", "repo");
        var updates = await service.PullUpdatesAsync("10.0");
        Assert.Empty(updates);
    }

    [Fact]
    public async Task PullUpdatesAsync_returns_empty_when_body_not_json()
    {
        var repoClient = new FakeRepoClient(JsonSerializer.Serialize(new { body = "not-json" }));
        var service = new UpdateDistributionService(repoClient, new GitHubReleasePublisher(new HttpClient()), "owner", "repo");
        var updates = await service.PullUpdatesAsync("10.0");
        Assert.Empty(updates);
    }
}
