using Microsoft.Data.Sqlite;
using WindowsUpdateAndPackageManager.Core;

namespace WindowsUpdateAndPackageManager.Data;

public interface IDeltaStore
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(DeltaManifest manifest, CancellationToken cancellationToken = default);
    Task<DeltaManifest?> GetAsync(string packageId, string fromVersion, string toVersion, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DeltaManifest>> ListAsync(string packageId, string? toVersion = null, CancellationToken cancellationToken = default);
}
