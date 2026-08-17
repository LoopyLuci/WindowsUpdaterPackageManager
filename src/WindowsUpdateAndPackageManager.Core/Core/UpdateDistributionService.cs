using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using WindowsUpdateAndPackageManager.Infrastructure;

namespace WindowsUpdateAndPackageManager.Core;

public sealed class UpdateDistributionService
{
    private readonly IRepoClient _repoClient;
    private readonly GitHubReleasePublisher _publisher;
    private readonly string _owner;
    private readonly string _repo;
    private readonly string? _token;
    private readonly HttpClient _http;

    public UpdateDistributionService(IRepoClient repoClient, GitHubReleasePublisher publisher, string owner, string repo, string? token = null)
    {
        _repoClient = repoClient;
        _publisher = publisher;
        _owner = owner;
        _repo = repo;
        _token = token;
        _http = new HttpClient();
    }

    public async Task<bool> PushUpdateAsync(string packagePath, string packageId, string version, string windowsVersion, string architecture, string channel, string? buildNumber = null, bool isDriver = false, string? displayName = null, string? changelog = null, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(packagePath))
        {
            throw new FileNotFoundException("Package file is missing.", packagePath);
        }

        await using var stream = File.OpenRead(packagePath);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
        var sha256 = Convert.ToHexString(hash).ToLowerInvariant();

        var tag = $"updates/{packageId}/{version}";
        var title = $"{packageId} {version} for {windowsVersion} {architecture} ({channel})";
        var release = await _publisher.CreateReleaseAsync(_owner, _repo, tag, title, _token, cancellationToken).ConfigureAwait(false);
        if (release is null)
        {
            return false;
        }

        var asset = await _publisher.UploadAssetAsync(_owner, _repo, release.Id, packagePath, _token, cancellationToken).ConfigureAwait(false);
        if (asset is null)
        {
            return false;
        }

        var manifest = new UpdateManifest(windowsVersion, architecture, packageId, version, sha256, asset.BrowserDownloadUrl, DateTimeOffset.UtcNow, channel)
        {
            Channels = new List<string> { channel }
        };

        var body = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        var releaseUpdate = new
        {
            body
        };

        using var update = new HttpRequestMessage(HttpMethod.Patch, $"https://api.github.com/repos/{_owner}/{_repo}/releases/{release.Id}");
        update.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        update.Headers.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("wupm", "1.0"));
        if (!string.IsNullOrWhiteSpace(_token))
        {
            update.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
        }
        update.Content = new StringContent(JsonSerializer.Serialize(releaseUpdate), System.Text.Encoding.UTF8, "application/json");
        using var response = await _http.SendAsync(update, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return true;
    }

    public async Task<IReadOnlyList<UpdateManifest>> PullUpdatesAsync(string? windowsVersion = null, string? architecture = null, string? channel = null, string? buildNumber = null, CancellationToken cancellationToken = default)
    {
        var raw = await _repoClient.GetLatestReleaseAsync(string.Empty, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Array.Empty<UpdateManifest>();
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(raw);
        }
        catch
        {
            return Array.Empty<UpdateManifest>();
        }

        if (node is not JsonObject root || !root.TryGetPropertyValue("body", out var bodyNode) || bodyNode is not JsonValue)
        {
            return Array.Empty<UpdateManifest>();
        }

        var body = bodyNode.AsValue().GetValue<string>();
        if (string.IsNullOrWhiteSpace(body))
        {
            return Array.Empty<UpdateManifest>();
        }

        var manifest = TryParseManifest(body);
        if (manifest is null)
        {
            return Array.Empty<UpdateManifest>();
        }

        if (!string.IsNullOrWhiteSpace(windowsVersion) && !string.Equals(manifest.WindowsVersion, windowsVersion, StringComparison.OrdinalIgnoreCase))
        {
            return Array.Empty<UpdateManifest>();
        }

        if (!string.IsNullOrWhiteSpace(architecture) && !string.Equals(manifest.Architecture, architecture, StringComparison.OrdinalIgnoreCase))
        {
            return Array.Empty<UpdateManifest>();
        }

        if (!string.IsNullOrWhiteSpace(channel) && !manifest.Channels.Contains(channel, StringComparer.OrdinalIgnoreCase))
        {
            return Array.Empty<UpdateManifest>();
        }

        return new List<UpdateManifest> { manifest };
    }

    private static UpdateManifest? TryParseManifest(string body)
    {
        try
        {
            var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            return new UpdateManifest(
                root.GetProperty("windowsVersion").GetString() ?? string.Empty,
                root.GetProperty("architecture").GetString() ?? "x64",
                root.GetProperty("packageId").GetString() ?? string.Empty,
                root.GetProperty("version").GetString() ?? string.Empty,
                root.GetProperty("sha256").GetString() ?? string.Empty,
                root.GetProperty("sourceUrl").GetString() ?? string.Empty,
                root.GetProperty("publishedAt").GetDateTimeOffset())
            {
                Channels = root.TryGetProperty("channels", out var channels) && channels.ValueKind == JsonValueKind.Array
                    ? channels.EnumerateArray().Select(x => x.GetString() ?? "stable").Where(x => !string.IsNullOrWhiteSpace(x)).ToList()
                    : new List<string> { root.GetProperty("channel").GetString() ?? "stable" },
                Channel = root.TryGetProperty("channel", out var channel) ? channel.GetString() ?? "stable" : "stable"
            };
        }
        catch
        {
            return null;
        }
    }
}
