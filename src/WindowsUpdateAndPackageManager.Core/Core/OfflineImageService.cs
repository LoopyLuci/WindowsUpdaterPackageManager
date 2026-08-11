using System.Diagnostics;

namespace WindowsUpdateAndPackageManager.Core;

public sealed class OfflineImageService : IOfflineImageService
{
    private readonly IDismProcessRunner _dism;

    public OfflineImageService(IDismProcessRunner? dism = null)
    {
        _dism = dism ?? new DefaultDismProcessRunner();
    }

    public async Task<OfflineImageResult> MountOrOpenAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        var result = new OfflineImageResult();
        try
        {
            var mountDir = Path.Combine(Path.GetTempPath(), $"wupm-image-{Guid.NewGuid():N}");
            Directory.CreateDirectory(mountDir);

            var exitCode = await _dism.RunAsync("dism.exe", $"/Mount-Image /ImageFile:\"{imagePath}\" /MountDir:\"{mountDir}\"", cancellationToken).ConfigureAwait(false);
            if (exitCode == 0)
            {
                result.Success = true;
                result.MountPath = mountDir;
                result.Message = "Image mounted successfully.";
            }
            else
            {
                Directory.Delete(mountDir, true);
                result.Message = $"DISM exited with code {exitCode}.";
            }
        }
        catch (Exception ex)
        {
            result.Message = $"Mount failed: {ex.Message}";
        }

        return result;
    }

    public async Task<OfflineImageResult> ApplyPackageAsync(string imageMountPath, string packagePath, CancellationToken cancellationToken = default)
    {
        var result = new OfflineImageResult();
        try
        {
            var exitCode = await _dism.RunAsync("dism.exe", $"/Image:\"{imageMountPath}\" /Add-Package /PackagePath:\"{packagePath}\"", cancellationToken).ConfigureAwait(false);
            result.Success = exitCode == 0;
            result.Message = exitCode == 0 ? "Package applied successfully." : $"DISM exited with code {exitCode}.";
        }
        catch (Exception ex)
        {
            result.Message = $"Apply failed: {ex.Message}";
        }

        return result;
    }

    public async Task<OfflineImageResult> DismountAsync(string imageMountPath, bool discard = false, CancellationToken cancellationToken = default)
    {
        var result = new OfflineImageResult();
        try
        {
            var commit = discard ? "/Discard" : "/Commit";
            var exitCode = await _dism.RunAsync("dism.exe", $"/Unmount-Image /MountDir:\"{imageMountPath}\" {commit}", cancellationToken).ConfigureAwait(false);
            result.Success = exitCode == 0;
            result.Message = exitCode == 0 ? "Image dismounted successfully." : $"DISM exited with code {exitCode}.";
            if (exitCode == 0)
            {
                Directory.Delete(imageMountPath, true);
            }
        }
        catch (Exception ex)
        {
            result.Message = $"Dismount failed: {ex.Message}";
        }

        return result;
    }
}
