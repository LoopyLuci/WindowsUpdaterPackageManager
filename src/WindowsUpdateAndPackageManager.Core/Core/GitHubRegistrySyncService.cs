using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace WindowsUpdateAndPackageManager.Core;

public sealed class GitHubRegistrySyncService : IRegistrySyncService
{
    private readonly IPluginRegistry _registry;
    private readonly string _ownerRepo;
    private readonly string? _token;

    public GitHubRegistrySyncService(IPluginRegistry registry, string ownerRepo, string? token = null)
    {
        _registry = registry;
        _ownerRepo = ownerRepo;
        _token = token;
    }

    public async Task SyncAsync(string ownerRepo, string branch, CancellationToken cancellationToken = default)
    {
        var effective = string.IsNullOrWhiteSpace(ownerRepo) ? _ownerRepo : ownerRepo;
        if (string.IsNullOrWhiteSpace(effective))
        {
            throw new InvalidOperationException("Owner/repo is required.");
        }

        var entries = _registry.ListAsync(cancellationToken).GetAwaiter().GetResult();
        var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
        var bytes = Encoding.UTF8.GetBytes(json);
        var path = $"plugin-registry-backup-{DateTime.UtcNow:yyyyMMddHHmmss}.json";

        using var http = new HttpClient();
        if (!string.IsNullOrWhiteSpace(_token))
        {
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        }
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("WUPM", "1.0"));
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));

        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        var url = $"https://api.github.com/repos/{effective}/contents/{path}";
        using var response = await http.PutAsync(url, new StringContent(JsonSerializer.Serialize(new
        {
            message = "Sync plugin registry",
            content = Convert.ToBase64String(bytes),
            branch = string.IsNullOrWhiteSpace(branch) ? "main" : branch
        }), Encoding.UTF8, "application/json"), cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }
}
