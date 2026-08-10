using System.Collections.ObjectModel;
using WindowsUpdateAndPackageManager.Models;

namespace WindowsUpdateAndPackageManager.Data;

public interface IStateDatabase
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task RecordInstallAsync(PackageManifest package, CancellationToken cancellationToken = default);
    Task RemoveInstallAsync(string packageId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PackageManifest>> ListInstalledAsync(CancellationToken cancellationToken = default);
    Task<bool> IsInstalledAsync(string packageId, string? version = null, CancellationToken cancellationToken = default);
}
