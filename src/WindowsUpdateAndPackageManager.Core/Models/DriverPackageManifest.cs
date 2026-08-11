namespace WindowsUpdateAndPackageManager.Models;

public class DriverPackageManifest
{
    public string Id { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Manufacturer { get; set; }
    public string? Architecture { get; set; }
    public string? DriverType { get; set; }
    public string? InfPath { get; set; }
    public string? ClassGuid { get; set; }
    public string? Provider { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
    public string[]? Changelog { get; set; }
}
