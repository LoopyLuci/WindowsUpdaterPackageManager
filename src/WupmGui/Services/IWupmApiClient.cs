using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Http;
using WindowsUpdateAndPackageManager.Models;
using WupmGui.Models;

namespace WupmGui.Services;

public interface IWupmApiClient
{
    Task<HealthResponse> GetHealthAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PackageManifest>> GetPackagesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PackageManifest>> GetInstalledAsync(CancellationToken cancellationToken = default);
    Task<InstallResult> InstallAsync(PackageManifest manifest, CancellationToken cancellationToken = default);
    Task<ScanResult> ScanAsync(bool offlineScan, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditEntry>> GetAuditAsync(DateTimeOffset? from, DateTimeOffset? to, string? action, CancellationToken cancellationToken = default);
}

public sealed class WupmApiClient : IWupmApiClient, IDisposable
{
    private readonly HttpClient _http;
    private bool _disposed;

    public WupmApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<HealthResponse> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync("/", cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<HealthResponse>(cancellationToken).ConfigureAwait(false))!;
    }

    public async Task<IReadOnlyList<PackageManifest>> GetPackagesAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync("/packages", cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<PackageManifest>>(cancellationToken).ConfigureAwait(false))!;
    }

    public async Task<IReadOnlyList<PackageManifest>> GetInstalledAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync("/installed", cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<PackageManifest>>(cancellationToken).ConfigureAwait(false))!;
    }

    public async Task<InstallResult> InstallAsync(PackageManifest manifest, CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync("/install", manifest, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<InstallResult>(cancellationToken).ConfigureAwait(false))!;
    }

    public async Task<ScanResult> ScanAsync(bool offlineScan, CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsync($"/windows-update?offlineScan={offlineScan}", null, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ScanResult>(cancellationToken).ConfigureAwait(false))!;
    }

    public async Task<IReadOnlyList<AuditEntry>> GetAuditAsync(DateTimeOffset? from, DateTimeOffset? to, string? action, CancellationToken cancellationToken = default)
    {
        var query = new List<string>();
        if (from is not null) query.Add($"from={Uri.EscapeDataString(from.Value.ToString("o"))}");
        if (to is not null) query.Add($"to={Uri.EscapeDataString(to.Value.ToString("o"))}");
        if (!string.IsNullOrWhiteSpace(action)) query.Add($"action={Uri.EscapeDataString(action!)}");

        var url = "/audit";
        if (query.Count > 0) url += "?" + string.Join("&", query);

        using var response = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<AuditEntry>>(cancellationToken).ConfigureAwait(false))!;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _http.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

public sealed class HealthResponse
{
    public string Status { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public sealed class InstallResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class ScanResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<PackageManifest> Packages { get; set; } = new();
}
