namespace WindowsUpdateAndPackageManager.Models;

public class InstallResult
{
    public bool Success { get; set; }
    public string PackageId { get; set; } = string.Empty;
    public string? InstalledVersion { get; set; }
    public string? Message { get; set; }
    public string? LogPath { get; set; }
}

public class UninstallResult
{
    public bool Success { get; set; }
    public string PackageId { get; set; } = string.Empty;
    public string? Message { get; set; }
}

public class WindowsUpdateResult
{
    public bool Success { get; set; }
    public int UpdatesFound { get; set; }
    public int UpdatesInstalled { get; set; }
    public bool RebootRequired { get; set; }
    public string? Message { get; set; }
}

public class SyncResult
{
    public bool Success { get; set; }
    public string RepositoryUrl { get; set; } = string.Empty;
    public int PackagesDownloaded { get; set; }
    public int PackagesUpdated { get; set; }
    public int PackagesRemoved { get; set; }
    public string? Message { get; set; }
}
