using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using WupmMcp.Protocol;

namespace WupmMcp.Tools;

public sealed class WupmApiClient
{
    private readonly HttpClient _http;
    private bool _disposed;

    public WupmApiClient()
    {
        _http = new HttpClient();
    }

    public async Task<JsonNode> GetHealthAsync(CancellationToken ct)
    {
        using var response = await _http.GetAsync("http://localhost:5002/", ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonNode.Parse(json)!;
    }

    public async Task<JsonNode> ScanAsync(bool offlineScan, CancellationToken ct)
    {
        using var response = await _http.PostAsync($"http://localhost:5002/windows-update?offlineScan={offlineScan}", null, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonNode.Parse(json)!;
    }

    public async Task<JsonNode> InstallAsync(JsonNode manifest, CancellationToken ct)
    {
        using var response = await _http.PostAsync("http://localhost:5002/install", new StringContent(manifest.ToJsonString(), Encoding.UTF8, "application/json"), ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonNode.Parse(json)!;
    }

    public async Task<JsonNode> ListPackagesAsync(CancellationToken ct)
    {
        using var response = await _http.GetAsync("http://localhost:5002/packages", ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonNode.Parse(json)!;
    }

    public async Task<JsonNode> ListInstalledAsync(CancellationToken ct)
    {
        using var response = await _http.GetAsync("http://localhost:5002/installed", ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonNode.Parse(json)!;
    }

    public async Task<JsonNode> ListPluginsAsync(CancellationToken ct)
    {
        using var response = await _http.GetAsync("http://localhost:5002/plugins", ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonNode.Parse(json)!;
    }

    public async Task<JsonNode> MarketplaceSearchAsync(string query, CancellationToken ct)
    {
        using var response = await _http.GetAsync($"http://localhost:5002/marketplace/search?query={Uri.EscapeDataString(query ?? string.Empty)}", ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonNode.Parse(json)!;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _http.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
