using WindowsUpdateAndPackageManager.Models;

namespace WindowsUpdateAndPackageManager.Data;

public interface IAuditStore
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task AppendAsync(AuditEntry entry, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditEntry>> QueryAsync(DateTimeOffset? from = null, DateTimeOffset? to = null, string? action = null, CancellationToken cancellationToken = default);
}
