using WindowsUpdateAndPackageManager.Data;

namespace WindowsUpdateAndPackageManager.Core;

public sealed class Auditor : IAuditor
{
    private readonly IAuditStore _store;

    public Auditor(IAuditStore store)
    {
        _store = store;
    }

    public Task<IReadOnlyList<Models.AuditEntry>> QueryAsync(DateTimeOffset? from = null, DateTimeOffset? to = null, string? action = null, CancellationToken cancellationToken = default)
        => _store.QueryAsync(from, to, action, cancellationToken);
}
