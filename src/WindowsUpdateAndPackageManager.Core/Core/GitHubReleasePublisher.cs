using System.Net.Http.Headers;

namespace WindowsUpdateAndPackageManager.Core;

public sealed class GitHubReleasePublisher
{
    private readonly HttpClient _http;

    public GitHubReleasePublisher(HttpClient? http = null)
    {
        _http = http ?? new HttpClient();
    }

    public async Task<bool> PublishReleaseAsync(string owner, string repo, string tag, string zipPath, string? changelog = null, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.github.com/repos/{owner}/{repo}/releases");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("wupm", "1.0"));
        request.Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(new
        {
            tag_name = tag,
            name = tag,
            body = changelog ?? $"Release {tag}",
            draft = false,
            prerelease = false
        }));
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return false;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var uploadUrl = System.Text.Json.JsonDocument.Parse(body).RootElement.GetProperty("upload_url").GetString();
        if (string.IsNullOrWhiteSpace(uploadUrl)) return false;

        using var zipBytes = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
        using var asset = new HttpRequestMessage(HttpMethod.Post, $"{uploadUrl}?name={Path.GetFileName(zipPath)}");
        asset.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        asset.Headers.UserAgent.Add(new ProductInfoHeaderValue("wupm", "1.0"));
        asset.Content = new StreamContent(zipBytes);
        asset.Content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");

        using var assetResponse = await _http.SendAsync(asset, cancellationToken);
        return assetResponse.IsSuccessStatusCode;
    }
}
