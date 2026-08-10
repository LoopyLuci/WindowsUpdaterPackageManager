using System.Net.Http;
using System.Security.Cryptography;
using WindowsUpdateAndPackageManager.Models;

namespace WindowsUpdateAndPackageManager.Infrastructure;

public sealed class DefaultManifestValidator : IManifestValidator
{
    public async Task<RepositoryIndex?> ParseAsync(string json, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        try
        {
            var index = System.Text.Json.JsonSerializer.Deserialize<RepositoryIndex>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return index;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> ValidateAsync(string json, CancellationToken cancellationToken = default)
    {
        var index = await ParseAsync(json, cancellationToken).ConfigureAwait(false);
        if (index is null) return false;
        return !string.IsNullOrWhiteSpace(index.SchemaVersion)
               && index.Packages is not null;
    }

    public async Task<bool> VerifyPackageIntegrityAsync(string packagePath, string? expectedSha256, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(expectedSha256)) return true;
        await using var stream = File.OpenRead(packagePath);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
        var hex = Convert.ToHexString(hash).ToLowerInvariant();
        return string.Equals(hex, expectedSha256, StringComparison.OrdinalIgnoreCase);
    }
}
