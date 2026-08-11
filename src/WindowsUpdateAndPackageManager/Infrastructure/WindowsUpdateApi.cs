using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WindowsUpdateAndPackageManager.Models;

namespace WindowsUpdateAndPackageManager.Infrastructure;

public sealed class WindowsUpdateApi : IWindowsUpdateApi
{
    public async Task<IReadOnlyList<WindowsUpdate>> SearchAsync(string criteria, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(criteria)) throw new ArgumentException("Criteria is required.", nameof(criteria));

        return await Task.Run(() =>
        {
            var updateSessionType = Type.GetTypeFromProgID("Microsoft.Update.Session", true)
                ?? throw new InvalidOperationException("Windows Update COM runtime is not available on this system.");

            dynamic session = Activator.CreateInstance(updateSessionType)!;
            dynamic searcher = session.CreateUpdateSearcher();
            dynamic searchResult = searcher.Search(criteria);

            var updates = new List<WindowsUpdate>();
            for (var i = 0; i < searchResult.Updates.Count; i++)
            {
                dynamic update = searchResult.Updates.Item(i);
                var categories = new List<string>();
                try
                {
                    for (var c = 0; c < update.Categories.Count; c++)
                    {
                        dynamic cat = update.Categories.Item(c);
                        var id = cat.CategoryID;
                        if (!string.IsNullOrWhiteSpace(id))
                        {
                            categories.Add(id);
                        }
                    }
                }
                catch { }

                updates.Add(new WindowsUpdate
                {
                    Id = i,
                    Title = update.Title,
                    Description = update.Description,
                    SupportUrl = update.SupportUrl,
                    SizeBytes = update.MaxDownloadSize,
                    CategoryIds = categories
                });
            }

            return updates;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task DownloadAsync(IReadOnlyList<WindowsUpdate> updates, CancellationToken cancellationToken = default)
    {
        if (updates is null || updates.Count == 0) return;

        await Task.Run(() =>
        {
            var updateSessionType = Type.GetTypeFromProgID("Microsoft.Update.Session", true)
                ?? throw new InvalidOperationException("Windows Update COM runtime is not available on this system.");

            dynamic session = Activator.CreateInstance(updateSessionType)!;
            dynamic downloader = session.CreateUpdateDownloader();

            foreach (dynamic update in ResolveComUpdates(session, updates))
            {
                downloader.Updates.Add(update);
            }

            downloader.Download();
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<WindowsUpdateInstallResult> InstallAsync(IReadOnlyList<WindowsUpdate> updates, CancellationToken cancellationToken = default)
    {
        if (updates is null || updates.Count == 0)
        {
            return await Task.FromResult(new WindowsUpdateInstallResult
            {
                Success = true,
                InstalledCount = 0,
                RebootRequired = false,
                Message = "No updates to install."
            });
        }

        var installResult = await Task.Run(() =>
        {
            var updateSessionType = Type.GetTypeFromProgID("Microsoft.Update.Session", true)
                ?? throw new InvalidOperationException("Windows Update COM runtime is not available on this system.");

            dynamic session = Activator.CreateInstance(updateSessionType)!;
            dynamic installer = session.CreateUpdateInstaller();

            foreach (dynamic update in ResolveComUpdates(session, updates))
            {
                installer.Updates.Add(update);
            }

            dynamic result = installer.Install();
            var installed = result.ResultCode is 2 or 3 ? updates.Count : 0;

            return new WindowsUpdateInstallResult
            {
                Success = installed == updates.Count,
                InstalledCount = installed,
                RebootRequired = result.RebootRequired,
                Message = installed == updates.Count
                    ? $"Installed {installed} update(s)."
                    : $"Installed {installed} of {updates.Count} updates."
            };
        }, cancellationToken).ConfigureAwait(false);

        return installResult;
    }

    private static IEnumerable<dynamic> ResolveComUpdates(dynamic session, IReadOnlyList<WindowsUpdate> updates)
    {
        dynamic searcher = session.CreateUpdateSearcher();
        dynamic searchResult = searcher.Search("IsInstalled=0");

        var lookup = new Dictionary<int, dynamic>();
        for (var i = 0; i < searchResult.Updates.Count; i++)
        {
            lookup[i] = searchResult.Updates.Item(i);
        }

        foreach (var update in updates)
        {
            if (lookup.TryGetValue(update.Id, out var comUpdate))
            {
                yield return comUpdate;
            }
        }
    }
}
