using System.Threading;
using System.Threading.Tasks;

namespace WindowsUpdateAndPackageManager.Infrastructure;

public interface IMarketplaceSearchCache
{
    Task<IReadOnlyList<MarketplacePlugin>> GetAsync(string query, CancellationToken cancellationToken = default);
    Task SetAsync(string query, IReadOnlyList<MarketplacePlugin> results, CancellationToken cancellationToken = default);
}
