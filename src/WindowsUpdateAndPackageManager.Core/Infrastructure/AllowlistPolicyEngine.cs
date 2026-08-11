using WindowsUpdateAndPackageManager.Models;

namespace WindowsUpdateAndPackageManager.Infrastructure;

public sealed class AllowlistPolicyEngine : IPolicyEngine
{
    private readonly HashSet<string> _allowed = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _denied = new(StringComparer.OrdinalIgnoreCase);

    public Task<bool> IsAllowedAsync(string packageId, CancellationToken cancellationToken = default)
    {
        if (_denied.Contains(packageId)) return Task.FromResult(false);
        if (_allowed.Count == 0 || _allowed.Contains(packageId)) return Task.FromResult(true);
        return Task.FromResult(false);
    }

    public Task<bool> ShouldBlockRebootAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<bool> VerifyDriverAsync(Models.DriverPackageManifest manifest, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task ApplyAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public void Allow(string packageId)
    {
        _allowed.Add(packageId);
        _denied.Remove(packageId);
    }

    public void Deny(string packageId)
    {
        _denied.Add(packageId);
        _allowed.Remove(packageId);
    }
}
