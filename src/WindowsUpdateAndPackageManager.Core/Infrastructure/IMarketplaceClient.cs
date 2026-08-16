using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace WindowsUpdateAndPackageManager.Infrastructure;

public interface IMarketplaceClient
{
    Task<IReadOnlyList<MarketplacePlugin>> SearchAsync(string query, CancellationToken cancellationToken = default);
}

public sealed class CompositeMarketplaceClient : IMarketplaceClient
{
    private readonly IMarketplaceClient _primary;
    private readonly IMarketplaceClient _local;

    public CompositeMarketplaceClient(IMarketplaceClient primary, IMarketplaceClient local)
    {
        _primary = primary;
        _local = local;
    }

    public async Task<IReadOnlyList<MarketplacePlugin>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        try
        {
            var primary = await _primary.SearchAsync(query, cancellationToken).ConfigureAwait(false);
            if (primary.Count > 0)
                return primary;
        }
        catch
        {
            // fallback to local on primary failure
        }

        return await _local.SearchAsync(query, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class LocalMarketplaceClient : IMarketplaceClient
{
    private readonly string _marketplaceRoot;

    public LocalMarketplaceClient(string rootPath)
    {
        _marketplaceRoot = Path.Combine(rootPath, ".wupm", "marketplace");
    }

    public Task<IReadOnlyList<MarketplacePlugin>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var results = new List<MarketplacePlugin>();
        if (!Directory.Exists(_marketplaceRoot))
            return Task.FromResult<IReadOnlyList<MarketplacePlugin>>(results);

        foreach (var file in Directory.EnumerateFiles(_marketplaceRoot, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var json = File.ReadAllText(file);
                var node = JsonNode.Parse(json);
                if (node is null) continue;

                var id = node["id"]?.ToString() ?? Path.GetFileNameWithoutExtension(file);
                var displayName = node["displayName"]?.ToString() ?? id;
                var version = node["version"]?.ToString() ?? "0.0.0";
                var deps = node["dependencies"]?.ToString();
                var dl = node["downloadCount"]?.GetValue<int?>();

                if (!string.IsNullOrWhiteSpace(query))
                {
                    var q = query.Trim();
                    if (!id.Contains(q, StringComparison.OrdinalIgnoreCase) &&
                        !displayName.Contains(q, StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                results.Add(new MarketplacePlugin
                {
                    Id = id,
                    DisplayName = displayName,
                    Version = version,
                    Dependencies = deps,
                    DownloadCount = dl
                });
            }
            catch
            {
                // skip bad manifests
            }
        }

        return Task.FromResult<IReadOnlyList<MarketplacePlugin>>(results);
    }
}

public sealed class MarketplacePlugin
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string? Dependencies { get; set; }
    public int? DownloadCount { get; set; }
}

public sealed class GitHubMarketplaceClient : IMarketplaceClient, IAsyncDisposable
{
    private readonly HttpClient _http;
    private readonly string _ownerRepo;
    private readonly string? _token;

    public GitHubMarketplaceClient(HttpClient http, string marketplaceRepositoryUrl, string? token = null)
    {
        _http = http;
        _token = token;
        _ownerRepo = ParseOwnerRepo(marketplaceRepositoryUrl);
    }

    public async Task<IReadOnlyList<MarketplacePlugin>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var url = $"https://api.github.com/repos/{_ownerRepo}/releases?per_page=50";
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

        var results = new List<MarketplacePlugin>();
        var effective = string.IsNullOrWhiteSpace(query) ? string.Empty : query.Trim();

        foreach (var release in doc.RootElement.EnumerateArray())
        {
            var tag = release.GetProperty("tag_name").GetString();
            if (string.IsNullOrWhiteSpace(tag))
            {
                continue;
            }

            foreach (var asset in release.GetProperty("assets").EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString() ?? string.Empty;
                if (!name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var id = Path.GetFileNameWithoutExtension(name);
                if (!string.IsNullOrWhiteSpace(effective) &&
                    !(id.Contains(effective, StringComparison.OrdinalIgnoreCase) ||
                      name.Contains(effective, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                results.Add(new MarketplacePlugin
                {
                    Id = id,
                    DisplayName = name,
                    Version = tag.TrimStart('v'),
                    Dependencies = ParseDependencies(release),
                    DownloadCount = asset.GetProperty("download_count").GetInt32()
                });
            }
        }

        return results;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
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

    private static string? ParseDependencies(JsonElement release)
    {
        if (!release.TryGetProperty("body", out var body) || body.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = body.GetString() ?? string.Empty;
        var marker = "dependencies:";
        var idx = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return null;
        }

        var after = text[(idx + marker.Length)..].Trim();
        var end = after.IndexOf('\n');
        var deps = end >= 0 ? after[..end].Trim() : after.Trim();
        return string.IsNullOrWhiteSpace(deps) ? null : deps;
    }
}
