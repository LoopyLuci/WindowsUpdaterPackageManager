using System.Text.Json;

namespace WindowsUpdateAndPackageManager.Infrastructure;

public sealed class FileMarketplaceSearchCache : IMarketplaceSearchCache
{
    private readonly string _cacheRoot;

    public FileMarketplaceSearchCache(string cacheRoot)
    {
        _cacheRoot = cacheRoot;
        Directory.CreateDirectory(_cacheRoot);
    }

    public async Task<IReadOnlyList<MarketplacePlugin>> GetAsync(string query, CancellationToken cancellationToken = default)
    {
        var path = GetPath(query);
        if (!File.Exists(path))
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

    public async Task SetAsync(string query, IReadOnlyList<MarketplacePlugin> results, CancellationToken cancellationToken = default)
    {
        var path = GetPath(query);
        var json = JsonSerializer.Serialize(results);
        await File.WriteAllTextAsync(path, json, cancellationToken).ConfigureAwait(false);
    }

    private string GetPath(string query)
    {
        var safe = string.Join("_", query.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_cacheRoot, $"marketplace-search-{safe}.json");
    }
}
