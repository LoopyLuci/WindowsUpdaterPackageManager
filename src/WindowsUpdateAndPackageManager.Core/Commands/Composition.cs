using Microsoft.Extensions.DependencyInjection;
using WindowsUpdateAndPackageManager.Core;
using WindowsUpdateAndPackageManager.Data;
using WindowsUpdateAndPackageManager.Infrastructure;
using WindowsUpdateAndPackageManager.Models;

namespace WindowsUpdateAndPackageManager.Commands;

public static class Composition
{
    public static IServiceProvider Build(string rootPath, string? repositoryUrl = null)
    {
        var services = new ServiceCollection();
        RegisterInto(services, rootPath, repositoryUrl);
        return services.BuildServiceProvider();
    }

    public static void RegisterInto(IServiceCollection services, string rootPath, string? repositoryUrl = null)
    {
        var dataRoot = Path.Combine(rootPath, ".wupm");
        Directory.CreateDirectory(dataRoot);
        var cacheRoot = Path.Combine(dataRoot, "cache");

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
            var http = new HttpClient();
            var repo = string.IsNullOrWhiteSpace(repositoryUrl) ? "https://github.com/LoopyLuci/WindowsUpdateAndPackageManager" : repositoryUrl!;
            return new GitHubRepoClient(http, repo);
        });
        services.AddSingleton<IManifestValidator>(sp => new DefaultManifestValidator());
        services.AddSingleton<ICacheManager>(sp => new DefaultCacheManager(cacheRoot));
        services.AddSingleton<IPolicyEngine>(sp => new AllowlistPolicyEngine());
        services.AddSingleton<ISignatureVerifier>(_ => new AuthenticodeVerifier(new SignaturePolicyOptions()));
        services.AddSingleton<IDismProcessRunner, DefaultDismProcessRunner>();
        services.AddSingleton<IProcessRunner, DefaultProcessRunner>();
        services.AddSingleton<ISelfUpdater, SelfUpdater>();
        services.AddSingleton<IServiceManager, ServiceManager>();
        services.AddSingleton<GitHubReleasePublisher>();
        services.AddSingleton<IOfflineImageService, OfflineImageService>();
        services.AddSingleton<IDeltaStore>(sp => new SqliteDeltaStore(cacheRoot));
        services.AddSingleton<IPackageDeltaProvider, PackageDeltaProvider>();

        if (!string.IsNullOrWhiteSpace(repositoryUrl))
        {
            services.AddSingleton(sp => new RepoSyncOptions { RepositoryUrl = repositoryUrl });
        }
    }
}

public sealed class RepoSyncOptions
{
    public string RepositoryUrl { get; init; } = string.Empty;
}
