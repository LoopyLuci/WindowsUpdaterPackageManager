using System.Net.Http.Headers;
using System.Text.Json;
using WindowsUpdateAndPackageManager.Models;

namespace WindowsUpdateAndPackageManager.Infrastructure;

public sealed class GitHubRepoClient : IRepoClient
{
    private readonly HttpClient _http;
    private readonly string _ownerRepo;
    private readonly string? _token;

    public GitHubRepoClient(HttpClient http, string repositoryUrl, string? token = null)
    {
        _http = http;
        _token = token;
        _ownerRepo = ParseOwnerRepo(repositoryUrl);
    }

    public async Task<string> DownloadIndexAsync(string repositoryUrl, CancellationToken cancellationToken = default)
    {
        var ownerRepo = string.IsNullOrWhiteSpace(repositoryUrl) ? _ownerRepo : ParseOwnerRepo(repositoryUrl);
        var url = $"https://api.github.com/repos/{ownerRepo}/releases/latest";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("WUPM", "1.0"));
        if (!string.IsNullOrWhiteSpace(_token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        }

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = doc.RootElement;

        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            if (asset.GetProperty("name").GetString() == "index.json")
            {
                return await _http.GetStringAsync(asset.GetProperty("browser_download_url").GetString()!, cancellationToken).ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException("Release asset 'index.json' was not found.");
    }

    public async Task<Stream> DownloadPackageAsync(string packageUrl, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, packageUrl);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("WUPM", "1.0"));
        if (!string.IsNullOrWhiteSpace(_token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        }

        var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> GetLatestReleaseAsync(string repositoryUrl, CancellationToken cancellationToken = default)
    {
        var ownerRepo = string.IsNullOrWhiteSpace(repositoryUrl) ? _ownerRepo : ParseOwnerRepo(repositoryUrl);
        var url = $"https://api.github.com/repos/{ownerRepo}/releases/latest";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("WUPM", "1.0"));
        if (!string.IsNullOrWhiteSpace(_token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        }

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string ParseOwnerRepo(string repositoryUrl)
    {
        if (string.IsNullOrWhiteSpace(repositoryUrl)) throw new ArgumentException("Repository URL is required.", nameof(repositoryUrl));
        var trimmed = repositoryUrl.Trim().TrimEnd('/');
        if (trimmed.Contains("github.com/", StringComparison.OrdinalIgnoreCase))
        {
            var idx = trimmed.IndexOf("github.com/", StringComparison.OrdinalIgnoreCase);
            trimmed = trimmed[(idx + "github.com/".Length)..];
        }

        trimmed = trimmed.Trim('/');
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Split('/').Length < 2)
        {
            throw new ArgumentException("Repository URL must be in the form 'https://github.com/owner/repo'.", nameof(repositoryUrl));
        }

        return trimmed;
    }
}
