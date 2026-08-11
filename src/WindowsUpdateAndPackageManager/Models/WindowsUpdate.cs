namespace WindowsUpdateAndPackageManager.Models;

public class WindowsUpdate
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? SupportUrl { get; init; }
    public long? SizeBytes { get; init; }
    public List<string> CategoryIds { get; init; } = new();
    public bool IsDriver => CategoryIds.Count == 0 ? false : CategoryIds.Any(id => id.StartsWith("E6A47BB7", StringComparison.OrdinalIgnoreCase) || id.StartsWith("595538D7", StringComparison.OrdinalIgnoreCase) || id.StartsWith("434323C5", StringComparison.OrdinalIgnoreCase));
}
