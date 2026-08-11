namespace WindowsUpdateAndPackageManager.Core;

public interface IDriverVerifier
{
    Task<bool> VerifyAsync(Models.DriverPackageManifest manifest, CancellationToken cancellationToken = default);
}
