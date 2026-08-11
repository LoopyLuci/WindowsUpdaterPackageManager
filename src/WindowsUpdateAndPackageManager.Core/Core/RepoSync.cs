using System.IO;
using WindowsUpdateAndPackageManager.Data;
using WindowsUpdateAndPackageManager.Infrastructure;
using WindowsUpdateAndPackageManager.Models;

namespace WindowsUpdateAndPackageManager.Core;

public sealed class RepoSync : IRepoSync
{
    private readonly IRepoClient _client;
    private readonly IManifestValidator _validator;
    private readonly IStateDatabase _state;
    private readonly IAuditStore _auditStore;
    private readonly ICacheManager _cache;

    public RepoSync(IRepoClient client, IManifestValidator validator, IStateDatabase state, IAuditStore auditStore, ICacheManager cache)
    {
        _client = client;
        _validator = validator;
        _state = state;
        _auditStore = auditStore;
        _cache = cache;
    }

    public async Task<SyncResult> SyncAsync(string repositoryUrl, CancellationToken cancellationToken = default)
    {
        var result = new SyncResult { RepositoryUrl = repositoryUrl };
        try
        {
            var json = await _client.DownloadIndexAsync(repositoryUrl, cancellationToken).ConfigureAwait(false);
            if (json is null || !await _validator.ValidateAsync(json, cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidOperationException("Repository manifest validation failed.");
            }
            var index = await _validator.ParseAsync(json, cancellationToken).ConfigureAwait(false);
            if (index is null) throw new InvalidOperationException("Repository manifest is empty.");

            result.PackagesUpdated = 0;
            foreach (var package in index.Packages)
            {
                try
                {
                    var assetUrl = BuildAssetUrl(repositoryUrl, package);
                    var cacheDir = await _cache.EnsurePackageCacheAsync(package.Id, package.Version, cancellationToken).ConfigureAwait(false);
                    var packageFile = Path.Combine(cacheDir, $"{package.Id}@{package.Version}.wupkg");

                    if (!await _cache.IsCachedAsync(package.Id, package.Version, cancellationToken).ConfigureAwait(false))
                    {
                        await using var stream = await _client.DownloadPackageAsync(assetUrl, cancellationToken).ConfigureAwait(false);
                        await using var output = File.OpenWrite(packageFile);
                        await stream.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
                    }

                    if (!string.IsNullOrWhiteSpace(package.Sha256) && !await _validator.VerifyPackageIntegrityAsync(packageFile, package.Sha256, cancellationToken).ConfigureAwait(false))
                    {
                        throw new InvalidOperationException($"Integrity check failed for {package.Id}@{package.Version}.");
                    }

                    result.PackagesUpdated++;
                }
                catch (Exception ex)
                {
                    result.Message += $"{package.Id}: {ex.Message}; ";
                }
            }

            result.Success = result.PackagesUpdated > 0;
            result.Message = string.IsNullOrWhiteSpace(result.Message) ? "Sync completed." : result.Message.Trim();
        }
        catch (Exception ex)
        {
            result.Message = ex.Message;
        }
        finally
        {
            await _auditStore.AppendAsync(new AuditEntry
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTimeOffset.UtcNow,
                Action = "Repo.Sync",
                Success = result.Success,
                Message = result.Message
            }, cancellationToken).ConfigureAwait(false);
        }
        return result;
    }

    public async Task<IReadOnlyList<PackageManifest>> ListAsync(string repositoryUrl, CancellationToken cancellationToken = default)
    {
        var json = await _client.DownloadIndexAsync(repositoryUrl, cancellationToken).ConfigureAwait(false);
        if (json is null || !await _validator.ValidateAsync(json, cancellationToken).ConfigureAwait(false))
        {
            return Array.Empty<PackageManifest>();
        }

        var index = await _validator.ParseAsync(json, cancellationToken).ConfigureAwait(false);
        return index?.Packages ?? Array.Empty<PackageManifest>();
    }

    private static string BuildAssetUrl(string repositoryUrl, PackageManifest package)
    {
        var baseUrl = repositoryUrl.TrimEnd('/');
        var assetFileName = $"{package.Id}@{package.Version}.wupkg";
        return $"{baseUrl}/releases/latest/download/{assetFileName}";
    }
}
