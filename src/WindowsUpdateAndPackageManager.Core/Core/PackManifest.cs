namespace WindowsUpdateAndPackageManager.Core;

public sealed class PackManifest
{
    public string Id { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public DateTimeOffset Created { get; set; }
    public string? PreviousSha256 { get; set; }
}
