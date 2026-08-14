using System.Text.Json;

namespace WindowsUpdateAndPackageManager.Core;

public sealed class PluginRegistryEntry
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool Enabled { get; set; }
}

public interface IPluginRegistry
{
    Task<IReadOnlyList<PluginRegistryEntry>> ListAsync(CancellationToken cancellationToken = default);
    Task AddAsync(string name, string version, string path, CancellationToken cancellationToken = default);
    Task RemoveAsync(string name, CancellationToken cancellationToken = default);
    Task<string?> ComputeSha256Async(string path, CancellationToken cancellationToken = default);
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

    public async Task AddAsync(string name, string version, string path, CancellationToken cancellationToken = default)
    {
        var entries = (await ListAsync(cancellationToken).ConfigureAwait(false)).ToList();
        entries.Add(new PluginRegistryEntry { Name = name, Version = version, Path = path, Enabled = true });
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

    private async Task WriteAsync(List<PluginRegistryEntry> entries, CancellationToken cancellationToken)
    {
        var dir = Path.GetDirectoryName(_registryPath)!;
        Directory.CreateDirectory(dir);
        var json = System.Text.Json.JsonSerializer.Serialize(entries, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_registryPath, json, cancellationToken).ConfigureAwait(false);
    }
}
