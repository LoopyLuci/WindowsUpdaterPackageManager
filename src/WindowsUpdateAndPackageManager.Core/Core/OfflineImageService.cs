using System.Diagnostics;

namespace WindowsUpdateAndPackageManager.Core;

public sealed class OfflineImageService : IOfflineImageService
{
    private readonly IDismProcessRunner _dism;
    private readonly OfflineServiceOptions _options;

    public OfflineImageService(IDismProcessRunner dism, OfflineServiceOptions? options = null)
    {
        _dism = dism;
        _options = options ?? new OfflineServiceOptions();
    }

    public async Task<OfflineImageResult> MountOrOpenAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                return new OfflineImageResult { Success = false, Message = "Image path does not exist." };
            }

            var args = $"/Mount-Image /ImageFile:\"{imagePath}\" /MountDir:\"{GetMountPath(imagePath)}\" /ReadOnly";
            var exit = await RunWithRetryAsync("dism.exe", args, cancellationToken);
            return new OfflineImageResult { Success = exit == 0, MountPath = exit == 0 ? GetMountPath(imagePath) : null, Message = exit == 0 ? null : $"DISM exited with code {exit}" };
        }
        catch (Exception ex)
        {
            return new OfflineImageResult { Success = false, Message = ex.Message };
        }
    }

    public async Task<OfflineImageResult> ApplyPackageAsync(string mountPath, string packagePath, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(mountPath) || !Directory.Exists(mountPath))
            {
                return new OfflineImageResult { Success = false, Message = "Mount path does not exist." };
            }

            if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
            {
                return new OfflineImageResult { Success = false, Message = "Package path does not exist." };
            }

            var args = $"/Image:\"{mountPath}\" /Add-Package /PackagePath:\"{packagePath}\"";
            var exit = await RunWithRetryAsync("dism.exe", args, cancellationToken);
            return new OfflineImageResult { Success = exit == 0, Message = exit == 0 ? null : $"DISM exited with code {exit}" };
        }
        catch (Exception ex)
        {
            return new OfflineImageResult { Success = false, Message = ex.Message };
        }
    }

    public async Task<OfflineImageResult> DismountAsync(string mountPath, bool commit, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(mountPath) || !Directory.Exists(mountPath))
            {
                return new OfflineImageResult { Success = false, Message = "Mount path does not exist." };
            }

            var commitArg = commit ? "/Commit-Image" : "/Discard-Image";
            var args = $"/Unmount-Image /MountDir:\"{mountPath}\" {commitArg}";
            var exit = await RunWithRetryAsync("dism.exe", args, cancellationToken);
            return new OfflineImageResult { Success = exit == 0, Message = exit == 0 ? null : $"DISM exited with code {exit}" };
        }
        catch (Exception ex)
        {
            return new OfflineImageResult { Success = false, Message = ex.Message };
        }
    }

    private async Task<int> RunWithRetryAsync(string fileName, string arguments, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt <= _options.DismMaxRetries; attempt++)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.DismTimeoutSeconds));
            var exit = await _dism.RunAsync(fileName, arguments, cts.Token);
            if (exit == 0) return 0;
            if (attempt == _options.DismMaxRetries) return exit;
            await Task.Delay(_options.DismRetryDelayMs, cancellationToken);
        }
        return -1;
    }

    private static string GetMountPath(string imagePath)
    {
        var name = Path.GetFileNameWithoutExtension(imagePath) ?? "mount";
        return Path.Combine(Path.GetTempPath(), "wupm-mounts", name);
    }
}
