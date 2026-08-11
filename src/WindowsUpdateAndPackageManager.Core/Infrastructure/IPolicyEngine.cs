namespace WindowsUpdateAndPackageManager.Infrastructure;

public interface IPolicyEngine
{
    Task<bool> IsAllowedAsync(string packageId, CancellationToken cancellationToken = default);
    Task<bool> ShouldBlockRebootAsync(CancellationToken cancellationToken = default);
    Task ApplyAsync(CancellationToken cancellationToken = default);
    Task<bool> VerifyDriverAsync(Models.DriverPackageManifest manifest, CancellationToken cancellationToken = default);
}
