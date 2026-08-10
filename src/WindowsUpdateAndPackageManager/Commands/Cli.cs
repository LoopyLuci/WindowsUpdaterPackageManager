using System;
using System.Threading.Tasks;
using System.CommandLine;
using System.CommandLine.Invocation;
using WindowsUpdateAndPackageManager.Core;
using WindowsUpdateAndPackageManager.Infrastructure;
using WindowsUpdateAndPackageManager.Models;

namespace WindowsUpdateAndPackageManager.Commands;

public static class Cli
{
    public static Task<int> Run(string[] args, IServiceProvider services)
    {
        var root = new RootCommand("Windows Update and Package Manager")
        {
            Description = "Personal-first, offline-capable update and package manager for Windows."
        };

        var repoOption = new Option<string?>("--repo", description: "Repository URL");

        var sync = new Command("sync", "Sync packages from a repository")
        {
            repoOption
        };
        sync.SetHandler<string?>((url) =>
        {
            try
            {
                var syncService = services.GetService(typeof(IRepoSync)) as IRepoSync;
                if (syncService is null)
                {
                    Console.WriteLine("IRepoSync is not registered.");
                    return;
                }
                var effective = string.IsNullOrWhiteSpace(url) ? "https://github.com/LoopyLuci/WindowsUpdateAndPackageManager" : url;
                var result = syncService.SyncAsync(effective).GetAwaiter().GetResult();
                Console.WriteLine($"Sync success: {result.Success}; packages={result.PackagesUpdated}; message={result.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Sync failed: {ex.Message}");
            }
        }, repoOption);
        root.AddCommand(sync);

        var install = new Command("install", "Install a package")
        {
            new Argument<string>("id") { Description = "Package ID" },
            repoOption
        };
        install.SetHandler<string, string?>((id, url) =>
        {
            try
            {
                var repoUrl = string.IsNullOrWhiteSpace(url) ? "https://github.com/LoopyLuci/WindowsUpdateAndPackageManager" : url;
                Console.WriteLine($"Install requested: {id} from {repoUrl}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Install failed: {ex.Message}");
            }
        }, new Argument<string>("id"), repoOption);
        root.AddCommand(install);

        var listInstalled = new Command("installed", "List installed packages");
        listInstalled.SetHandler(() =>
        {
            try
            {
                var pm = services.GetService(typeof(IPackageManager)) as IPackageManager;
                if (pm is null)
                {
                    Console.WriteLine("IPackageManager is not registered.");
                    return;
                }
                var installed = pm.ListInstalledAsync().GetAwaiter().GetResult();
                foreach (var p in installed)
                {
                    Console.WriteLine($"{p.Id}@{p.Version} - {p.DisplayName}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"List installed failed: {ex.Message}");
            }
        });
        root.AddCommand(listInstalled);

        var audit = new Command("audit", "Show audit log");
        audit.SetHandler(() =>
        {
            try
            {
                var auditor = services.GetService(typeof(IAuditor)) as IAuditor;
                if (auditor is null)
                {
                    Console.WriteLine("IAuditor is not registered.");
                    return;
                }
                var entries = auditor.QueryAsync().GetAwaiter().GetResult();
                foreach (var e in entries.Take(50))
                {
                    Console.WriteLine($"{e.Timestamp:u} | {e.Action} | {e.PackageId}@{e.Version} | success={e.Success} | {e.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Audit failed: {ex.Message}");
            }
        });
        root.AddCommand(audit);

        var rollback = new Command("rollback", "Rollback installed package");
        rollback.SetHandler(() =>
        {
            try
            {
                var rollbackManager = services.GetService(typeof(RollbackManager)) as RollbackManager;
                if (rollbackManager is null)
                {
                    Console.WriteLine("RollbackManager is not registered.");
                    return;
                }
                rollbackManager.RollbackAsync().GetAwaiter().GetResult();
                Console.WriteLine("Rollback attempted.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Rollback failed: {ex.Message}");
            }
        });
        root.AddCommand(rollback);

        var wu = new Command("windows-update", "Scan and apply Windows updates");
        wu.SetHandler(() =>
        {
            try
            {
                var manager = services.GetService(typeof(IWindowsUpdateManager)) as IWindowsUpdateManager;
                if (manager is null)
                {
                    Console.WriteLine("IWindowsUpdateManager is not registered.");
                    return;
                }
                var result = manager.ScanAndInstallAsync().GetAwaiter().GetResult();
                Console.WriteLine($"Success={result.Success}; found={result.UpdatesFound}; installed={result.UpdatesInstalled}; reboot={result.RebootRequired}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Windows Update failed: {ex.Message}");
            }
        });
        root.AddCommand(wu);

        var health = new Command("health", "Repo health summary");
        health.SetHandler(() =>
        {
            try
            {
                var repoUrl = "https://github.com/LoopyLuci/WindowsUpdateAndPackageManager";
                var validator = services.GetService(typeof(IManifestValidator)) as IManifestValidator;
                var repoClient = services.GetService(typeof(IRepoClient)) as IRepoClient;
                if (validator is null || repoClient is null)
                {
                    Console.WriteLine("Health check is not fully configured.");
                    return;
                }
                string? indexJson = null;
                try
                {
                    indexJson = repoClient.DownloadIndexAsync(repoUrl).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Repository connectivity failed: {ex.Message}");
                    return;
                }
                var valid = validator.ValidateAsync(indexJson).GetAwaiter().GetResult();
                Console.WriteLine($"Repository reachable: true; manifest valid: {valid}; schema: {valid}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Health check failed: {ex.Message}");
            }
        });
        root.AddCommand(health);

        try
        {
            return root.InvokeAsync(args);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Command failed: {ex.Message}");
            return Task.FromResult(1);
        }
    }
}
