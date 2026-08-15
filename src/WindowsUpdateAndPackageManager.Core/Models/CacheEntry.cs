namespace WindowsUpdateAndPackageManager.Models;

public sealed class CacheEntry
{
    public string PackageId { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTimeOffset CachedAt { get; set; }
}
