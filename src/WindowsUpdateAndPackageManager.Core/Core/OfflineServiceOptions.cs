namespace WindowsUpdateAndPackageManager.Core;

public sealed class OfflineServiceOptions
{
    public int DismTimeoutSeconds { get; set; } = 600;
    public int DismMaxRetries { get; set; } = 2;
    public int DismRetryDelayMs { get; set; } = 1000;
}
