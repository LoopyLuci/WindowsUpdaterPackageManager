using WindowsUpdateAndPackageManager.Models;

namespace WindowsUpdateAndPackageManager.Core;

public interface IWindowsUpdateManager
{
    Task<WindowsUpdateResult> ScanAndInstallAsync(bool driversOnly = false, bool offlineScan = false, CancellationToken cancellationToken = default);
}

public interface IPackageManager
{
    Task<InstallResult> InstallAsync(PackageManifest package, CancellationToken cancellationToken = default);
    Task<UninstallResult> UninstallAsync(string packageId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PackageManifest>> ListInstalledAsync(CancellationToken cancellationToken = default);
    Task<bool> IsInstalledAsync(string packageId, string? version = null, CancellationToken cancellationToken = default);
}

public interface IDriverManager
{
    Task<InstallResult> InstallDriverAsync(PackageManifest driver, CancellationToken cancellationToken = default);
}

public interface IRepoSync
{
    Task<SyncResult> SyncAsync(string repositoryUrl, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PackageManifest>> ListAsync(string repositoryUrl, CancellationToken cancellationToken = default);
}

public interface IAuditor
{
    Task<IReadOnlyList<AuditEntry>> QueryAsync(DateTimeOffset? from = null, DateTimeOffset? to = null, string? action = null, CancellationToken cancellationToken = default);
}
