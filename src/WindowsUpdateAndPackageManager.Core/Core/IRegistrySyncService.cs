using System.Threading;
using System.Threading.Tasks;

namespace WindowsUpdateAndPackageManager.Core;

public interface IRegistrySyncService
{
    Task<RegistrySyncResult> SyncAsync(string ownerRepo, string branch, CancellationToken cancellationToken = default);
}

public sealed class RegistrySyncResult
{
    public int Added { get; set; }
    public int Replaced { get; set; }
    public int Skipped { get; set; }
    public List<string> Conflicts { get; } = new();
}
