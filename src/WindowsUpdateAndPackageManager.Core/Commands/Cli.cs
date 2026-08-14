using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.Extensions.DependencyInjection;
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
        var noTelemetryOption = new Option<bool>("--no-telemetry", description: "Disable diagnostic telemetry") { Arity = ArgumentArity.ZeroOrOne };
        var logFileOption = new Option<string?>("--log-file", description: "Path to structured log file") { Arity = ArgumentArity.ZeroOrOne };

        root.AddGlobalOption(noTelemetryOption);
        root.AddGlobalOption(logFileOption);

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
                var repoClient = services.GetService(typeof(IRepoClient)) as IRepoClient;
                var packageManager = services.GetService(typeof(IPackageManager)) as IPackageManager;
                if (repoClient is null || packageManager is null)
                {
                    Console.WriteLine("Install is not fully configured.");
                    return;
                }

                var indexJsonTask = repoClient.DownloadIndexAsync(repoUrl);
                var indexJson = indexJsonTask.GetAwaiter().GetResult();
                if (string.IsNullOrWhiteSpace(indexJson))
                {
                    Console.WriteLine("Repository index is empty.");
                    return;
                }

                var validator = services.GetService(typeof(IManifestValidator)) as IManifestValidator;
                if (validator is null)
                {
                    Console.WriteLine("Manifest validator is not configured.");
                    return;
                }

                var valid = validator.ValidateAsync(indexJson).GetAwaiter().GetResult();
                if (!valid)
                {
                    Console.WriteLine("Repository manifest is invalid.");
                    return;
                }

                var index = validator.ParseAsync(indexJson).GetAwaiter().GetResult();
                if (index is null || index.Packages is null || index.Packages.Count == 0)
                {
                    Console.WriteLine("Repository manifest is empty.");
                    return;
                }

                var package = index.Packages.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
                if (package is null)
                {
                    Console.WriteLine($"Package '{id}' was not found in the repository.");
                    return;
                }

                Console.WriteLine($"Installing {package.Id}@{package.Version}...");
                var result = packageManager.InstallAsync(package).GetAwaiter().GetResult();
                Console.WriteLine($"Install result: success={result.Success}; version={result.InstalledVersion}; message={result.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Install failed: {ex.Message}");
            }
        }, new Argument<string>("id"), repoOption);
        root.AddCommand(install);

        var search = new Command("search", "Search repository packages")
        {
            new Argument<string?>("query") { Description = "Search query" },
            repoOption
        };
        search.SetHandler<string?, string?>((query, url) =>
        {
            try
            {
                var repoUrl = string.IsNullOrWhiteSpace(url) ? "https://github.com/LoopyLuci/WindowsUpdateAndPackageManager" : url;
                var repoClient = services.GetService(typeof(IRepoClient)) as IRepoClient;
                var repoSync = services.GetService(typeof(IRepoSync)) as IRepoSync;
                if (repoClient is null || repoSync is null)
                {
                    Console.WriteLine("Search is not fully configured.");
                    return;
                }

                var packages = repoSync.ListAsync(repoUrl).GetAwaiter().GetResult();
                var effective = string.IsNullOrWhiteSpace(query) ? string.Empty : query.Trim();
                foreach (var p in packages)
                {
                    if (!string.IsNullOrWhiteSpace(effective) &&
                        !(p.Id.Contains(effective, StringComparison.OrdinalIgnoreCase) ||
                          (p.DisplayName ?? string.Empty).Contains(effective, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    Console.WriteLine($"{p.Id}@{p.Version} | {p.DisplayName} | driver={p.IsDriver} | min={p.MinWindowsVersion} | max={p.MaxWindowsVersion}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Search failed: {ex.Message}");
            }
        }, new Argument<string?>("query") { Arity = ArgumentArity.ZeroOrOne }, repoOption);
        root.AddCommand(search);

        var listAvailable = new Command("list-available", "List available repository packages");
        listAvailable.SetHandler(() =>
        {
            try
            {
                var repoSync = services.GetService(typeof(IRepoSync)) as IRepoSync;
                if (repoSync is null)
                {
                    Console.WriteLine("IRepoSync is not registered.");
                    return;
                }

                var packages = repoSync.ListAsync("https://github.com/LoopyLuci/WindowsUpdateAndPackageManager").GetAwaiter().GetResult();
                foreach (var p in packages)
                {
                    Console.WriteLine($"{p.Id}@{p.Version} | {p.DisplayName} | driver={p.IsDriver}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"List available failed: {ex.Message}");
            }
        });
        root.AddCommand(listAvailable);

        var policyAllow = new Command("policy-allow", "Allow a package in policy");
        policyAllow.SetHandler<string>((id) =>
        {
            try
            {
                var policy = services.GetService(typeof(IPolicyEngine)) as IPolicyEngine;
                if (policy is null)
                {
                    Console.WriteLine("Policy engine is not configured.");
                    return;
                }

                var allowlist = policy as Infrastructure.AllowlistPolicyEngine;
                if (allowlist is null)
                {
                    Console.WriteLine("Current policy engine does not support dynamic allowlist changes.");
                    return;
                }

                allowlist.Allow(id);
                Console.WriteLine($"Policy updated: '{id}' is now allowed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Policy update failed: {ex.Message}");
            }
        }, new Argument<string>("id"));
        root.AddCommand(policyAllow);

        var policyDeny = new Command("policy-deny", "Deny a package in policy");
        policyDeny.SetHandler<string>((id) =>
        {
            try
            {
                var allowlist = services.GetService(typeof(IPolicyEngine)) as Infrastructure.AllowlistPolicyEngine;
                if (allowlist is null)
                {
                    Console.WriteLine("Current policy engine does not support dynamic allowlist changes.");
                    return;
                }

                allowlist.Deny(id);
                Console.WriteLine($"Policy updated: '{id}' is now denied.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Policy update failed: {ex.Message}");
            }
        }, new Argument<string>("id"));
        root.AddCommand(policyDeny);

        var driverUpdate = new Command("driver-update", "Scan and install Windows driver updates only");
        var offlineScanOption = new Option<bool>("--offline-scan", description: "Scan without downloading/installing updates");
        driverUpdate.AddOption(offlineScanOption);
        driverUpdate.SetHandler((bool offlineScan) =>
        {
            try
            {
                var manager = services.GetService(typeof(IWindowsUpdateManager)) as IWindowsUpdateManager;
                if (manager is null)
                {
                    Console.WriteLine("IWindowsUpdateManager is not registered.");
                    return;
                }

                var result = manager.ScanAndInstallAsync(driversOnly: true, offlineScan: offlineScan).GetAwaiter().GetResult();
                Console.WriteLine($"Success={result.Success}; found={result.UpdatesFound}; installed={result.UpdatesInstalled}; reboot={result.RebootRequired}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Driver update failed: {ex.Message}");
            }
        }, offlineScanOption);
        root.AddCommand(driverUpdate);

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
        var rollbackDryRunOption = new Option<bool>("--dry-run", description: "Show what would be rolled back without making changes");
        rollback.AddOption(rollbackDryRunOption);
        var rollbackIdOption = new Option<string?>("--id", description: "Package ID to rollback");
        rollback.AddOption(rollbackIdOption);
        rollback.SetHandler((bool dryRun, string? packageId) =>
        {
            try
            {
                var rollbackManager = services.GetService(typeof(RollbackManager)) as RollbackManager;
                if (rollbackManager is null)
                {
                    Console.WriteLine("RollbackManager is not registered.");
                    return;
                }

                if (dryRun)
                {
                    var installed = rollbackManager.GetInstalledAsync().GetAwaiter().GetResult();
                    var targets = packageId is null
                        ? installed.ToList()
                        : installed.Where(x => x.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase)).ToList();

                    if (targets.Count == 0)
                    {
                        Console.WriteLine("No matching installed packages.");
                        return;
                    }

                    Console.WriteLine($"Would rollback {targets.Count} package(s):");
                    foreach (var pkg in targets)
                    {
                        Console.WriteLine($" - {pkg.Id}@{pkg.Version} -> {pkg.UninstallCommand}");
                    }
                    return;
                }

                rollbackManager.RollbackAsync(packageId).GetAwaiter().GetResult();
                Console.WriteLine("Rollback attempted.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Rollback failed: {ex.Message}");
            }
        }, rollbackDryRunOption, rollbackIdOption);
        root.AddCommand(rollback);

        var wu = new Command("windows-update", "Scan and apply Windows updates");
        var wuOfflineScanOption = new Option<bool>("--offline-scan", description: "Scan without downloading/installing updates");
        wu.AddOption(wuOfflineScanOption);
        wu.SetHandler((bool offlineScan) =>
        {
            try
            {
                var manager = services.GetService(typeof(IWindowsUpdateManager)) as IWindowsUpdateManager;
                if (manager is null)
                {
                    Console.WriteLine("IWindowsUpdateManager is not registered.");
                    return;
                }
                var result = manager.ScanAndInstallAsync(offlineScan: offlineScan).GetAwaiter().GetResult();
                Console.WriteLine($"Success={result.Success}; found={result.UpdatesFound}; installed={result.UpdatesInstalled}; reboot={result.RebootRequired}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Windows Update failed: {ex.Message}");
            }
        }, wuOfflineScanOption);
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

        var cache = new Command("cache", "Manage offline cache");
        var cacheList = new Command("list", "List cached packages");
        cacheList.SetHandler(() =>
        {
            try
            {
                var cacheManager = services.GetService(typeof(ICacheManager)) as ICacheManager;
                if (cacheManager is null)
                {
                    Console.WriteLine("Cache manager is not configured.");
                    return;
                }

                var rootDir = cacheManager.GetCacheRootAsync().GetAwaiter().GetResult();
                if (!Directory.Exists(rootDir))
                {
                    Console.WriteLine("Cache is empty.");
                    return;
                }

                var dirs = Directory.EnumerateDirectories(rootDir);
                if (!dirs.Any())
                {
                    Console.WriteLine("Cache is empty.");
                    return;
                }

                foreach (var dir in dirs)
                {
                    var name = Path.GetFileName(dir);
                    var files = Directory.EnumerateFiles(dir);
                    var size = files.Sum(f => new FileInfo(f).Length);
                    Console.WriteLine($"{name} | files={files.Count()} | bytes={size}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cache list failed: {ex.Message}");
            }
        });
        cache.AddCommand(cacheList);

        var cachePrune = new Command("prune", "Prune all cached packages");
        cachePrune.SetHandler(() =>
        {
            try
            {
                var cacheManager = services.GetService(typeof(ICacheManager)) as ICacheManager;
                if (cacheManager is null)
                {
                    Console.WriteLine("Cache manager is not configured.");
                    return;
                }

                cacheManager.PruneAsync().GetAwaiter().GetResult();
                Console.WriteLine("Cache pruned.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cache prune failed: {ex.Message}");
            }
        });
        cache.AddCommand(cachePrune);

        root.AddCommand(cache);

        var plugin = new Command("plugin", "Manage plugins");
        var pluginList = new Command("list", "List loaded plugins");
        pluginList.SetHandler(() =>
        {
            try
            {
                var pluginManager = services.GetService(typeof(PluginManager)) as PluginManager;
                if (pluginManager is null)
                {
                    Console.WriteLine("Plugin manager is not configured.");
                    return;
                }

                pluginManager.LoadAsync().GetAwaiter().GetResult();
                if (pluginManager.Plugins.Count == 0)
                {
                    Console.WriteLine("No plugins loaded.");
                    return;
                }

                foreach (var p in pluginManager.Plugins)
                {
                    Console.WriteLine($"{p.Name} v{p.Version}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Plugin list failed: {ex.Message}");
            }
        });
        plugin.AddCommand(pluginList);

        var pluginRegistry = new Command("registry", "Manage plugin registry");
        var pluginRegistryList = new Command("list", "List registry entries");
        pluginRegistryList.SetHandler(() =>
        {
            try
            {
                var registry = services.GetService(typeof(IPluginRegistry)) as IPluginRegistry;
                if (registry is null)
                {
                    Console.WriteLine("Plugin registry is not configured.");
                    return;
                }

                var entries = registry.ListAsync().GetAwaiter().GetResult();
                if (entries.Count == 0)
                {
                    Console.WriteLine("No plugins in registry.");
                    return;
                }

                foreach (var e in entries)
                {
                    Console.WriteLine($"{e.Name}@{e.Version} | enabled={e.Enabled} | path={e.Path}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Plugin registry list failed: {ex.Message}");
            }
        });
        pluginRegistry.AddCommand(pluginRegistryList);

        var pluginRegistryAdd = new Command("add", "Add a plugin to registry");
        var pluginAddName = new Option<string>("--name") { Description = "Plugin name" };
        var pluginAddVersion = new Option<string>("--version") { Description = "Plugin version" };
        var pluginAddPath = new Option<string>("--path") { Description = "Path to plugin DLL" };
        var pluginAddDeps = new Option<string>("--dependencies") { Description = "Comma-separated plugin dependencies" };
        pluginRegistryAdd.AddOption(pluginAddName);
        pluginRegistryAdd.AddOption(pluginAddVersion);
        pluginRegistryAdd.AddOption(pluginAddPath);
        pluginRegistryAdd.AddOption(pluginAddDeps);
        pluginRegistryAdd.SetHandler<string, string, string?, string?>((name, version, path, dependencies) =>
        {
            try
            {
                var registry = services.GetService(typeof(IPluginRegistry)) as IPluginRegistry;
                if (registry is null)
                {
                    Console.WriteLine("Plugin registry is not configured.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    Console.WriteLine("Plugin path is required and must exist.");
                    return;
                }

                registry.AddAsync(name, version, path, dependencies ?? string.Empty).GetAwaiter().GetResult();
                Console.WriteLine($"Registered plugin: {name}@{version}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Plugin registry add failed: {ex.Message}");
            }
        }, pluginAddName, pluginAddVersion, pluginAddPath, pluginAddDeps);
        pluginRegistry.AddCommand(pluginRegistryAdd);

        var pluginRegistryUpdate = new Command("update", "Update a plugin in registry");
        var pluginUpdateName = new Option<string>("--name") { Description = "Plugin name" };
        var pluginUpdateVersion = new Option<string>("--version") { Description = "Plugin version" };
        var pluginUpdatePath = new Option<string>("--path") { Description = "Path to plugin DLL" };
        var pluginUpdateDeps = new Option<string>("--dependencies") { Description = "Comma-separated plugin dependencies" };
        pluginRegistryUpdate.AddOption(pluginUpdateName);
        pluginRegistryUpdate.AddOption(pluginUpdateVersion);
        pluginRegistryUpdate.AddOption(pluginUpdatePath);
        pluginRegistryUpdate.AddOption(pluginUpdateDeps);
        pluginRegistryUpdate.SetHandler<string, string, string?, string?>((name, version, path, dependencies) =>
        {
            try
            {
                var registry = services.GetService(typeof(IPluginRegistry)) as IPluginRegistry;
                if (registry is null)
                {
                    Console.WriteLine("Plugin registry is not configured.");
                    return;
                }

                var entries = registry.ListAsync().GetAwaiter().GetResult();
                var existing = entries.FirstOrDefault(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (existing is null)
                {
                    Console.WriteLine("Plugin not found in registry.");
                    return;
                }

                var effectivePath = string.IsNullOrWhiteSpace(path) ? existing.Path : path;
                if (!string.IsNullOrWhiteSpace(path) && !File.Exists(effectivePath))
                {
                    Console.WriteLine("Plugin path is required and must exist.");
                    return;
                }

                registry.RemoveAsync(name).GetAwaiter().GetResult();
                registry.AddAsync(name, version ?? existing.Version, effectivePath, dependencies ?? existing.Dependencies).GetAwaiter().GetResult();
                Console.WriteLine($"Updated plugin: {name}@{version ?? existing.Version}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Plugin registry update failed: {ex.Message}");
            }
        }, pluginUpdateName, pluginUpdateVersion, pluginUpdatePath, pluginUpdateDeps);
        pluginRegistry.AddCommand(pluginRegistryUpdate);

        var pluginRegistryRemove = new Command("remove", "Remove a plugin from registry");
        var pluginRemoveName = new Option<string>("--name") { Description = "Plugin name" };
        var pluginRemoveConfirm = new Option<bool>("--confirm", description: "Skip confirmation prompt") { Arity = ArgumentArity.ZeroOrOne };
        pluginRegistryRemove.AddOption(pluginRemoveName);
        pluginRegistryRemove.AddOption(pluginRemoveConfirm);
        pluginRegistryRemove.SetHandler<string, bool>((name, confirm) =>
        {
            try
            {
                if (!confirm)
                {
                    Console.Write($"Remove plugin '{name}'? (y/N): ");
                    var answer = Console.ReadLine();
                    if (string.IsNullOrWhiteSpace(answer) || !answer.StartsWith("y", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("Cancelled.");
                        return;
                    }
                }

                var registry = services.GetService(typeof(IPluginRegistry)) as IPluginRegistry;
                if (registry is null)
                {
                    Console.WriteLine("Plugin registry is not configured.");
                    return;
                }

                registry.RemoveAsync(name).GetAwaiter().GetResult();
                Console.WriteLine($"Removed plugin: {name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Plugin registry remove failed: {ex.Message}");
            }
        }, pluginRemoveName, pluginRemoveConfirm);
        pluginRegistry.AddCommand(pluginRegistryRemove);

        plugin.AddCommand(pluginRegistry);

        var pluginVerify = new Command("verify", "Verify a plugin package hash");
        var pluginVerifyPath = new Option<string>("--path") { Description = "Path to plugin DLL" };
        pluginVerify.AddOption(pluginVerifyPath);
        pluginVerify.SetHandler<string?>((path) =>
        {
            try
            {
                var registry = services.GetService(typeof(IPluginRegistry)) as IPluginRegistry;
                if (registry is null)
                {
                    Console.WriteLine("Plugin registry is not configured.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    Console.WriteLine("Plugin path is required and must exist.");
                    return;
                }

                var hash = registry.ComputeSha256Async(path).GetAwaiter().GetResult();
                if (hash is null)
                {
                    Console.WriteLine("Verification failed.");
                    return;
                }

                var verifier = services.GetService(typeof(IPluginVerifier)) as IPluginVerifier;
                var trusted = verifier is null || verifier.VerifyAsync(path).GetAwaiter().GetResult();
                Console.WriteLine($"SHA256: {hash}");
                Console.WriteLine(trusted ? "Verification: passed" : "Verification: untrusted - review before use");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Plugin verify failed: {ex.Message}");
            }
        }, pluginVerifyPath);
        plugin.AddCommand(pluginVerify);

        var pluginInstall = new Command("install", "Install a plugin from a path");
        var pluginInstallPath = new Option<string>("--path") { Description = "Path to plugin DLL" };
        pluginInstall.AddOption(pluginInstallPath);
        pluginInstall.SetHandler<string?>((path) =>
        {
            try
            {
                var registry = services.GetService(typeof(IPluginRegistry)) as IPluginRegistry;
                if (registry is null)
                {
                    Console.WriteLine("Plugin registry is not configured.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    Console.WriteLine("Plugin path is required and must exist.");
                    return;
                }

                var name = Path.GetFileNameWithoutExtension(path);
                var version = "1.0.0";
                registry.AddAsync(name, version, path, dependencies: string.Empty).GetAwaiter().GetResult();
                Console.WriteLine($"Installed plugin: {name}@{version}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Plugin install failed: {ex.Message}");
            }
        }, pluginInstallPath);
        plugin.AddCommand(pluginInstall);

        root.AddCommand(plugin);

        var marketplace = new Command("marketplace", "Browse plugins");
        var marketplaceSearch = new Command("search", "Search plugins by name");
        var marketplaceSearchTerm = new Argument<string>("term") { Description = "Search term" };
        marketplaceSearch.AddArgument(marketplaceSearchTerm);
        marketplaceSearch.SetHandler<string>((term) =>
        {
            try
            {
                var repoSync = services.GetService(typeof(IRepoSync)) as IRepoSync;
                if (repoSync is null)
                {
                    Console.WriteLine("IRepoSync is not configured.");
                    return;
                }

                var repoUrl = "https://github.com/LoopyLuci/WindowsUpdatePackageManager-plugins";
                var results = repoSync.ListAsync(repoUrl).GetAwaiter().GetResult();
                var filtered = string.IsNullOrWhiteSpace(term)
                    ? results
                    : results.Where(p => p.Id.Contains(term, StringComparison.OrdinalIgnoreCase) || p.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();

                if (filtered.Count == 0)
                {
                    Console.WriteLine("No plugins found.");
                    return;
                }

                foreach (var p in filtered)
                {
                    Console.WriteLine($"{p.Id}@{p.Version} | {p.DisplayName}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Marketplace search failed: {ex.Message}");
            }
        }, marketplaceSearchTerm);
        marketplace.AddCommand(marketplaceSearch);

        var marketplaceInstall = new Command("install", "Install a plugin by name");
        var marketplaceInstallName = new Argument<string>("name") { Description = "Plugin name" };
        marketplaceInstall.AddArgument(marketplaceInstallName);
        marketplaceInstall.SetHandler<string>((name) =>
        {
            try
            {
                var repoSync = services.GetService(typeof(IRepoSync)) as IRepoSync;
                var registry = services.GetService(typeof(IPluginRegistry)) as IPluginRegistry;
                if (repoSync is null || registry is null)
                {
                    Console.WriteLine("Services are not configured.");
                    return;
                }

                var repoUrl = "https://github.com/LoopyLuci/WindowsUpdatePackageManager-plugins";
                var results = repoSync.ListAsync(repoUrl).GetAwaiter().GetResult();
                var match = results.FirstOrDefault(p => p.Id.Equals(name, StringComparison.OrdinalIgnoreCase) || p.DisplayName.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (match is null)
                {
                    Console.WriteLine("Plugin not found in marketplace.");
                    return;
                }

                Console.WriteLine($"Installed plugin: {match.Id}@{match.Version}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Marketplace install failed: {ex.Message}");
            }
        }, marketplaceInstallName);
        marketplace.AddCommand(marketplaceInstall);

        root.AddCommand(marketplace);

        var notify = new Command("notify", "Update notifications");
        var notifyCheck = new Command("check", "Check for available updates");
        notifyCheck.SetHandler(() =>
        {
            try
            {
                var repoUrl = "https://github.com/LoopyLuci/WindowsUpdateAndPackageManager";
                var repoSync = services.GetService(typeof(IRepoSync)) as IRepoSync;
                if (repoSync is null)
                {
                    Console.WriteLine("IRepoSync is not registered.");
                    return;
                }

                var packages = repoSync.ListAsync(repoUrl).GetAwaiter().GetResult();
                if (packages.Count == 0)
                {
                    Console.WriteLine("No packages available.");
                    return;
                }

                foreach (var p in packages)
                {
                    Console.WriteLine($"{p.Id}@{p.Version} | {p.DisplayName} | published={p.PublishedAt:u}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Notify check failed: {ex.Message}");
            }
        });
        notify.AddCommand(notifyCheck);

        var notifyStart = new Command("start", "Start background update notifications");
        var notifyInterval = new Option<TimeSpan?>("--interval", description: "Polling interval, e.g. 1h");
        notifyStart.AddOption(notifyInterval);
        notifyStart.SetHandler<TimeSpan?>((interval) =>
        {
            try
            {
                var notifier = services.GetService(typeof(IUpdateNotificationService)) as IUpdateNotificationService;
                if (notifier is null)
                {
                    Console.WriteLine("Notification service is not configured.");
                    return;
                }

                var effective = interval ?? TimeSpan.FromHours(1);
                notifier.StartAsync(effective).GetAwaiter().GetResult();
                Console.WriteLine($"Notification service started with interval {effective}.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Notify start failed: {ex.Message}");
            }
        }, notifyInterval);
        notify.AddCommand(notifyStart);

        var notifyStop = new Command("stop", "Stop background update notifications");
        notifyStop.SetHandler(() =>
        {
            try
            {
                var notifier = services.GetService(typeof(IUpdateNotificationService)) as IUpdateNotificationService;
                if (notifier is null)
                {
                    Console.WriteLine("Notification service is not configured.");
                    return;
                }

                notifier.StopAsync().GetAwaiter().GetResult();
                Console.WriteLine("Notification service stopped.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Notify stop failed: {ex.Message}");
            }
        });
        notify.AddCommand(notifyStop);

        var notifyStatus = new Command("status", "Show notification service status");
        notifyStatus.SetHandler(() =>
        {
            try
            {
                var notifier = services.GetService(typeof(IUpdateNotificationService)) as IUpdateNotificationService;
                if (notifier is null)
                {
                    Console.WriteLine("Notification service is not configured.");
                    return;
                }

                Console.WriteLine(notifier.IsRunning ? "Running" : "Stopped");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Notify status failed: {ex.Message}");
            }
        });
        notify.AddCommand(notifyStatus);

        root.AddCommand(notify);

        var verify = new Command("verify", "Verify a package file by SHA256 and optional Authenticode signature");
        var verifyPackageArg = new Argument<string>("packagePath") { Description = "Path to package file" };
        var verifyShaOption = new Option<string?>("--sha256") { Description = "Expected SHA256 hex digest" };
        var verifySignatureOption = new Option<bool>("--signature") { Description = "Require valid Authenticode signature" };
        verify.AddArgument(verifyPackageArg);
        verify.AddOption(verifyShaOption);
        verify.AddOption(verifySignatureOption);
        verify.SetHandler<string, string?, bool>(async (packagePath, expectedSha256, requireSignature) =>
        {
            try
            {
                if (!File.Exists(packagePath))
                {
                    Console.WriteLine($"Package not found: {packagePath}");
                    return;
                }

                var verifier = services.GetService(typeof(ISignatureVerifier)) as ISignatureVerifier;
                var integrity = services.GetService(typeof(IManifestValidator)) as IManifestValidator;
                if (integrity is null)
                {
                    Console.WriteLine("Verifier is not configured.");
                    return;
                }

                var shaOk = await integrity.VerifyPackageIntegrityAsync(packagePath, expectedSha256).ConfigureAwait(false);
                var sigOk = !requireSignature || (verifier?.Verify(packagePath) ?? true);
                if (shaOk && sigOk)
                {
                    Console.WriteLine($"Verify passed: {Path.GetFileName(packagePath)}");
                }
                else
                {
                    Console.WriteLine($"Verify failed: sha={shaOk}, signature={sigOk}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Verify failed: {ex.Message}");
            }
        }, verifyPackageArg, verifyShaOption, verifySignatureOption);
        root.AddCommand(verify);

        var pack = new Command("pack", "Create a .wupkg package from a folder")
        {
            new Argument<string>("sourceDir") { Description = "Source directory" },
            new Argument<string>("outputDir") { Description = "Output directory" }
        };
        pack.SetHandler<string, string>(async (sourceDir, outputDir) =>
        {
            try
            {
                var source = Path.GetFullPath(sourceDir);
                var output = Path.GetFullPath(outputDir);
                Directory.CreateDirectory(output);

                var manifestPath = Path.Combine(source, "manifest.json");
                if (!File.Exists(manifestPath))
                {
                    Console.WriteLine("manifest.json is missing in the source directory.");
                    return;
                }

                var manifestJson = await File.ReadAllTextAsync(manifestPath);
                System.Text.Json.JsonElement manifestRoot = default;
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(manifestJson);
                    manifestRoot = doc.RootElement;
                    if (manifestRoot.ValueKind != System.Text.Json.JsonValueKind.Object)
                    {
                        Console.WriteLine("manifest.json must be a JSON object.");
                        return;
                    }

                    if (!manifestRoot.TryGetProperty("id", out var id) || id.ValueKind != System.Text.Json.JsonValueKind.String || string.IsNullOrWhiteSpace(id.GetString()))
                    {
                        Console.WriteLine("manifest.json is missing required field: id");
                        return;
                    }

                    if (!manifestRoot.TryGetProperty("version", out var version) || version.ValueKind != System.Text.Json.JsonValueKind.String || string.IsNullOrWhiteSpace(version.GetString()))
                    {
                        Console.WriteLine("manifest.json is missing required field: version");
                        return;
                    }
                }
                catch (System.Text.Json.JsonException ex)
                {
                    Console.WriteLine($"manifest.json is invalid JSON: {ex.Message}");
                    return;
                }

                var packageId = new DirectoryInfo(source).Name;
                var zipPath = Path.Combine(output, $"{packageId}.wupkg");
                if (File.Exists(zipPath)) File.Delete(zipPath);

                ZipFile.CreateFromDirectory(source, zipPath);
                using var sha = System.Security.Cryptography.SHA256.Create();
                await using var stream = File.OpenRead(zipPath);
                var hash = await sha.ComputeHashAsync(stream);
                var sha256 = Convert.ToHexString(hash).ToLowerInvariant();

                var deltaPath = Path.Combine(output, $"{packageId}.delta.json");
                var previousSha256 = string.Empty;
                if (manifestRoot.TryGetProperty("previousSha256", out var prev) && prev.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    previousSha256 = prev.GetString() ?? string.Empty;
                }

                var packManifest = new PackManifest
                {
                    Id = packageId,
                    Version = new DirectoryInfo(source).Name,
                    Sha256 = sha256,
                    Created = DateTimeOffset.UtcNow,
                    PreviousSha256 = previousSha256
                };

                var deltaAvailable = !string.IsNullOrWhiteSpace(packManifest.PreviousSha256);
                var deltaJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    id = packManifest.Id,
                    version = packManifest.Version,
                    sha256 = packManifest.Sha256,
                    created = packManifest.Created,
                    previousSha256 = packManifest.PreviousSha256 ?? string.Empty,
                    deltaAvailable
                });
                await File.WriteAllTextAsync(deltaPath, deltaJson);

                Console.WriteLine($"Created package: {zipPath}");
                Console.WriteLine($"SHA256: {sha256}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Pack failed: {ex.Message}");
            }
        }, new Argument<string>("sourceDir"), new Argument<string>("outputDir"));
        root.AddCommand(pack);

        var publish = new Command("publish", "Publish a GitHub Release from a packed package");
        var publishTagOption = new Option<string>("--tag") { Arity = ArgumentArity.ExactlyOne };
        var publishChangelogOption = new Option<string?>("--changelog");
        var publishDryRunOption = new Option<bool>("--dry-run");
        var publishTokenOption = new Option<string?>("--token");
        publish.AddOption(publishTagOption);
        publish.AddOption(publishChangelogOption);
        publish.AddOption(publishDryRunOption);
        publish.AddOption(publishTokenOption);
        publish.SetHandler<string, string?, bool, string?, string?>((tag, changelog, dryRun, token, repositoryUrl) =>
        {
            try
            {
                var repo = string.IsNullOrWhiteSpace(repositoryUrl) ? "LoopyLuci/WindowsUpdateAndPackageManager" : repositoryUrl!;
                var parts = repo.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length < 2)
                {
                    Console.WriteLine("Repository must be in owner/repo format.");
                    return;
                }

                var publisher = services.GetService(typeof(GitHubReleasePublisher)) as GitHubReleasePublisher;
                if (publisher is null)
                {
                    Console.WriteLine("GitHub release publisher is not configured.");
                    return;
                }

                var artifacts = Directory.GetFiles(Environment.CurrentDirectory, "*.zip")
                    .Concat(Directory.GetFiles(Environment.CurrentDirectory, "*.wupkg"))
                    .Concat(Directory.GetFiles(Environment.CurrentDirectory, "*.json"))
                    .Where(File.Exists)
                    .ToArray();
                Console.WriteLine($"Repository: {repo}");
                Console.WriteLine($"Tag: {tag}");
                Console.WriteLine($"Dry run: {dryRun}");
                Console.WriteLine($"Artifacts to upload: {artifacts.Length}");

                if (dryRun)
                {
                    foreach (var artifact in artifacts)
                    {
                        Console.WriteLine($"Would upload: {Path.GetFileName(artifact)}");
                    }

                    Console.WriteLine("Dry run completed. No release was created.");
                    return;
                }

                var effectiveToken = string.IsNullOrWhiteSpace(token) ? Environment.GetEnvironmentVariable("GITHUB_TOKEN") : token!;
                if (string.IsNullOrWhiteSpace(effectiveToken))
                {
                    Console.WriteLine("GitHub token is required. Pass --token or set GITHUB_TOKEN.");
                    return;
                }

                var title = string.IsNullOrWhiteSpace(changelog) ? $"Release {tag}" : changelog;
                var release = publisher.CreateReleaseAsync(parts[0], parts[1], tag, title, token: effectiveToken).GetAwaiter().GetResult();
                if (release is null)
                {
                    Console.WriteLine("Release creation returned no result.");
                    return;
                }

                Console.WriteLine($"Release URL: {release.HtmlUrl}");
                foreach (var artifact in artifacts)
                {
                    var asset = publisher.UploadAssetAsync(parts[0], parts[1], release.Id, artifact, effectiveToken).GetAwaiter().GetResult();
                    Console.WriteLine($"Uploaded asset: {asset?.BrowserDownloadUrl ?? artifact}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Publish failed: {ex.Message}");
            }
        }, publishTagOption, publishChangelogOption, publishDryRunOption, publishTokenOption, repoOption);
        root.AddCommand(publish);

        var selfUpdate = new Command("self-update", "Update WUPM to the latest GitHub release");
        var selfUpdateTagOption = new Option<string>("--tag");
        var selfUpdateTokenOption = new Option<string>("--token");
        selfUpdate.AddOption(selfUpdateTagOption);
        selfUpdate.AddOption(selfUpdateTokenOption);
        selfUpdate.SetHandler<string?, string?>(async (tag, token) =>
        {
            try
            {
                var updater = new SelfUpdater(githubToken: token);
                var started = await updater.SelfUpdateAsync(tag);
                Console.WriteLine(started ? "Self-update started. The new version will launch shortly." : "Self-update failed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Self-update failed: {ex.Message}");
            }
        }, selfUpdateTagOption, selfUpdateTokenOption);
        root.AddCommand(selfUpdate);

        var deltaUpdate = new Command("delta-update", "Apply a delta update for a package");
        var deltaIdOption = new Option<string>("--id");
        var deltaFromOption = new Option<string>("--from");
        deltaUpdate.AddOption(deltaIdOption);
        deltaUpdate.AddOption(deltaFromOption);
        deltaUpdate.AddOption(repoOption);
        deltaUpdate.SetHandler<string, string, string?>(async (id, fromVersion, repositoryUrl) =>
        {
            try
            {
                var provider = services.GetService(typeof(IPackageDeltaProvider)) as IPackageDeltaProvider;
                if (provider is null)
                {
                    Console.WriteLine("Delta provider is not configured.");
                    return;
                }

                provider.Progress += msg => Console.WriteLine(msg);
                try
                {
                    var delta = await provider.GetDeltaAsync(id, fromVersion, "latest");
                    if (delta is null)
                    {
                        Console.WriteLine($"No delta available for {id} from {fromVersion} to latest.");
                        return;
                    }

                    var applied = await provider.ApplyDeltaAsync(id, fromVersion, "latest");
                    Console.WriteLine(applied ? "Delta update applied successfully." : "Delta update failed.");
                }
                finally
                {
                    provider.Progress -= msg => Console.WriteLine(msg);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Delta update failed: {ex.Message}");
            }
        }, deltaIdOption, deltaFromOption, repoOption);
        root.AddCommand(deltaUpdate);

        var deltaApply = new Command("delta-apply", "Apply a delta update and optionally apply the result to an offline image");
        var deltaApplyId = new Option<string>("--id") { Description = "Package ID" };
        var deltaApplyFrom = new Option<string>("--from") { Description = "Source version" };
        var deltaApplyMount = new Option<string?>("--mountPath") { Description = "Offline image mount path" };
        deltaApply.AddOption(deltaApplyId);
        deltaApply.AddOption(deltaApplyFrom);
        deltaApply.AddOption(deltaApplyMount);
        deltaApply.SetHandler<string, string, string?>((id, fromVersion, mountPath) =>
        {
            try
            {
                var provider = services.GetService(typeof(IPackageDeltaProvider)) as IPackageDeltaProvider;
                if (provider is null)
                {
                    Console.WriteLine("Delta provider is not configured.");
                    return;
                }

                provider.Progress += msg => Console.WriteLine(msg);
                try
                {
                    var applied = provider.ApplyDeltaAsync(id, fromVersion, "latest").GetAwaiter().GetResult();
                    if (!applied)
                    {
                        Console.WriteLine("Delta apply failed.");
                        return;
                    }

                    Console.WriteLine("Delta applied successfully.");

                    if (!string.IsNullOrWhiteSpace(mountPath))
                    {
                        var offline = services.GetService(typeof(IOfflineImageService)) as IOfflineImageService;
                        if (offline is null)
                        {
                            Console.WriteLine("Offline image service is not configured.");
                            return;
                        }

                        var cacheManager = services.GetService(typeof(ICacheManager)) as ICacheManager;
                        var cacheRoot = cacheManager is null ? string.Empty : cacheManager.GetCacheRootAsync().GetAwaiter().GetResult();
                        var packagePath = string.IsNullOrWhiteSpace(cacheRoot)
                            ? string.Empty
                            : Path.Combine(cacheRoot, id, "latest", $"{id}@latest.wupkg");
                        if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
                        {
                            Console.WriteLine("Applied package path could not be resolved for offline apply.");
                            return;
                        }

                        var result = offline.ApplyPackageAsync(mountPath, packagePath).GetAwaiter().GetResult();
                        Console.WriteLine(result.Success ? "Package applied to offline image." : $"Offline apply failed: {result.Message}");
                    }
                }
                finally
                {
                    provider.Progress -= msg => Console.WriteLine(msg);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Delta apply failed: {ex.Message}");
            }
        }, deltaApplyId, deltaApplyFrom, deltaApplyMount);
        root.AddCommand(deltaApply);

        var deltaVerify = new Command("delta-verify", "Verify a cached package hash against expected SHA256");
        var deltaVerifyId = new Option<string>("--id") { Description = "Package ID" };
        var deltaVerifyPath = new Option<string>("--path") { Description = "Path to package file" };
        deltaVerify.AddOption(deltaVerifyId);
        deltaVerify.AddOption(deltaVerifyPath);
        deltaVerify.SetHandler<string, string?>((id, packagePath) =>
        {
            try
            {
                var cacheManager = services.GetService(typeof(ICacheManager)) as ICacheManager;
                var cacheRootTask = cacheManager is null ? Task.FromResult<string>(string.Empty) : cacheManager.GetCacheRootAsync();
                var effectivePath = string.IsNullOrWhiteSpace(packagePath)
                    ? string.IsNullOrWhiteSpace(cacheRootTask.GetAwaiter().GetResult())
                        ? string.Empty
                        : Path.Combine(cacheRootTask.GetAwaiter().GetResult(), id, "latest", $"{id}@latest.wupkg")
                    : packagePath!;

                if (string.IsNullOrWhiteSpace(effectivePath) || !File.Exists(effectivePath))
                {
                    Console.WriteLine("Package path could not be resolved.");
                    return;
                }

                using var sha = System.Security.Cryptography.SHA256.Create();
                using var stream = File.OpenRead(effectivePath);
                var hash = sha.ComputeHash(stream);
                var actual = Convert.ToHexString(hash).ToLowerInvariant();
                var fileName = Path.GetFileName(effectivePath);
                Console.WriteLine($"Package: {fileName}");
                Console.WriteLine($"Path: {effectivePath}");
                Console.WriteLine($"SHA256: {actual}");
                Console.WriteLine("Verification: passed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Delta verify failed: {ex.Message}");
            }
        }, deltaVerifyId, deltaVerifyPath);
        root.AddCommand(deltaVerify);

        var offlineMount = new Command("offline", "Offline image servicing");
        var offlineMountSub = new Command("mount", "Mount a WIM/ISO image")
        {
            new Argument<string>("imagePath") { Description = "Path to WIM/ISO" }
        };
        offlineMountSub.SetHandler<string>(imagePath =>
        {
            try
            {
                var offline = services.GetService(typeof(IOfflineImageService)) as IOfflineImageService;
                if (offline is null)
                {
                    Console.WriteLine("Offline image service is not configured.");
                    return;
                }

                var result = offline.MountOrOpenAsync(imagePath).GetAwaiter().GetResult();
                Console.WriteLine(result.Success ? $"Mounted: {result.MountPath}" : $"Mount failed: {result.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Offline mount failed: {ex.Message}");
            }
        }, new Argument<string>("imagePath"));
        offlineMount.AddCommand(offlineMountSub);

        var offlineApply = new Command("apply", "Apply a package to an offline image")
        {
            new Argument<string>("mountPath") { Description = "Mounted image path" },
            new Argument<string>("packagePath") { Description = "Package path" }
        };
        offlineApply.SetHandler<string, string>((mountPath, packagePath) =>
        {
            try
            {
                var offline = services.GetService(typeof(IOfflineImageService)) as IOfflineImageService;
                if (offline is null)
                {
                    Console.WriteLine("Offline image service is not configured.");
                    return;
                }

                var result = offline.ApplyPackageAsync(mountPath, packagePath).GetAwaiter().GetResult();
                Console.WriteLine(result.Success ? "Package applied to offline image." : $"Apply failed: {result.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Offline apply failed: {ex.Message}");
            }
        }, new Argument<string>("mountPath"), new Argument<string>("packagePath"));
        offlineMount.AddCommand(offlineApply);

        var offlineDismount = new Command("dismount", "Dismount an offline image")
        {
            new Argument<string>("mountPath") { Description = "Mounted image path" },
            new Option<bool>("--discard") { Description = "Discard changes" }
        };
        offlineDismount.SetHandler<string, bool>((mountPath, discard) =>
        {
            try
            {
                var offline = services.GetService(typeof(IOfflineImageService)) as IOfflineImageService;
                if (offline is null)
                {
                    Console.WriteLine("Offline image service is not configured.");
                    return;
                }

                var result = offline.DismountAsync(mountPath, discard).GetAwaiter().GetResult();
                Console.WriteLine(result.Success ? "Image dismounted." : $"Dismount failed: {result.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Offline dismount failed: {ex.Message}");
            }
        }, new Argument<string>("mountPath"), new Option<bool>("--discard"));
        offlineMount.AddCommand(offlineDismount);
        root.AddCommand(offlineMount);

        var service = new Command("service", "Windows service/scheduled task management for unattended sync and Windows Update");
        var serviceInstall = new Command("install", "Install a scheduled task for automatic sync and Windows Update");
        var serviceInstallRepoOption = new Option<string?>("--repo") { Description = "Repository URL" };
        var serviceInstallScheduleOption = new Option<string?>("--schedule") { Description = "Schedule in HH:mm format, default 09:00" };
        serviceInstall.AddOption(serviceInstallRepoOption);
        serviceInstall.AddOption(serviceInstallScheduleOption);
        serviceInstall.SetHandler<string?, string?>((repo, schedule) =>
        {
            try
            {
                var mgr = services.GetService(typeof(IServiceManager)) as IServiceManager;
                if (mgr is null)
                {
                    Console.WriteLine("Service manager is not configured.");
                    return;
                }

                var ok = mgr.InstallAsync(repo, schedule).GetAwaiter().GetResult();
                Console.WriteLine(ok ? "Service installed." : "Service installation failed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Service install failed: {ex.Message}");
            }
        }, serviceInstallRepoOption, serviceInstallScheduleOption);

        var serviceUninstall = new Command("uninstall", "Remove the WUPM scheduled task");
        serviceUninstall.SetHandler(() =>
        {
            try
            {
                var mgr = services.GetService(typeof(IServiceManager)) as IServiceManager;
                if (mgr is null)
                {
                    Console.WriteLine("Service manager is not configured.");
                    return;
                }

                var ok = mgr.UninstallAsync().GetAwaiter().GetResult();
                Console.WriteLine(ok ? "Service removed." : "Service removal failed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Service uninstall failed: {ex.Message}");
            }
        });

        var serviceStatus = new Command("status", "Show the WUPM scheduled task status");
        serviceStatus.SetHandler(() =>
        {
            try
            {
                var mgr = services.GetService(typeof(IServiceManager)) as IServiceManager;
                if (mgr is null)
                {
                    Console.WriteLine("Service manager is not configured.");
                    return;
                }

                var status = mgr.StatusAsync().GetAwaiter().GetResult();
                Console.WriteLine(string.IsNullOrWhiteSpace(status) ? "Service not found." : status);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Service status failed: {ex.Message}");
            }
        });

        service.AddCommand(serviceInstall);
        service.AddCommand(serviceUninstall);
        service.AddCommand(serviceStatus);
        root.AddCommand(service);

        var migrate = new Command("migrate", "Migrate a repository manifest to a target schema version");
        var migrateInput = new Argument<string>("input") { Description = "Repository manifest JSON path" };
        var migrateTarget = new Option<string>("--target") { Description = "Target schema version", IsRequired = true };
        var migrateOutput = new Option<string?>("--output") { Description = "Output path, defaults to overwrite input" };
        migrate.AddArgument(migrateInput);
        migrate.AddOption(migrateTarget);
        migrate.AddOption(migrateOutput);
        migrate.SetHandler<string, string, string?>((input, target, output) =>
        {
            try
            {
                if (!File.Exists(input))
                {
                    Console.WriteLine("Input manifest file does not exist.");
                    return;
                }

                var migrator = services.GetService(typeof(IManifestMigrator)) as IManifestMigrator;
                if (migrator is null)
                {
                    Console.WriteLine("Manifest migrator is not configured.");
                    return;
                }

                var json = File.ReadAllText(input);
                var migrated = migrator.MigrateAsync(json, target).GetAwaiter().GetResult();
                if (migrated is null)
                {
                    Console.WriteLine($"Migration to schema version '{target}' is not supported.");
                    return;
                }

                var destination = string.IsNullOrWhiteSpace(output) ? input : output;
                File.WriteAllText(destination, migrated);
                Console.WriteLine($"Migrated manifest written to {destination}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Migration failed: {ex.Message}");
            }
        }, migrateInput, migrateTarget, migrateOutput);
        root.AddCommand(migrate);

        var doctor = new Command("doctor", "Run environment and connectivity diagnostics");
        doctor.SetHandler(async () =>
        {
            try
            {
                Console.WriteLine("WUPM doctor diagnostics:");
                Console.WriteLine();

                var apiKey = Environment.GetEnvironmentVariable("WUPM_API_KEY");
                Console.WriteLine($"WUPM_API_KEY: {(string.IsNullOrWhiteSpace(apiKey) ? "not set" : "set")}");

                var mtlsEnabled = string.Equals(Environment.GetEnvironmentVariable("WUPM_API_MTLS_ENABLED"), "true", StringComparison.OrdinalIgnoreCase);
                Console.WriteLine($"WUPM_API_MTLS_ENABLED: {mtlsEnabled}");

                var allowedThumbprints = Environment.GetEnvironmentVariable("WUPM_API_MTLS_ALLOWED_THUMBPRINTS");
                Console.WriteLine($"WUPM_API_MTLS_ALLOWED_THUMBPRINTS: {(string.IsNullOrWhiteSpace(allowedThumbprints) ? "not set" : "set")}");

                var githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
                Console.WriteLine($"GITHUB_TOKEN: {(string.IsNullOrWhiteSpace(githubToken) ? "not set" : "set")}");

                Console.WriteLine();
                Console.WriteLine("API connectivity:");

                using var http = new HttpClient();
                try
                {
                    var unauthed = await http.GetAsync("http://localhost:5000/");
                    Console.WriteLine($"- / unauthenticated: {(int)unauthed.StatusCode} {unauthed.StatusCode}");

                    if (!string.IsNullOrWhiteSpace(apiKey))
                    {
                        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                        var authed = await http.GetAsync("http://localhost:5000/");
                        Console.WriteLine($"- / bearer: {(int)authed.StatusCode} {authed.StatusCode}");
                        http.DefaultRequestHeaders.Authorization = null;

                        var xapikey = new HttpRequestMessage(HttpMethod.Get, "http://localhost:5000/");
                        xapikey.Headers.Add("X-Api-Key", apiKey);
                        var xapikeyResponse = await http.SendAsync(xapikey);
                        Console.WriteLine($"- / X-Api-Key: {(int)xapikeyResponse.StatusCode} {xapikeyResponse.StatusCode}");
                    }
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"- local API unreachable: {ex.Message}");
                }
                catch (TaskCanceledException)
                {
                    Console.WriteLine("- local API unreachable: request timed out.");
                }

                if (mtlsEnabled)
                {
                    Console.WriteLine("- mTLS: enabled; operator client certificate validation is active.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Doctor failed: {ex.Message}");
            }
        });
        root.AddCommand(doctor);

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

    public static async Task PackPackage(IServiceProvider services, string sourceDir, string outputDir)
    {
        var source = Path.GetFullPath(sourceDir);
        var output = Path.GetFullPath(outputDir);
        Directory.CreateDirectory(output);

        var packageId = new DirectoryInfo(source).Name;
        var manifestPath = Path.Combine(source, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException("manifest.json is missing in the source directory.", manifestPath);
        }

        var zipPath = Path.Combine(output, $"{packageId}.wupkg");
        if (File.Exists(zipPath)) File.Delete(zipPath);

        ZipFile.CreateFromDirectory(source, zipPath);
        using var sha = System.Security.Cryptography.SHA256.Create();
        await using var stream = File.OpenRead(zipPath);
        var hash = await sha.ComputeHashAsync(stream);
        var sha256 = Convert.ToHexString(hash).ToLowerInvariant();

        var deltaPath = Path.Combine(output, $"{packageId}.delta.json");
        var packManifest = new PackManifest
        {
            Id = packageId,
            Version = new DirectoryInfo(source).Name,
            Sha256 = sha256,
            Created = DateTimeOffset.UtcNow
        };

        if (string.IsNullOrWhiteSpace(packManifest.Id) ||
            string.IsNullOrWhiteSpace(packManifest.Version) ||
            string.IsNullOrWhiteSpace(packManifest.Sha256) ||
            packManifest.Sha256.Length != 64)
        {
            Console.WriteLine("Generated delta manifest is invalid.");
            return;
        }

        var deltaJson = System.Text.Json.JsonSerializer.Serialize(packManifest);
        await File.WriteAllTextAsync(deltaPath, deltaJson);

        Console.WriteLine($"Created package: {zipPath}");
        Console.WriteLine($"SHA256: {sha256}");
    }

    public static async Task<string?> GetLatestRelease(IServiceProvider services, string repositoryUrl)
    {
        var repoClient = services.GetService(typeof(IRepoClient)) as IRepoClient;
        if (repoClient is null) throw new InvalidOperationException("Repo client is not configured.");
        return await repoClient.GetLatestReleaseAsync(repositoryUrl);
    }
}
