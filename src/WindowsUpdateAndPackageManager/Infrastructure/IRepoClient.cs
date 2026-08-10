using System.Text.Json;
using WindowsUpdateAndPackageManager.Models;

namespace WindowsUpdateAndPackageManager.Infrastructure;

public interface IRepoClient
{
    Task<string?> DownloadIndexAsync(string repositoryUrl, CancellationToken cancellationToken = default);
    Task<Stream?> DownloadPackageAsync(string packageUrl, CancellationToken cancellationToken = default);
}
