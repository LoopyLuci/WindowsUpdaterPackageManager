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
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("schemaVersion", out var schema) || schema.ValueKind != System.Text.Json.JsonValueKind.String || string.IsNullOrWhiteSpace(schema.GetString()))
            {
                return false;
            }
            if (!root.TryGetProperty("repositoryUrl", out var repo) || repo.ValueKind != System.Text.Json.JsonValueKind.String || string.IsNullOrWhiteSpace(repo.GetString()))
            {
                return false;
            }
            if (!root.TryGetProperty("packages", out var packages) || packages.ValueKind != System.Text.Json.JsonValueKind.Array)
            {
                return false;
            }
            return true;
        }
        catch
        {
            return false;
        }
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
