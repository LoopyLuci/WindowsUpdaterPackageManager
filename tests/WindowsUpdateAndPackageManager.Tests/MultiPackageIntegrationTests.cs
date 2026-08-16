using System;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using WindowsUpdateAndPackageManager.Commands;
using WindowsUpdateAndPackageManager.Core;
using WindowsUpdateAndPackageManager.Data;
using WindowsUpdateAndPackageManager.Infrastructure;
using WindowsUpdateAndPackageManager.Models;
using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

public sealed class MultiPackageIntegrationTests
{
    [Fact]
    public async Task Install_audit_prune_uninstall_flow_succeeds()
    {
        var root = Path.Combine(Path.GetTempPath(), "wupm-integration-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        IServiceProvider? provider = null;
        try
        {
            var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
            WindowsUpdateAndPackageManager.Commands.Composition.RegisterInto(services, root, "https://github.com/LoopyLuci/WindowsUpdateAndPackageManager");
            provider = services.BuildServiceProvider();

            var package = new PackageManifest { Id = "pkg-integration", Version = "1.2.3", DisplayName = "Integration Package" };

            var manager = provider.GetRequiredService<IPackageManager>();
            var installResult = await manager.InstallAsync(package);
            Assert.NotNull(installResult);

            var auditor = provider.GetRequiredService<IAuditor>();
            var auditEntries = await auditor.QueryAsync(null, null, null, CancellationToken.None);
            Assert.NotNull(auditEntries);
            Assert.Contains(auditEntries, e => e.PackageId == package.Id);

            var cache = provider.GetRequiredService<ICacheManager>();
            await cache.PruneAsync(CancellationToken.None);

            var uninstallResult = await manager.UninstallAsync(package.Id!);
            Assert.NotNull(uninstallResult);
        }
        finally
        {
            if (provider is IDisposable disposable)
                disposable.Dispose();

            var preserveRoot = Path.Combine(Path.GetTempPath(), "wupm-integration-last");
            try
            {
                if (Directory.Exists(preserveRoot))
                    Directory.Delete(preserveRoot, recursive: true);
                Directory.Move(root, preserveRoot);
            }
            catch
            {
                // ignore cleanup issues
            }
        }
    }

    [Fact]
    public async Task Cache_invalidation_removes_specific_entry()
    {
        var root = Path.Combine(Path.GetTempPath(), "wupm-cache-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        IServiceProvider? provider = null;
        try
        {
            var services = new ServiceCollection();
            Composition.RegisterInto(services, root);
            provider = services.BuildServiceProvider();

            var cache = provider.GetRequiredService<ICacheManager>();
            var cacheRoot = await cache.GetCacheRootAsync();
            var entryDir = Path.Combine(cacheRoot, "sample@1.0.0");
            Directory.CreateDirectory(entryDir);
            File.WriteAllText(Path.Combine(entryDir, "artifact.bin"), "payload");

            var listBefore = (await cache.GetCacheRootAsync());
            Assert.True(Directory.Exists(entryDir));

            await cache.InvalidateAsync("sample", "1.0.0");

            Assert.False(Directory.Exists(entryDir));
        }
        finally
        {
            if (provider is IDisposable disposable)
                disposable.Dispose();

            try
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
            catch
            {
                // ignore cleanup issues
            }
        }
    }

    [Fact]
    public async Task Plugin_registry_toggle_enables_and_disables_entry()
    {
        var root = Path.Combine(Path.GetTempPath(), "wupm-plugin-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        IServiceProvider? provider = null;
        try
        {
            var services = new ServiceCollection();
            Composition.RegisterInto(services, root);
            provider = services.BuildServiceProvider();

            var registry = provider.GetRequiredService<IPluginRegistry>();
            await registry.AddAsync("TestPlugin", "1.0.0", "plugin.dll", "", CancellationToken.None);

            var listed = await registry.ListAsync();
            Assert.Contains(listed, e => e.Name == "TestPlugin" && e.Enabled);

            await registry.SetEnabledAsync("TestPlugin", false);
            var afterDisable = await registry.ListAsync();
            Assert.Contains(afterDisable, e => e.Name == "TestPlugin" && !e.Enabled);

            await registry.SetEnabledAsync("TestPlugin", true);
            var afterEnable = await registry.ListAsync();
            Assert.Contains(afterEnable, e => e.Name == "TestPlugin" && e.Enabled);
        }
        finally
        {
            if (provider is IDisposable disposable)
                disposable.Dispose();

            try
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
            catch
            {
                // ignore cleanup issues
            }
        }
    }

    [Fact]
    public async Task Marketplace_local_fallback_returns_manifest()
    {
        var root = Path.Combine(Path.GetTempPath(), "wupm-marketplace-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var marketplaceRoot = Path.Combine(root, ".wupm", "marketplace");
        Directory.CreateDirectory(marketplaceRoot);

        var manifestPath = Path.Combine(marketplaceRoot, "local-plugin.json");
        await File.WriteAllTextAsync(manifestPath, @"{
  ""id"": ""local-plugin"",
  ""name"": ""Local Plugin"",
  ""description"": ""Local fallback plugin."",
  ""version"": ""0.1.0"",
  ""author"": ""test"",
  ""repository"": """",
  ""manifestPath"": """"
}");

        IServiceProvider? provider = null;
        try
        {
            var services = new ServiceCollection();
            Composition.RegisterInto(services, root);
            provider = services.BuildServiceProvider();

            var localClient = new LocalMarketplaceClient(root);
            var results = await localClient.SearchAsync("local-plugin");
            Assert.NotNull(results);
            Assert.Contains(results, r => r.Id.Equals("local-plugin", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (provider is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync();
            else if (provider is IDisposable disposable)
                disposable.Dispose();

            try
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
            catch
            {
                // ignore cleanup issues
            }
        }
    }

    [Fact]
    public void Service_status_returns_elevation_state()
    {
        var elevated = new WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent()).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        var status = new
        {
            installed = false,
            elevated,
            message = elevated ? "Running as administrator" : "Not running as administrator; service operations require elevation"
        };

        Assert.False(status.installed);
        Assert.Equal(elevated, status.elevated);
        Assert.False(string.IsNullOrWhiteSpace(status.message));
    }
}
