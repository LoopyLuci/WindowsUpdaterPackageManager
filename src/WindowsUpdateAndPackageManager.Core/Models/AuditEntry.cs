namespace WindowsUpdateAndPackageManager.Models;

public class AuditEntry
{
    public Guid Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string Action { get; set; } = string.Empty;
    public string PackageId { get; set; } = string.Empty;
    public string? Version { get; set; }
    public string? ComputerName { get; set; }
    public string? User { get; set; }
    public bool Success { get; set; }
    public string? Message { get; set; }
}
