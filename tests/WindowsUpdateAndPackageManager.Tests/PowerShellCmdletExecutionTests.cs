using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using WindowsUpdateAndPackageManager.Commands;
using WindowsUpdateAndPackageManager.Core;
using WindowsUpdateAndPackageManager.Data;
using WindowsUpdateAndPackageManager.Infrastructure;
using WindowsUpdateAndPackageManager.Models;
using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

public sealed class PowerShellCmdletExecutionTests
{
    private static IServiceProvider BuildServices(params (Type type, object instance)[] services)
    {
        var collection = new ServiceCollection();
        foreach (var (type, instance) in services)
        {
            collection.AddSingleton(type, instance);
        }
        return collection.BuildServiceProvider();
    }

    [Fact]
    public async Task SyncRepositoryAsync_returns_sync_result()
    {
        var repoSync = new Mock<IRepoSync>();
        repoSync.Setup(x => x.SyncAsync(It.IsAny<string>(), default)).ReturnsAsync(new SyncResult { Success = true, PackagesUpdated = 1 });
        var services = BuildServices((typeof(IRepoSync), repoSync.Object), (typeof(IRepoClient), Mock.Of<IRepoClient>()), (typeof(IManifestValidator), Mock.Of<IManifestValidator>()), (typeof(IStateDatabase), Mock.Of<IStateDatabase>()), (typeof(IAuditStore), Mock.Of<IAuditStore>()), (typeof(ICacheManager), Mock.Of<ICacheManager>()));

        var result = await PowerShellModule.SyncRepositoryAsync(services, "https://example.invalid/repo");

        Assert.True(result.Success);
    }

    [Fact]
    public async Task InstallPackageAsync_returns_install_result()
    {
        var pm = new Mock<IPackageManager>();
        pm.Setup(x => x.InstallAsync(It.IsAny<PackageManifest>(), default)).ReturnsAsync(new InstallResult { Success = true });
        var services = BuildServices((typeof(IPackageManager), pm.Object), (typeof(IStateDatabase), Mock.Of<IStateDatabase>()), (typeof(IAuditStore), Mock.Of<IAuditStore>()), (typeof(ICacheManager), Mock.Of<ICacheManager>()), (typeof(IPolicyEngine), Mock.Of<IPolicyEngine>()));

        var result = await PowerShellModule.InstallPackageAsync(services, new PackageManifest { Id = "pkg", Version = "1.0" });

        Assert.True(result.Success);
    }

    [Fact]
    public async Task InvokeDeltaUpdateAsync_throws_when_no_delta()
    {
        var provider = new Mock<IPackageDeltaProvider>();
        provider.Setup(x => x.GetDeltaAsync("pkg", "1.0", "latest", default)).ReturnsAsync((DeltaManifest?)null);
        var services = BuildServices((typeof(IPackageDeltaProvider), provider.Object));

        await Assert.ThrowsAsync<InvalidOperationException>(() => PowerShellModule.InvokeDeltaUpdateAsync(services, "pkg", "1.0", "latest"));
    }

    [Fact]
    public async Task MountOfflineImageAsync_returns_result()
    {
        var offline = new Mock<IOfflineImageService>();
        offline.Setup(x => x.MountOrOpenAsync("C:\\image.wim", default)).ReturnsAsync(new OfflineImageResult { Success = true, MountPath = "C:\\mount" });
        var services = BuildServices((typeof(IOfflineImageService), offline.Object));

        var result = await PowerShellModule.MountOfflineImageAsync(services, "C:\\image.wim");

        Assert.True(result.Success);
        Assert.Equal("C:\\mount", result.MountPath);
    }

    [Fact]
    public async Task ApplyPackageToImageAsync_returns_result()
    {
        var offline = new Mock<IOfflineImageService>();
        offline.Setup(x => x.ApplyPackageAsync("C:\\mount", "C:\\pkg.wupkg", default)).ReturnsAsync(new OfflineImageResult { Success = true });
        var services = BuildServices((typeof(IOfflineImageService), offline.Object));

        var result = await PowerShellModule.ApplyPackageToImageAsync(services, "C:\\mount", "C:\\pkg.wupkg");

        Assert.True(result.Success);
    }

    [Fact]
    public async Task DismountOfflineImageAsync_returns_result()
    {
        var offline = new Mock<IOfflineImageService>();
        offline.Setup(x => x.DismountAsync("C:\\mount", true, default)).ReturnsAsync(new OfflineImageResult { Success = true });
        var services = BuildServices((typeof(IOfflineImageService), offline.Object));

        var result = await PowerShellModule.DismountOfflineImageAsync(services, "C:\\mount", true);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task QueryAuditAsync_returns_entries()
    {
        var auditor = new Mock<IAuditor>();
        auditor.Setup(x => x.QueryAsync(It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(), It.IsAny<string?>(), default)).ReturnsAsync(new List<AuditEntry> { new AuditEntry { Action = "test" } });
        var services = BuildServices((typeof(IAuditor), auditor.Object), (typeof(IAuditStore), Mock.Of<IAuditStore>()));

        var entries = await PowerShellModule.QueryAuditAsync(services);

        Assert.Single(entries);
    }

    [Fact]
    public void SetPolicyAllow_adds_to_allowlist()
    {
        var policy = new AllowlistPolicyEngine();
        var services = BuildServices((typeof(IPolicyEngine), policy));

        PowerShellModule.SetPolicyAllow(services, "pkg");

        Assert.True(policy.IsAllowedAsync("pkg").GetAwaiter().GetResult());
    }

    [Fact]
    public void SetPolicyDeny_adds_to_denylist()
    {
        var policy = new AllowlistPolicyEngine();
        var services = BuildServices((typeof(IPolicyEngine), policy));

        PowerShellModule.SetPolicyDeny(services, "badpkg");

        Assert.False(policy.IsAllowedAsync("badpkg").GetAwaiter().GetResult());
    }

    [Fact]
    public async Task GetLatestReleaseAsync_returns_release_text()
    {
        var client = new Mock<IRepoClient>();
        client.Setup(x => x.GetLatestReleaseAsync(It.IsAny<string>(), default)).ReturnsAsync("{\"tag_name\":\"v1.0\",\"html_url\":\"https://example.invalid\"}");
        var services = BuildServices((typeof(IRepoClient), client.Object));

        var text = await PowerShellModule.GetLatestReleaseAsync(services, "https://example.invalid/repo");

        Assert.Contains("v1.0", text);
    }
}
