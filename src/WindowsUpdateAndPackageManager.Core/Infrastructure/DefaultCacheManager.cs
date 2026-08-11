using WindowsUpdateAndPackageManager.Models;

namespace WindowsUpdateAndPackageManager.Infrastructure;

public sealed class DefaultCacheManager : ICacheManager
{
    private readonly string _root;

    public DefaultCacheManager(string root)
    {
        _root = root;
    }

    public Task<string> GetCacheRootAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_root);
        return Task.FromResult(_root);
    }

    public Task<string> EnsurePackageCacheAsync(string packageId, string version, CancellationToken cancellationToken = default)
    {
        var dir = Path.Combine(_root, $"{packageId}@{version}");
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "package.cache"), string.Empty);
        }
        catch { }
        return Task.FromResult(dir);
    }

    public Task<bool> IsCachedAsync(string packageId, string version, CancellationToken cancellationToken = default)
    {
        var file = Path.Combine(_root, $"{packageId}@{version}", "package.cache");
        return Task.FromResult(File.Exists(file));
    }

    public Task PruneAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_root)) return Task.CompletedTask;
        foreach (var dir in Directory.EnumerateDirectories(_root))
        {
            try { Directory.Delete(dir, true); }
            catch { }
        }
        return Task.CompletedTask;
    }
}
