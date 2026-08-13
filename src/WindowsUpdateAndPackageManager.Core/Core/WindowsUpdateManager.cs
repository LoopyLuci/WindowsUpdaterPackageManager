using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WindowsUpdateAndPackageManager.Data;
using WindowsUpdateAndPackageManager.Infrastructure;
using WindowsUpdateAndPackageManager.Models;

namespace WindowsUpdateAndPackageManager.Core;

public sealed class WindowsUpdateManager : IWindowsUpdateManager
{
    private readonly IAuditStore _auditStore;
    private readonly IWindowsUpdateApi _windowsUpdateApi;

    public WindowsUpdateManager(IAuditStore auditStore, IWindowsUpdateApi windowsUpdateApi)
    {
        _auditStore = auditStore;
        _windowsUpdateApi = windowsUpdateApi;
    }

    public async Task<WindowsUpdateResult> ScanAndInstallAsync(bool driversOnly = false, bool offlineScan = false, CancellationToken cancellationToken = default)
    {
        var result = new WindowsUpdateResult();
        try
        {
            var updates = await _windowsUpdateApi.SearchAsync("IsInstalled=0", cancellationToken).ConfigureAwait(false);
            if (driversOnly)
            {
                updates = updates.Where(u => u.IsDriver).ToList();
            }

            result.UpdatesFound = updates.Count;

            if (result.UpdatesFound > 0)
            {
                await _windowsUpdateApi.DownloadAsync(updates, cancellationToken).ConfigureAwait(false);
                var installResult = await _windowsUpdateApi.InstallAsync(updates, cancellationToken).ConfigureAwait(false);

                result.Success = installResult.Success;
                result.UpdatesInstalled = installResult.InstalledCount;
                result.RebootRequired = installResult.RebootRequired;
                result.Message = installResult.Message ?? $"Installed {installResult.InstalledCount} of {result.UpdatesFound} updates.";
            }
            else
            {
                result.Success = true;
                result.Message = "No updates were found.";
            }

            await _auditStore.AppendAsync(new AuditEntry
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTimeOffset.UtcNow,
                Action = offlineScan ? "WindowsUpdate.OfflineScan" : "WindowsUpdate.Scan",
                Success = result.Success,
                Message = result.Message
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = ex.Message;
            await _auditStore.AppendAsync(new AuditEntry
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTimeOffset.UtcNow,
                Action = offlineScan ? "WindowsUpdate.OfflineScan" : "WindowsUpdate.Scan",
                Success = false,
                Message = ex.Message
            }, cancellationToken).ConfigureAwait(false);
        }
        return result;
    }
}
