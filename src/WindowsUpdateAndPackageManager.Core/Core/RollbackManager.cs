using WindowsUpdateAndPackageManager.Data;
using WindowsUpdateAndPackageManager.Models;

namespace WindowsUpdateAndPackageManager.Core;

public sealed class RollbackManager
{
    private readonly IStateDatabase _state;
    private readonly IAuditStore _auditStore;
    private readonly IProcessRunner _processRunner;

    public RollbackManager(IStateDatabase state, IAuditStore auditStore, IProcessRunner? processRunner = null)
    {
        _state = state;
        _auditStore = auditStore;
        _processRunner = processRunner ?? new DefaultProcessRunner();
    }

    public async Task<bool> RollbackAsync(string? packageId = null, CancellationToken cancellationToken = default)
    {
        var installed = await _state.ListInstalledAsync(cancellationToken).ConfigureAwait(false);
        var targets = packageId is null
            ? installed.ToList()
            : installed.Where(x => x.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase)).ToList();

        if (targets.Count == 0)
        {
            await _auditStore.AppendAsync(new AuditEntry
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTimeOffset.UtcNow,
                Action = "Rollback",
                PackageId = packageId ?? string.Empty,
                Success = false,
                Message = "No matching installed packages."
            }, cancellationToken).ConfigureAwait(false);
            return false;
        }

        bool anySuccess = false;
        foreach (var pkg in targets)
        {
            try
            {
                var exit = await _processRunner.StartAndWaitAsync(pkg.UninstallCommand, string.Empty, cancellationToken).ConfigureAwait(false);
                if (exit == 0)
                {
                    await _state.RemoveInstallAsync(pkg.Id, cancellationToken).ConfigureAwait(false);
                    anySuccess = true;
                }
            }
            catch { }
        }

        await _auditStore.AppendAsync(new AuditEntry
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            Action = "Rollback",
            PackageId = packageId ?? string.Empty,
            Success = anySuccess,
            Message = anySuccess ? null : "Rollback did not complete successfully."
        }, cancellationToken).ConfigureAwait(false);

        return anySuccess;
    }
}
