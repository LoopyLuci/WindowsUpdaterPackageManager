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
        driverUpdate.SetHandler(() =>
        {
            try
            {
                var manager = services.GetService(typeof(IWindowsUpdateManager)) as IWindowsUpdateManager;
                if (manager is null)
                {
                    Console.WriteLine("IWindowsUpdateManager is not registered.");
                    return;
                }

                var result = manager.ScanAndInstallAsync(driversOnly: true).GetAwaiter().GetResult();
                Console.WriteLine($"Success={result.Success}; found={result.UpdatesFound}; installed={result.UpdatesInstalled}; reboot={result.RebootRequired}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Driver update failed: {ex.Message}");
            }
        });
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
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(manifestJson);
                    var root = doc.RootElement;
                    if (root.ValueKind != System.Text.Json.JsonValueKind.Object)
                    {
                        Console.WriteLine("manifest.json must be a JSON object.");
                        return;
                    }

                    if (!root.TryGetProperty("id", out var id) || id.ValueKind != System.Text.Json.JsonValueKind.String || string.IsNullOrWhiteSpace(id.GetString()))
                    {
                        Console.WriteLine("manifest.json is missing required field: id");
                        return;
                    }

                    if (!root.TryGetProperty("version", out var version) || version.ValueKind != System.Text.Json.JsonValueKind.String || string.IsNullOrWhiteSpace(version.GetString()))
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
        selfUpdate.SetHandler(() =>
        {
            try
            {
                var updater = services.GetService(typeof(ISelfUpdater)) as ISelfUpdater;
                if (updater is null)
                {
                    Console.WriteLine("Self-updater is not configured.");
                    return;
                }

                var started = updater.SelfUpdateAsync().GetAwaiter().GetResult();
                Console.WriteLine(started ? "Self-update started. The new version will launch shortly." : "Self-update failed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Self-update failed: {ex.Message}");
            }
        });
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

                var delta = await provider.GetDeltaAsync(id, fromVersion, "latest");
                if (delta is null)
                {
                    Console.WriteLine($"No delta available for {id} from {fromVersion} to latest.");
                    return;
                }

                var applied = await provider.ApplyDeltaAsync(id, fromVersion, "latest");
                Console.WriteLine(applied ? "Delta update applied successfully." : "Delta update failed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Delta update failed: {ex.Message}");
            }
        }, deltaIdOption, deltaFromOption, repoOption);
        root.AddCommand(deltaUpdate);

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
