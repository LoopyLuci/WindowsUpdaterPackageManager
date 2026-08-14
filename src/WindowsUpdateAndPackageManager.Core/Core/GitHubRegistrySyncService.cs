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

    public async Task<RegistrySyncResult> SyncAsync(string ownerRepo, string branch, CancellationToken cancellationToken = default)
    {
        var result = new RegistrySyncResult();
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

        var request = new
        {
            message = "Sync plugin registry",
            content = Convert.ToBase64String(bytes),
            branch = string.IsNullOrWhiteSpace(branch) ? "main" : branch
        };
        var requestJson = JsonSerializer.Serialize(request);
        using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        var url = $"https://api.github.com/repos/{effective}/contents/{path}";
        using var response = await http.PutAsync(url, content, cancellationToken).ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
        {
            result.Added = entries.Count;
        }
        else if ((int)response.StatusCode == 409)
        {
            result.Skipped = entries.Count;
            result.Conflicts.Add("GitHub reported a conflict; file may have been modified concurrently.");
        }
        response.EnsureSuccessStatusCode();
        return result;
    }
}
