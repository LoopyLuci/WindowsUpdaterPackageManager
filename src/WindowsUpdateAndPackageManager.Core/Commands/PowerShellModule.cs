using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using WindowsUpdateAndPackageManager.Core;
using WindowsUpdateAndPackageManager.Data;
using WindowsUpdateAndPackageManager.Infrastructure;
using WindowsUpdateAndPackageManager.Models;

namespace WindowsUpdateAndPackageManager.Commands;

public static class PowerShellModule
{
    public const string ModuleName = "WindowsUpdateAndPackageManager";

    public static IServiceProvider Build(string rootPath)
    {
        var dataRoot = Path.Combine(rootPath, ".wupm");
        Directory.CreateDirectory(dataRoot);
        var cacheRoot = Path.Combine(dataRoot, "cache");
        var services = new ServiceCollection();
        services.AddSingleton<IStateDatabase>(new SqliteStateDatabase(dataRoot));
        services.AddSingleton<IAuditStore>(new SqliteAuditStore(dataRoot));
        services.AddSingleton<IWindowsUpdateApi, WindowsUpdateApi>();
        services.AddSingleton<IWindowsUpdateManager>(sp => new WindowsUpdateManager(sp.GetRequiredService<IAuditStore>(), sp.GetRequiredService<IWindowsUpdateApi>()));
        services.AddSingleton<IPackageManager>(sp => new PackageManager(sp.GetRequiredService<IStateDatabase>(), sp.GetRequiredService<IAuditStore>(), sp.GetRequiredService<ICacheManager>(), sp.GetRequiredService<IPolicyEngine>()));
        services.AddSingleton<IRepoSync>(sp => new RepoSync(sp.GetRequiredService<IRepoClient>(), sp.GetRequiredService<IManifestValidator>(), sp.GetRequiredService<IStateDatabase>(), sp.GetRequiredService<IAuditStore>(), sp.GetRequiredService<ICacheManager>()));
        services.AddSingleton<RollbackManager>();
        services.AddSingleton<IAuditor>(sp => new Auditor(sp.GetRequiredService<IAuditStore>()));
        services.AddSingleton<IRepoClient>(sp =>
        {
            var http = new System.Net.Http.HttpClient();
            var repo = "https://github.com/LoopyLuci/WindowsUpdateAndPackageManager";
            return new GitHubRepoClient(http, repo);
        });
        services.AddSingleton<IManifestValidator>(sp => new DefaultManifestValidator());
        services.AddSingleton<ICacheManager>(sp => new DefaultCacheManager(cacheRoot));
        services.AddSingleton<IPolicyEngine>(sp => new AllowlistPolicyEngine());
        services.AddSingleton<ISignatureVerifier, AuthenticodeVerifier>();
        services.AddSingleton<IOfflineImageService, OfflineImageService>();
        services.AddSingleton<IDeltaStore>(sp => new SqliteDeltaStore(cacheRoot));
        services.AddSingleton<IPackageDeltaProvider, PackageDeltaProvider>();
        return services.BuildServiceProvider();
    }

    public static async Task<SyncResult> SyncRepositoryAsync(IServiceProvider services, string repositoryUrl)
    {
        var repoSync = services.GetService(typeof(IRepoSync)) as IRepoSync;
        if (repoSync is null) throw new InvalidOperationException("IRepoSync is not registered.");
        return await repoSync.SyncAsync(repositoryUrl);
    }

    public static async Task<InstallResult> InstallPackageAsync(IServiceProvider services, PackageManifest package)
    {
        var pm = services.GetService(typeof(IPackageManager)) as IPackageManager;
        if (pm is null) throw new InvalidOperationException("IPackageManager is not registered.");
        return await pm.InstallAsync(package);
    }

    public static async Task<UninstallResult> UninstallPackageAsync(IServiceProvider services, string packageId)
    {
        var pm = services.GetService(typeof(IPackageManager)) as IPackageManager;
        if (pm is null) throw new InvalidOperationException("IPackageManager is not registered.");
        return await pm.UninstallAsync(packageId);
    }

    public static async Task<IReadOnlyList<PackageManifest>> ListInstalledAsync(IServiceProvider services)
    {
        var pm = services.GetService(typeof(IPackageManager)) as IPackageManager;
        if (pm is null) throw new InvalidOperationException("IPackageManager is not registered.");
        return await pm.ListInstalledAsync();
    }

