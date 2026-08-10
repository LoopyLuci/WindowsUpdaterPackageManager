namespace WindowsUpdateAndPackageManager.Models;

public class WindowsUpdateInstallResult
{
    public bool Success { get; init; }
    public int InstalledCount { get; init; }
    public bool RebootRequired { get; init; }
    public string? Message { get; init; }
}
