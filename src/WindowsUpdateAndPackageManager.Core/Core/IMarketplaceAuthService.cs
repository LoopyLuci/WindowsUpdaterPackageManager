namespace WindowsUpdateAndPackageManager.Core;

public interface IMarketplaceAuthService
{
    Task<string?> GetTokenAsync(CancellationToken cancellationToken = default);
    Task SetTokenAsync(string token, CancellationToken cancellationToken = default);
    Task ClearTokenAsync(CancellationToken cancellationToken = default);
}
