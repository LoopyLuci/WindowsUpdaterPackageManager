namespace WindowsUpdateAndPackageManager.Core;

public sealed record UpdateManifest(string WindowsVersion, string Architecture, string PackageId, string Version, string Sha256, string SourceUrl, DateTimeOffset PublishedAt, string Channel = "stable", string? DisplayName = null, string? BuildNumber = null)
{
    public List<string> Channels { get; init; } = new() { Channel };
    public string? BuildNumber { get; init; } = BuildNumber;
    public string? DisplayName { get; init; } = DisplayName;
}
