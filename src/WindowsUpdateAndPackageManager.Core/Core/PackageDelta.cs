using System.Text.Json;

namespace WindowsUpdateAndPackageManager.Core;

public interface IPackageDeltaProvider
{
    Task<DeltaManifest?> GetDeltaAsync(string packageId, string fromVersion, string toVersion, CancellationToken cancellationToken = default);
    Task<bool> ApplyDeltaAsync(string packageId, string fromVersion, string toVersion, CancellationToken cancellationToken = default);
}

public sealed class DeltaManifest
{
    public string PackageId { get; init; } = string.Empty;
    public string FromVersion { get; init; } = string.Empty;
    public string ToVersion { get; init; } = string.Empty;
    public string DeltaUrl { get; init; } = string.Empty;
    public long DeltaSize { get; init; }
    public string DeltaHash { get; init; } = string.Empty;
    public string? PreviousSha256 { get; init; }
}
