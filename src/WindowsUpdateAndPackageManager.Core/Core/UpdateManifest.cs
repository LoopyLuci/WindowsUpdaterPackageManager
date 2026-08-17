namespace WindowsUpdateAndPackageManager.Core;

public sealed record UpdateManifest(string WindowsVersion, string Architecture, string PackageId, string Version, string Sha256, string SourceUrl, DateTimeOffset PublishedAt, string Channel = "stable")
{
    public List<string> Channels { get; init; } = new() { Channel };
}
