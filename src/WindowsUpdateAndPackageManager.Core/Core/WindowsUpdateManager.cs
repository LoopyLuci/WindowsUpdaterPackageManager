using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
    private readonly string? _offlineScanCachePath;

    public WindowsUpdateManager(IAuditStore auditStore, IWindowsUpdateApi windowsUpdateApi, string? offlineScanCachePath = null)
    {
        _auditStore = auditStore;
        _windowsUpdateApi = windowsUpdateApi;
        _offlineScanCachePath = offlineScanCachePath;
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

            if (result.UpdatesFound > 0 && !offlineScan)
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
                result.Message = offlineScan ? $"Found {result.UpdatesFound} updates available." : "No updates were found.";
            }

            if (offlineScan && !string.IsNullOrWhiteSpace(_offlineScanCachePath))
            {
                await CacheOfflineScanResultAsync(updates, _offlineScanCachePath, cancellationToken).ConfigureAwait(false);
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

    private static async Task CacheOfflineScanResultAsync(IReadOnlyList<WindowsUpdate> updates, string cachePath, CancellationToken cancellationToken)
    {
        try
        {
            var cacheDir = Path.GetDirectoryName(cachePath);
            if (!string.IsNullOrWhiteSpace(cacheDir))
            {
                Directory.CreateDirectory(cacheDir);
            }

            var lines = new List<string>
            {
                $"# Offline scan result: {DateTimeOffset.UtcNow:u}",
                $"# Updates found: {updates.Count}",
                $"# Title|SizeBytes|IsDriver"
            };

            foreach (var u in updates)
            {
                var size = u.SizeBytes.HasValue ? u.SizeBytes.Value.ToString() : "Unknown";
                lines.Add($"{u.Title}|{size}|{u.IsDriver}");
            }

            await File.WriteAllLinesAsync(cachePath, lines, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Cache write failures should not fail the scan.
        }
    }
}
