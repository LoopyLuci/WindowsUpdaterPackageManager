using System.Diagnostics;
using System.IO.Compression;

namespace WindowsUpdateAndPackageManager.Core;

public interface ISelfUpdater
{
    Task<bool> SelfUpdateAsync(string? tag = null, CancellationToken cancellationToken = default);
}

public sealed class SelfUpdater : ISelfUpdater
{
    private readonly string _currentExePath;
    private readonly string _githubOwner;
    private readonly string _githubRepo;
    private readonly string _assetName;
    private readonly Func<ProcessStartInfo, Task<bool>> _processStartAsync;
    private readonly Func<string, CancellationToken, Task<string?>> _fetchReleaseAsync;
    private readonly Func<string, string, CancellationToken, Task> _downloadAssetAsync;

    public SelfUpdater(
        string? currentExePath = null,
        string? githubOwner = null,
        string? githubRepo = null,
        string? assetName = null,
        Func<ProcessStartInfo, Task<bool>>? processStartAsync = null,
        Func<string, CancellationToken, Task<string?>>? fetchReleaseAsync = null,
        Func<string, string, CancellationToken, Task>? downloadAssetAsync = null)
    {
        _currentExePath = currentExePath ?? Environment.ProcessPath ?? throw new InvalidOperationException("Cannot determine current executable path.");
        _githubOwner = githubOwner ?? "LoopyLuci";
        _githubRepo = githubRepo ?? "WindowsUpdateAndPackageManager";
        _assetName = assetName ?? "wupm-cli.zip";
        _processStartAsync = processStartAsync ?? DefaultProcessStartAsync;
        _fetchReleaseAsync = fetchReleaseAsync ?? DefaultFetchReleaseAsync;
        _downloadAssetAsync = downloadAssetAsync ?? DefaultDownloadAssetAsync;
    }

    public async Task<bool> SelfUpdateAsync(string? tag = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var releaseUrl = string.IsNullOrWhiteSpace(tag)
                ? $"https://api.github.com/repos/{_githubOwner}/{_githubRepo}/releases/latest"
                : $"https://api.github.com/repos/{_githubOwner}/{_githubRepo}/releases/tags/{tag}";

            var latestRelease = await _fetchReleaseAsync(releaseUrl, cancellationToken).ConfigureAwait(false);
            if (latestRelease is null) return false;

            var tempDir = Path.Combine(Path.GetTempPath(), "wupm-selfupdate", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var zipPath = Path.Combine(tempDir, _assetName);

            await _downloadAssetAsync(latestRelease, zipPath, cancellationToken).ConfigureAwait(false);

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

            return await _processStartAsync(psi).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Self-update failed: {ex.Message}");
            return false;
        }
    }

    private static async Task<bool> DefaultProcessStartAsync(ProcessStartInfo psi)
    {
        using var proc = Process.Start(psi);
        if (proc is null) return false;
        await proc.WaitForExitAsync().ConfigureAwait(false);
        return proc.ExitCode == 0;
    }

    private static async Task<string?> DefaultFetchReleaseAsync(string url, CancellationToken cancellationToken)
    {
        using var client = new HttpClient();
        using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task DefaultDownloadAssetAsync(string releaseJson, string destinationPath, CancellationToken cancellationToken)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(releaseJson);
        var root = doc.RootElement;
        if (!root.TryGetProperty("assets", out var assets)) throw new InvalidOperationException("Release has no assets.");

        foreach (var asset in assets.EnumerateArray())
        {
            if (!asset.TryGetProperty("name", out var nameElement)) continue;
            var name = nameElement.GetString();
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (!string.Equals(name, Path.GetFileName(destinationPath), StringComparison.OrdinalIgnoreCase)) continue;

            if (!asset.TryGetProperty("browser_download_url", out var urlElement)) continue;
            var url = urlElement.GetString();
            if (string.IsNullOrWhiteSpace(url)) continue;

            using var response = await new HttpClient().GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var target = File.Open(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
            return;
        }

        throw new InvalidOperationException($"{Path.GetFileName(destinationPath)} asset not found in latest release.");
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

    private static string Escape(string s) => s.Replace("'", "''");
}
