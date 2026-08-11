using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
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
            return ValidateIndex(doc.RootElement);
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

    private static bool ValidateIndex(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return false;
        if (!root.TryGetProperty("schemaVersion", out var schema) || schema.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(schema.GetString()))
        {
            return false;
        }

        if (!IsSupportedSchemaVersion(schema.GetString()!))
        {
            return false;
        }

        if (!root.TryGetProperty("repositoryUrl", out var repo) || repo.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(repo.GetString()))
        {
            return false;
        }
        if (!root.TryGetProperty("packages", out var packages) || packages.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var pkg in packages.EnumerateArray())
        {
            if (pkg.ValueKind != JsonValueKind.Object) return false;
            if (!pkg.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(id.GetString()))
            {
                return false;
            }
            if (!pkg.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(version.GetString()))
            {
                return false;
            }
            if (!pkg.TryGetProperty("sha256", out var sha) || sha.ValueKind != JsonValueKind.String || !IsHexSha256(sha.GetString()))
            {
                return false;
            }
            if (!pkg.TryGetProperty("created", out var created) || created.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(created.GetString()))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSupportedSchemaVersion(string? version)
    {
        return version is "1.0" or "2";
    }

    private static bool IsHexSha256(string? value)
    {
        if (value is null) return false;
        if (value.Length != 64) return false;
        foreach (var ch in value)
        {
            var isHex = (ch >= '0' && ch <= '9') || (ch >= 'A' && ch <= 'F') || (ch >= 'a' && ch <= 'f');
            if (!isHex) return false;
        }
        return true;
    }
}
