using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WindowsUpdateAndPackageManager.Models;

namespace WindowsUpdateAndPackageManager.Infrastructure;

public interface IWindowsUpdateApi
{
    Task<IReadOnlyList<WindowsUpdate>> SearchAsync(string criteria, CancellationToken cancellationToken = default);
    Task DownloadAsync(IReadOnlyList<WindowsUpdate> updates, CancellationToken cancellationToken = default);
    Task<WindowsUpdateInstallResult> InstallAsync(IReadOnlyList<WindowsUpdate> updates, CancellationToken cancellationToken = default);
}
