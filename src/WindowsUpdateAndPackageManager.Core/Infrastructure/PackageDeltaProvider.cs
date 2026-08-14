using System.Security.Cryptography;
using WindowsUpdateAndPackageManager.Core;
using WindowsUpdateAndPackageManager.Data;
using WindowsUpdateAndPackageManager.Infrastructure;

namespace WindowsUpdateAndPackageManager.Core;

public sealed class PackageDeltaProvider : IPackageDeltaProvider
{
    private readonly IDeltaStore _deltaStore;
    private readonly IRepoClient _repoClient;
    private readonly ICacheManager _cacheManager;

    public PackageDeltaProvider(IDeltaStore deltaStore, IRepoClient repoClient, ICacheManager cacheManager)
    {
        _deltaStore = deltaStore;
        _repoClient = repoClient;
        _cacheManager = cacheManager;
    }

    public event Action<string>? Progress;

    public async Task<DeltaManifest?> GetDeltaAsync(string packageId, string fromVersion, string toVersion, CancellationToken cancellationToken = default)
        => await _deltaStore.GetAsync(packageId, fromVersion, toVersion, cancellationToken).ConfigureAwait(false);

    public async Task<bool> ApplyDeltaAsync(string packageId, string fromVersion, string toVersion, CancellationToken cancellationToken = default)
    {
        var delta = await GetDeltaAsync(packageId, fromVersion, toVersion, cancellationToken).ConfigureAwait(false);
        if (delta is null) return false;

        Progress?.Invoke($"Downloading delta for {packageId} {fromVersion} -> {toVersion} ...");
        using var stream = await _repoClient.DownloadPackageAsync(delta.DeltaUrl, cancellationToken).ConfigureAwait(false);
        var targetDir = await _cacheManager.EnsurePackageCacheAsync(packageId, toVersion, cancellationToken).ConfigureAwait(false);
        Directory.CreateDirectory(targetDir);
        var targetPath = Path.Combine(targetDir, $"{packageId}@{toVersion}.wupkg");

        await using (var target = File.Create(targetPath))
        {
            Progress?.Invoke($"Applying delta for {packageId} ...");
            await stream.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
        }

        Progress?.Invoke($"Verifying {packageId} ...");
        using var sha = SHA256.Create();
        using (var hashStream = File.OpenRead(targetPath))
        {
            var hash = sha.ComputeHash(hashStream);
            var actualHash = Convert.ToHexString(hash).ToLowerInvariant();
            if (!string.Equals(actualHash, delta.DeltaHash, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(targetPath);
                return false;
            }
        }

        Progress?.Invoke($"Delta applied for {packageId}.");
        return true;
    }
}
