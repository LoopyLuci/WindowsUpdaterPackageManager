using System.Text.Json;

namespace WindowsUpdateAndPackageManager.Infrastructure;

public sealed class FileMarketplaceSearchCache : IMarketplaceSearchCache
{
    private readonly string _cacheRoot;
    private TimeSpan _ttl = TimeSpan.FromHours(1);

    public FileMarketplaceSearchCache(string cacheRoot)
    {
        _cacheRoot = cacheRoot;
        Directory.CreateDirectory(_cacheRoot);
    }

    public async Task<IReadOnlyList<MarketplacePlugin>> GetAsync(string query, CancellationToken cancellationToken = default)
    {
        var path = GetPath(query);
        var meta = GetMetaPath(query);
        if (!File.Exists(path) || !File.Exists(meta))
        {
            return Array.Empty<MarketplacePlugin>();
        }

        var written = File.GetLastWriteTimeUtc(meta);
        if (DateTime.UtcNow - written > _ttl)
        {
            return Array.Empty<MarketplacePlugin>();
        }

        await using var stream = File.OpenRead(path);
        var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var list = new List<MarketplacePlugin>();
        foreach (var element in doc.RootElement.EnumerateArray())
        {
            list.Add(element.Deserialize<MarketplacePlugin>()!);
        }
        return list;
    }

    public async Task SetAsync(string query, IReadOnlyList<MarketplacePlugin> results, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        var path = GetPath(query);
        var meta = GetMetaPath(query);
        var json = JsonSerializer.Serialize(results);
        await File.WriteAllTextAsync(path, json, cancellationToken).ConfigureAwait(false);
        File.WriteAllText(meta, DateTime.UtcNow.ToString("o"));
        if (ttl.HasValue)
        {
            _ttl = ttl.Value;
        }
    }

    private string GetPath(string query)
    {
        var safe = string.Join("_", query.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_cacheRoot, $"marketplace-search-{safe}.json");
    }

    private string GetMetaPath(string query)
    {
        var safe = string.Join("_", query.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_cacheRoot, $"marketplace-search-{safe}.meta");
    }
}
