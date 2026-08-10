using System.Net.Http;
using WindowsUpdateAndPackageManager.Models;

namespace WindowsUpdateAndPackageManager.Infrastructure;

public sealed class GitHubRepoClient : IRepoClient
{
    private static readonly HttpClient Http = new();

    public async Task<string> DownloadIndexAsync(string repositoryUrl, CancellationToken cancellationToken = default)
    {
        var indexUrl = $"{repositoryUrl.TrimEnd('/')}/releases/latest/download/index.json";
        return await Http.GetStringAsync(indexUrl, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Stream> DownloadPackageAsync(string packageUrl, CancellationToken cancellationToken = default)
    {
        var stream = await Http.GetStreamAsync(packageUrl, cancellationToken).ConfigureAwait(false);
        return stream;
    }

    public async Task<string> GetLatestReleaseAsync(string repositoryUrl, CancellationToken cancellationToken = default)
    {
        var releaseUrl = repositoryUrl.TrimEnd('/') + "/releases/latest";
        using var request = new HttpRequestMessage(HttpMethod.Get, releaseUrl);
        using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return releaseUrl;
    }
}
