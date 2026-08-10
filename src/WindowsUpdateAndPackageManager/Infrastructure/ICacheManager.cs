using WindowsUpdateAndPackageManager.Models;

namespace WindowsUpdateAndPackageManager.Infrastructure;

public interface ICacheManager
{
    Task<string> GetCacheRootAsync(CancellationToken cancellationToken = default);
    Task<string> EnsurePackageCacheAsync(string packageId, string version, CancellationToken cancellationToken = default);
    Task<bool> IsCachedAsync(string packageId, string version, CancellationToken cancellationToken = default);
    Task PruneAsync(CancellationToken cancellationToken = default);
}
