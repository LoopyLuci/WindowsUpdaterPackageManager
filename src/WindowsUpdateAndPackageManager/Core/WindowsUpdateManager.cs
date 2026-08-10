using System.Diagnostics;
using WindowsUpdateAndPackageManager.Data;
using WindowsUpdateAndPackageManager.Infrastructure;
using WindowsUpdateAndPackageManager.Models;

namespace WindowsUpdateAndPackageManager.Core;

public sealed class WindowsUpdateManager : IWindowsUpdateManager
{
    private readonly IAuditStore _auditStore;

    public WindowsUpdateManager(IAuditStore auditStore)
    {
        _auditStore = auditStore;
    }

    public async Task<WindowsUpdateResult> ScanAndInstallAsync(CancellationToken cancellationToken = default)
    {
        var result = new WindowsUpdateResult();
        try
        {
            await _auditStore.AppendAsync(new AuditEntry
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTimeOffset.UtcNow,
                Action = "WindowsUpdate.Scan",
                Success = true,
                Message = "Windows Update scan stubbed."
            }, cancellationToken).ConfigureAwait(false);

            result.Success = true;
            result.UpdatesFound = 0;
            result.UpdatesInstalled = 0;
        }
        catch (Exception ex)
        {
            result.Message = ex.Message;
        }
        return result;
    }
}
