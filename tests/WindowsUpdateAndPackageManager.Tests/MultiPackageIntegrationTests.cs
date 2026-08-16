using System;
using System.IO;
using System.Linq;
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
}
