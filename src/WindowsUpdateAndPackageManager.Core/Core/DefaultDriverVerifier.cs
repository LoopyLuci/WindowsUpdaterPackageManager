namespace WindowsUpdateAndPackageManager.Core;

public sealed class DefaultDriverVerifier : IDriverVerifier
{
    public Task<bool> VerifyAsync(Models.DriverPackageManifest manifest, CancellationToken cancellationToken = default)
    {
        if (manifest is null) return Task.FromResult(false);
        if (string.IsNullOrWhiteSpace(manifest.InfPath)) return Task.FromResult(false);
        if (string.IsNullOrWhiteSpace(manifest.ClassGuid)) return Task.FromResult(false);
        if (string.IsNullOrWhiteSpace(manifest.Architecture)) return Task.FromResult(false);
        if (string.IsNullOrWhiteSpace(manifest.Manufacturer)) return Task.FromResult(false);
        return Task.FromResult(true);
    }
}
