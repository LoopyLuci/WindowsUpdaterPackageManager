using System.Diagnostics;
using System.IO.Compression;

namespace WindowsUpdateAndPackageManager.Core;

public interface ISelfUpdater
{
    Task<bool> SelfUpdateAsync(CancellationToken cancellationToken = default);
}

public sealed class SelfUpdater : ISelfUpdater
{
    private readonly string _currentExePath;
    private readonly string _githubOwner;
    private readonly string _githubRepo;
    private readonly string _assetName;
    private readonly HttpClient _httpClient;

    public SelfUpdater(string? currentExePath = null, string? githubOwner = null, string? githubRepo = null, string? assetName = null, HttpClient? httpClient = null)
    {
        _currentExePath = currentExePath ?? Environment.ProcessPath ?? throw new InvalidOperationException("Cannot determine current executable path.");
        _githubOwner = githubOwner ?? "LoopyLuci";
        _githubRepo = githubRepo ?? "WindowsUpdateAndPackageManager";
        _assetName = assetName ?? "wupm-cli.zip";
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<bool> SelfUpdateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var latestRelease = await DownloadLatestReleaseAsync(cancellationToken).ConfigureAwait(false);
            if (latestRelease is null) return false;

            var tempDir = Path.Combine(Path.GetTempPath(), "wupm-selfupdate", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var zipPath = Path.Combine(tempDir, _assetName);

            await DownloadAssetAsync(latestRelease, zipPath, cancellationToken).ConfigureAwait(false);

            var extractDir = Path.Combine(tempDir, "extracted");
            Directory.CreateDirectory(extractDir);
            ZipFile.ExtractToDirectory(zipPath, extractDir);

            var newExe = FindExecutable(extractDir);
            if (newExe is null)
            {
                Console.WriteLine("Self-update failed: no executable found in downloaded asset.");
                return false;
            }

            var updaterScript = Path.Combine(tempDir, "apply-update.ps1");
            await File.WriteAllTextAsync(updaterScript, $@"
$ErrorActionPreference = 'Stop'
$new = '{Escape(newExe)}'
$old = '{Escape(_currentExePath)}'
$backup = '{Escape(_currentExePath + ".bak")}'

if (Test-Path $backup) {{ Remove-Item $backup -Force }}
Move-Item $old $backup -Force
Move-Item $new $old -Force
Start-Process -FilePath $old -ArgumentList 'self-update-complete'
Remove-Item $backup -Force
", cancellationToken).ConfigureAwait(false);

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                ArgumentList = { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", updaterScript },
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process.Start(psi);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Self-update failed: {ex.Message}");
            return false;
        }
    }

    private static string? FindExecutable(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory, "*.exe", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(file);
            if (string.Equals(name, "Wupm.Cli.exe", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "Wupm.Cli", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "wupm.exe", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "wupm", StringComparison.OrdinalIgnoreCase))
            {
                return file;
            }
        }
        return null;
    }

    private async Task<string?> DownloadLatestReleaseAsync(CancellationToken cancellationToken)
    {
        var url = $"https://api.github.com/repos/{_githubOwner}/{_githubRepo}/releases/latest";
        using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task DownloadAssetAsync(string releaseJson, string destinationPath, CancellationToken cancellationToken)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(releaseJson);
        var root = doc.RootElement;
        if (!root.TryGetProperty("assets", out var assets)) throw new InvalidOperationException("Release has no assets.");

        foreach (var asset in assets.EnumerateArray())
        {
            if (!asset.TryGetProperty("name", out var nameElement)) continue;
            var name = nameElement.GetString();
            if (!string.Equals(name, _assetName, StringComparison.OrdinalIgnoreCase)) continue;

            if (!asset.TryGetProperty("browser_download_url", out var urlElement)) continue;
            var url = urlElement.GetString();
            if (string.IsNullOrWhiteSpace(url)) continue;

            using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var target = File.Open(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
            return;
        }

        throw new InvalidOperationException($"{_assetName} asset not found in latest release.");
    }

    private static string Escape(string s) => s.Replace("'", "''");
}
