using WindowsUpdateAndPackageManager.Models;

namespace WindowsUpdateAndPackageManager.Infrastructure;

public interface IManifestValidator
{
    Task<RepositoryIndex?> ParseAsync(string json, CancellationToken cancellationToken = default);
    Task<bool> ValidateAsync(string json, CancellationToken cancellationToken = default);
    Task<bool> VerifyPackageIntegrityAsync(string packagePath, string? expectedSha256, CancellationToken cancellationToken = default);
}
