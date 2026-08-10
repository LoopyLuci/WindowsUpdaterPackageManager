namespace WindowsUpdateAndPackageManager.Models;

public class WindowsUpdate
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? SupportUrl { get; init; }
    public long? SizeBytes { get; init; }
}
