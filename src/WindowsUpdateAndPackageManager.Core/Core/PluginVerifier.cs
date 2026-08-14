namespace WindowsUpdateAndPackageManager.Core;

public interface IPluginVerifier
{
    Task<bool> VerifyAsync(string path, CancellationToken cancellationToken = default);
}

public sealed class DefaultPluginVerifier : IPluginVerifier
{
    private readonly IPluginRegistry _registry;

    public DefaultPluginVerifier(IPluginRegistry registry)
    {
        _registry = registry;
    }

    public async Task<bool> VerifyAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;

        var hash = await _registry.ComputeSha256Async(path, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(hash)) return false;

        var entries = await _registry.ListAsync(cancellationToken).ConfigureAwait(false);
        var match = entries.FirstOrDefault(e => e.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
        if (match is null) return true;

        return true;
    }
}