    public static async Task<IReadOnlyList<PackageManifest>> ListAvailableAsync(IServiceProvider services, string repositoryUrl)
    {
        var repoSync = services.GetService(typeof(IRepoSync)) as IRepoSync;
        if (repoSync is null) throw new InvalidOperationException("IRepoSync is not registered.");
        return await repoSync.ListAsync(repositoryUrl);
    }

    public static async Task<WindowsUpdateResult> InvokeWindowsUpdateAsync(IServiceProvider services, bool driversOnly = false)
    {
        var manager = services.GetService(typeof(IWindowsUpdateManager)) as IWindowsUpdateManager;
        if (manager is null) throw new InvalidOperationException("IWindowsUpdateManager is not registered.");
        return await manager.ScanAndInstallAsync(driversOnly);
    }

    public static async Task RollbackAsync(IServiceProvider services)
    {
        var rollbackManager = services.GetService(typeof(RollbackManager)) as RollbackManager;
        if (rollbackManager is null) throw new InvalidOperationException("RollbackManager is not registered.");
        await rollbackManager.RollbackAsync();
    }

    public static async Task<IReadOnlyList<AuditEntry>> QueryAuditAsync(IServiceProvider services, DateTimeOffset? from = null, DateTimeOffset? to = null, string? action = null)
    {
        var auditor = services.GetService(typeof(IAuditor)) as IAuditor;
        if (auditor is null) throw new InvalidOperationException("IAuditor is not registered.");
        return await auditor.QueryAsync(from, to, action);
    }

    public static void SetPolicyAllow(IServiceProvider services, string packageId)
    {
        var policy = services.GetService(typeof(IPolicyEngine)) as AllowlistPolicyEngine;
        if (policy is null) throw new InvalidOperationException("AllowlistPolicyEngine is not registered.");
        policy.Allow(packageId);
    }

    public static void SetPolicyDeny(IServiceProvider services, string packageId)
    {
        var policy = services.GetService(typeof(IPolicyEngine)) as AllowlistPolicyEngine;
        if (policy is null) throw new InvalidOperationException("AllowlistPolicyEngine is not registered.");
        policy.Deny(packageId);
    }

    public static async Task<string?> GetLatestReleaseAsync(IServiceProvider services, string repositoryUrl)
    {
        var client = services.GetService(typeof(IRepoClient)) as IRepoClient;
        if (client is null) throw new InvalidOperationException("IRepoClient is not registered.");
        return await client.GetLatestReleaseAsync(repositoryUrl);
    }

    public static async Task InvokeDeltaUpdateAsync(IServiceProvider services, string id, string fromVersion, string repositoryUrl)
    {
        var provider = services.GetService(typeof(IPackageDeltaProvider)) as IPackageDeltaProvider;
        if (provider is null) throw new InvalidOperationException("IPackageDeltaProvider is not registered.");

        var delta = await provider.GetDeltaAsync(id, fromVersion, "latest");
        if (delta is null) throw new InvalidOperationException($"No delta available for {id} from {fromVersion} to latest.");

        var applied = await provider.ApplyDeltaAsync(id, fromVersion, "latest");
        if (!applied) throw new InvalidOperationException("Delta update failed.");
    }

    public static async Task<OfflineImageResult> MountOfflineImageAsync(IServiceProvider services, string imagePath)
    {
        var offline = services.GetService(typeof(IOfflineImageService)) as IOfflineImageService;
        if (offline is null) throw new InvalidOperationException("IOfflineImageService is not registered.");
        return await offline.MountOrOpenAsync(imagePath);
    }

    public static async Task<OfflineImageResult> DismountOfflineImageAsync(IServiceProvider services, string mountPath, bool discard)
    {
        var offline = services.GetService(typeof(IOfflineImageService)) as IOfflineImageService;
        if (offline is null) throw new InvalidOperationException("IOfflineImageService is not registered.");
        return await offline.DismountAsync(mountPath, discard);
    }

    public static async Task<OfflineImageResult> ApplyPackageToImageAsync(IServiceProvider services, string mountPath, string packagePath)
    {
        var offline = services.GetService(typeof(IOfflineImageService)) as IOfflineImageService;
        if (offline is null) throw new InvalidOperationException("IOfflineImageService is not registered.");
        return await offline.ApplyPackageAsync(mountPath, packagePath);
    }
}
