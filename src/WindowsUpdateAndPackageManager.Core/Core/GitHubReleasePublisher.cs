using System.Net.Http.Headers;
using System.Text.Json;

namespace WindowsUpdateAndPackageManager.Core;

public sealed class GitHubReleasePublisher
{
    private readonly HttpClient _http;

    public GitHubReleasePublisher(HttpClient? http = null)
    {
        _http = http ?? new HttpClient();
    }

    public async Task<bool> PublishReleaseAsync(string owner, string repo, string tag, string zipPath, string? changelog = null, string? token = null, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.github.com/repos/{owner}/{repo}/releases");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("wupm", "1.0"));
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        request.Content = new StringContent(JsonSerializer.Serialize(new
        {
            tag_name = tag,
            name = tag,
            body = changelog ?? $"Release {tag}",
            draft = false,
            prerelease = false
        }));
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return false;

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var uploadUrl = JsonDocument.Parse(body).RootElement.GetProperty("upload_url").GetString();
        if (string.IsNullOrWhiteSpace(uploadUrl)) return false;

        using var zipBytes = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
        using var asset = new HttpRequestMessage(HttpMethod.Post, $"{uploadUrl}?name={Path.GetFileName(zipPath)}");
        asset.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        asset.Headers.UserAgent.Add(new ProductInfoHeaderValue("wupm", "1.0"));
        if (!string.IsNullOrWhiteSpace(token))
        {
            asset.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        asset.Content = new StreamContent(zipBytes);
        asset.Content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");

        using var assetResponse = await _http.SendAsync(asset, cancellationToken).ConfigureAwait(false);
        return assetResponse.IsSuccessStatusCode;
    }

    public async Task<GitHubRelease?> CreateReleaseAsync(string owner, string repo, string tag, string title, string? token = null, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.github.com/repos/{owner}/{repo}/releases");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("wupm", "1.0"));
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        request.Content = new StringContent(JsonSerializer.Serialize(new
        {
            tag_name = tag,
            name = title,
            body = title,
            draft = false,
            prerelease = false
        }));
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        return new GitHubRelease
        {
            Id = root.GetProperty("id").GetInt64(),
            HtmlUrl = root.GetProperty("html_url").GetString() ?? string.Empty
        };
    }

    public async Task<GitHubAsset?> UploadAssetAsync(string owner, string repo, long releaseId, string filePath, string? token = null, CancellationToken cancellationToken = default)
    {
        var uploadUrl = $"https://uploads.github.com/repos/{owner}/{repo}/releases/{releaseId}/assets?name={Uri.EscapeDataString(Path.GetFileName(filePath))}";
        using var request = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("wupm", "1.0"));
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
        request.Content = new StreamContent(stream);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        return new GitHubAsset
        {
            BrowserDownloadUrl = root.GetProperty("browser_download_url").GetString() ?? string.Empty
        };
    }
}

public sealed class GitHubRelease
{
    public long Id { get; set; }
    public string HtmlUrl { get; set; } = string.Empty;
}

public sealed class GitHubAsset
{
    public string BrowserDownloadUrl { get; set; } = string.Empty;
}