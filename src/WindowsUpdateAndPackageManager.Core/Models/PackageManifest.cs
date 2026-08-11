namespace WindowsUpdateAndPackageManager.Models;

public class PackageManifest
{
    public string Id { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Homepage { get; set; }
    public string License { get; set; } = string.Empty;
    public bool IsDriver { get; set; }
    public string? Sha256 { get; set; }
    public long? SizeBytes { get; set; }
    public string? Architecture { get; set; }
    public string? MinWindowsVersion { get; set; }
    public string? MaxWindowsVersion { get; set; }
    public string InstallCommand { get; set; } = string.Empty;
    public string UninstallCommand { get; set; } = string.Empty;
    public bool RequiresReboot { get; set; }
    public string[]? Dependencies { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
    public bool IsDeprecated { get; set; }
    public string? DeprecationReason { get; set; }
    public string[]? Changelog { get; set; }
}
