namespace WindowsUpdateAndPackageManager.Core;

public interface IOfflineImageService
{
    Task<OfflineImageResult> MountOrOpenAsync(string imagePath, CancellationToken cancellationToken = default);
    Task<OfflineImageResult> ApplyPackageAsync(string imageMountPath, string packagePath, CancellationToken cancellationToken = default);
    Task<OfflineImageResult> DismountAsync(string imageMountPath, bool discard = false, CancellationToken cancellationToken = default);
}

public sealed class OfflineImageResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? MountPath { get; set; }
}
