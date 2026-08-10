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
        var dataRoot = Path.Combine(rootPath, ".wupm");
        Directory.CreateDirectory(dataRoot);
        var cacheRoot = Path.Combine(dataRoot, "cache");

        services.AddSingleton<IStateDatabase>(new SqliteStateDatabase(dataRoot));
        services.AddSingleton<IAuditStore>(new SqliteAuditStore(dataRoot));
        services.AddSingleton<IWindowsUpdateManager>(sp => new WindowsUpdateManager(sp.GetRequiredService<IAuditStore>()));
        services.AddSingleton<IPackageManager>(sp => new PackageManager(sp.GetRequiredService<IStateDatabase>(), sp.GetRequiredService<IAuditStore>()));
        services.AddSingleton<IRepoSync>(sp => new RepoSync(sp.GetRequiredService<IRepoClient>(), sp.GetRequiredService<IManifestValidator>(), sp.GetRequiredService<IStateDatabase>(), sp.GetRequiredService<IAuditStore>()));
        services.AddSingleton<RollbackManager>();
        services.AddSingleton<IAuditor>(sp => new Auditor(sp.GetRequiredService<IAuditStore>()));
        services.AddSingleton<IRepoClient>(sp => new GitHubRepoClient());
        services.AddSingleton<IManifestValidator>(sp => new DefaultManifestValidator());
        services.AddSingleton<ICacheManager>(sp => new DefaultCacheManager(cacheRoot));
        services.AddSingleton<IPolicyEngine>(sp => new AllowlistPolicyEngine());

        if (!string.IsNullOrWhiteSpace(repositoryUrl))
        {
            services.AddSingleton(sp => new RepoSyncOptions { RepositoryUrl = repositoryUrl });
        }

        return services.BuildServiceProvider();
    }
}

public sealed class RepoSyncOptions
{
    public string RepositoryUrl { get; init; } = string.Empty;
}
