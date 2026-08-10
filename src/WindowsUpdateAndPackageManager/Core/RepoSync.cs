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

    public RepoSync(IRepoClient client, IManifestValidator validator, IStateDatabase state, IAuditStore auditStore)
    {
        _client = client;
        _validator = validator;
        _state = state;
        _auditStore = auditStore;
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

            result.PackagesUpdated = index.Packages.Count;
            result.Success = true;
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
}
