namespace WindowsUpdateAndPackageManager.Core;

public interface IMarketplaceAuthService
{
    Task<string?> GetTokenAsync(CancellationToken cancellationToken = default);
    string? GetToken();
    Task SetTokenAsync(string token, CancellationToken cancellationToken = default);
    Task ClearTokenAsync(CancellationToken cancellationToken = default);
}
