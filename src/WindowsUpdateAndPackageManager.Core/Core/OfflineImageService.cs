using System.Diagnostics;

namespace WindowsUpdateAndPackageManager.Core;

public sealed class OfflineImageService : IOfflineImageService
{
    public async Task<OfflineImageResult> MountOrOpenAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        var result = new OfflineImageResult();
        try
        {
            var mountDir = Path.Combine(Path.GetTempPath(), $"wupm-image-{Guid.NewGuid():N}");
            Directory.CreateDirectory(mountDir);

            var psi = new ProcessStartInfo
            {
                FileName = "dism.exe",
                Arguments = $"/Mount-Image /ImageFile:\"{imagePath}\" /MountDir:\"{mountDir}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p is null) throw new InvalidOperationException("Failed to start DISM.");
            await p.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            if (p.ExitCode == 0)
            {
                result.Success = true;
                result.MountPath = mountDir;
                result.Message = "Image mounted successfully.";
            }
            else
            {
                Directory.Delete(mountDir, true);
                result.Message = $"DISM exited with code {p.ExitCode}.";
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
            var psi = new ProcessStartInfo
            {
                FileName = "dism.exe",
                Arguments = $"/Image:\"{imageMountPath}\" /Add-Package /PackagePath:\"{packagePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p is null) throw new InvalidOperationException("Failed to start DISM.");
            await p.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            result.Success = p.ExitCode == 0;
            result.Message = p.ExitCode == 0 ? "Package applied successfully." : $"DISM exited with code {p.ExitCode}.";
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
            var psi = new ProcessStartInfo
            {
                FileName = "dism.exe",
                Arguments = $"/Unmount-Image /MountDir:\"{imageMountPath}\" {commit}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p is null) throw new InvalidOperationException("Failed to start DISM.");
            await p.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            result.Success = p.ExitCode == 0;
            result.Message = p.ExitCode == 0 ? "Image dismounted successfully." : $"DISM exited with code {p.ExitCode}.";
            if (p.ExitCode == 0)
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
