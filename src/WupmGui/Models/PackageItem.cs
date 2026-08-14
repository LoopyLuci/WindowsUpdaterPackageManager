using WindowsUpdateAndPackageManager.Models;

namespace WupmGui.Models;

public sealed class PackageItem
{
    public PackageItem(PackageManifest package)
    {
        Package = package;
    }

    public PackageManifest Package { get; }
    public string Title => Package.DisplayName;
    public string Version => Package.Version;
    public string Size => Package.SizeBytes is long b ? $"{b / 1024 / 1024} MB" : string.Empty;
    public string Category => Package.IsDriver ? "Driver" : "Update";
}
