using System.Collections.Generic;
using System.Text.Json;

namespace WindowsUpdateAndPackageManager.Core;

public sealed class PluginRegistryEntry
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string Dependencies { get; set; } = string.Empty;
    public string[] Commands { get; set; } = Array.Empty<string>();
    public string Status { get; set; } = "Unknown";
}

public interface IPluginRegistry
{
    Task<IReadOnlyList<PluginRegistryEntry>> ListAsync(CancellationToken cancellationToken = default);
    Task AddAsync(string name, string version, string path, string dependencies = "", CancellationToken cancellationToken = default);
    Task RemoveAsync(string name, CancellationToken cancellationToken = default);
    Task<string?> ComputeSha256Async(string path, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> ValidateAsync(CancellationToken cancellationToken = default);
    Task SetEnabledAsync(string name, bool enabled, CancellationToken cancellationToken = default);
}

public sealed class FilePluginRegistry : IPluginRegistry
{
    private readonly string _registryPath;

    public FilePluginRegistry(string dataRoot)
    {
        _registryPath = Path.Combine(dataRoot, "plugins", "registry.json");
    }

    public async Task<IReadOnlyList<PluginRegistryEntry>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_registryPath)) return Array.Empty<PluginRegistryEntry>();
        var json = await File.ReadAllTextAsync(_registryPath, cancellationToken).ConfigureAwait(false);
        return System.Text.Json.JsonSerializer.Deserialize<List<PluginRegistryEntry>>(json) ?? new List<PluginRegistryEntry>();
    }

    public async Task AddAsync(string name, string version, string path, string dependencies, CancellationToken cancellationToken = default)
    {
        var entries = (await ListAsync(cancellationToken).ConfigureAwait(false)).ToList();
        entries.Add(new PluginRegistryEntry { Name = name, Version = version, Path = path, Enabled = true, Dependencies = dependencies });
        await WriteAsync(entries, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> ComputeSha256Async(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path)) return null;
        await using var stream = File.OpenRead(path);
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public async Task RemoveAsync(string name, CancellationToken cancellationToken = default)
    {
        var entries = (await ListAsync(cancellationToken).ConfigureAwait(false)).ToList();
        entries.RemoveAll(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        await WriteAsync(entries, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> ValidateAsync(CancellationToken cancellationToken = default)
    {
        var issues = new List<string>();
        var entries = (await ListAsync(cancellationToken).ConfigureAwait(false)).ToList();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Name))
            {
                issues.Add("Registry entry has an empty name.");
                continue;
            }

            if (!names.Add(entry.Name))
            {
                issues.Add($"Duplicate plugin name: {entry.Name}");
            }

            if (!File.Exists(entry.Path))
            {
                issues.Add($"Plugin file missing: {entry.Path}");
            }

            if (!string.IsNullOrWhiteSpace(entry.Dependencies))
            {
                var deps = entry.Dependencies.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var dep in deps)
                {
                    if (!entries.Any(e => e.Name.Equals(dep, StringComparison.OrdinalIgnoreCase)))
                    {
                        issues.Add($"Plugin '{entry.Name}' has missing dependency: {dep}");
                    }
                }
            }
        }

        return issues;
    }

    public async Task SetEnabledAsync(string name, bool enabled, CancellationToken cancellationToken = default)
    {
        var entries = (await ListAsync(cancellationToken).ConfigureAwait(false)).ToList();
        var entry = entries.FirstOrDefault(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            throw new InvalidOperationException($"Plugin '{name}' is not in the registry.");
        }

        entry.Enabled = enabled;
        await WriteAsync(entries, cancellationToken).ConfigureAwait(false);
    }

    private async Task WriteAsync(List<PluginRegistryEntry> entries, CancellationToken cancellationToken)
    {
        var dir = Path.GetDirectoryName(_registryPath)!;
        Directory.CreateDirectory(dir);
        var json = System.Text.Json.JsonSerializer.Serialize(entries, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_registryPath, json, cancellationToken).ConfigureAwait(false);
    }
}
