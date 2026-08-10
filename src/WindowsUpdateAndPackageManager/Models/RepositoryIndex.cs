namespace WindowsUpdateAndPackageManager.Models;

public class RepositoryIndex
{
    public string SchemaVersion { get; set; } = "1.0";
    public DateTimeOffset GeneratedAt { get; set; }
    public string RepositoryUrl { get; set; } = string.Empty;
    public IReadOnlyList<PackageManifest> Packages { get; set; } = Array.Empty<PackageManifest>();
}
