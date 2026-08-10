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

        var repoOption = new Option<string?>("--repo", getDefaultValue: () => null, description: "Repository URL");

        var sync = new Command("sync", "Sync packages from a repository")
        {
            repoOption
        };
        sync.SetHandler<string?>((url) =>
        {
            var syncService = services.GetService(typeof(IRepoSync)) as IRepoSync;
            if (syncService is null) return;
            var effective = string.IsNullOrWhiteSpace(url) ? "https://github.com/LoopyLuci/WindowsUpdateAndPackageManager" : url;
            var result = syncService.SyncAsync(effective).GetAwaiter().GetResult();
            Console.WriteLine($"Sync success: {result.Success}; packages={result.PackagesUpdated}; message={result.Message}");
        }, repoOption);
        root.AddCommand(sync);

        var install = new Command("install", "Install a package")
        {
            new Argument<string>("id") { Description = "Package ID" },
            repoOption
        };
        install.SetHandler<string, string?>((id, url) =>
        {
            var packages = new List<PackageManifest>();
            var repoUrl = string.IsNullOrWhiteSpace(url) ? "https://github.com/LoopyLuci/WindowsUpdateAndPackageManager" : url;
            // In real flow, first sync, then resolve package by id.
            Console.WriteLine($"Install requested: {id} from {repoUrl}");
        }, new Argument<string>("id"), repoOption);
        root.AddCommand(install);

        var listInstalled = new Command("installed", "List installed packages");
        listInstalled.SetHandler(() =>
        {
            var pm = services.GetService(typeof(IPackageManager)) as IPackageManager;
            if (pm is null) return;
            var installed = pm.ListInstalledAsync().GetAwaiter().GetResult();
            foreach (var p in installed)
            {
                Console.WriteLine($"{p.Id}@{p.Version} - {p.DisplayName}");
            }
        });
        root.AddCommand(listInstalled);

        var audit = new Command("audit", "Show audit log");
        audit.SetHandler(() =>
        {
            var auditor = services.GetService(typeof(IAuditor)) as IAuditor;
            if (auditor is null) return;
            var entries = auditor.QueryAsync().GetAwaiter().GetResult();
            foreach (var e in entries.Take(50))
            {
                Console.WriteLine($"{e.Timestamp:u} | {e.Action} | {e.PackageId}@{e.Version} | success={e.Success} | {e.Message}");
            }
        });
        root.AddCommand(audit);

        var rollback = new Command("rollback", "Rollback installed package");
        rollback.SetHandler(() =>
        {
            var rollbackManager = services.GetService(typeof(RollbackManager)) as RollbackManager;
            if (rollbackManager is null) return;
            rollbackManager.RollbackAsync().GetAwaiter().GetResult();
            Console.WriteLine("Rollback attempted.");
        });
        root.AddCommand(rollback);

        var wu = new Command("windows-update", "Scan and apply Windows updates");
        wu.SetHandler(() =>
        {
            var manager = services.GetService(typeof(IWindowsUpdateManager)) as IWindowsUpdateManager;
            if (manager is null) return;
            var result = manager.ScanAndInstallAsync().GetAwaiter().GetResult();
            Console.WriteLine($"Success={result.Success}; found={result.UpdatesFound}; installed={result.UpdatesInstalled}; reboot={result.RebootRequired}");
        });
        root.AddCommand(wu);

        var health = new Command("health", "Repo health summary");
        health.SetHandler(() =>
        {
            Console.WriteLine("Repository connectivity and manifest validation summary would appear here.");
        });
        root.AddCommand(health);

        return root.InvokeAsync(args);
    }
}
