using System.Threading;
using System.Threading.Tasks;

namespace WindowsUpdateAndPackageManager.Core;

public interface IRegistrySyncService
{
    Task SyncAsync(string ownerRepo, string branch, CancellationToken cancellationToken = default);
}
