namespace WupmGui.Models;

public sealed class PackageItem
{
    public PackageItem(WindowsUpdateAndPackageManager.Models.PackageManifest package)
    {
        Package = package;
    }

    public WindowsUpdateAndPackageManager.Models.PackageManifest Package { get; }
    public string Title => Package.DisplayName;
    public string Version => Package.Version;
    public string Size => Package.SizeBytes is long b ? $"{b / 1024 / 1024} MB" : string.Empty;
    public string Category => Package.IsDriver ? "Driver" : "Update";
}
