namespace WupmGui.Models;

public sealed class UpdateItem
{
    public string PackageId { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string WindowsVersion { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string BuildNumber { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
}
