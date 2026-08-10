using WindowsUpdateAndPackageManager.Models;

namespace WindowsUpdateAndPackageManager.Infrastructure;

public sealed class AllowlistPolicyEngine : IPolicyEngine
{
    private readonly HashSet<string> _allowed = new(StringComparer.OrdinalIgnoreCase);

    public Task<bool> IsAllowedAsync(string packageId, CancellationToken cancellationToken = default)
        => Task.FromResult(_allowed.Count == 0 || _allowed.Contains(packageId));

    public Task<bool> ShouldBlockRebootAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task ApplyAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
