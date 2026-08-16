using System.Security.Cryptography;
using System.Text;

namespace WindowsUpdateAndPackageManager.Core;

public sealed class FileMarketplaceAuthService : IMarketplaceAuthService, IAsyncDisposable
{
    private readonly string _authPath;

    public FileMarketplaceAuthService(string dataRoot)
    {
        _authPath = Path.Combine(dataRoot, "marketplace", "auth.json");
    }

    public string? GetToken()
    {
        if (!File.Exists(_authPath)) return null;
        try
        {
            var json = File.ReadAllText(_authPath);
            var doc = System.Text.Json.JsonSerializer.Deserialize<MarketplaceAuth>(json);
            return doc?.Token;
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_authPath)) return null;
        try
        {
            var json = await File.ReadAllTextAsync(_authPath, cancellationToken).ConfigureAwait(false);
            var doc = System.Text.Json.JsonSerializer.Deserialize<MarketplaceAuth>(json);
            return doc?.Token;
        }
        catch
        {
            return null;
        }
    }

    public async Task SetTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token)) throw new ArgumentException("Token is required.", nameof(token));

        var dir = Path.GetDirectoryName(_authPath)!;
        Directory.CreateDirectory(dir);

        var doc = new MarketplaceAuth
        {
            Token = token
        };

        var json = System.Text.Json.JsonSerializer.Serialize(doc, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_authPath, json, cancellationToken).ConfigureAwait(false);
    }

    public async Task ClearTokenAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(_authPath))
        {
            await Task.Run(() => File.Delete(_authPath), cancellationToken).ConfigureAwait(false);
        }
    }

    public ValueTask DisposeAsync()
{
    GC.SuppressFinalize(this);
    return ValueTask.CompletedTask;
}

    private sealed class MarketplaceAuth
    {
        public string Token { get; set; } = string.Empty;
    }
}
