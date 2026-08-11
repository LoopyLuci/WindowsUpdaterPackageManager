using System;
using System.IO;
using Xunit;
using WindowsUpdateAndPackageManager.Commands;
using WindowsUpdateAndPackageManager.Core;
using WindowsUpdateAndPackageManager.Data;
using WindowsUpdateAndPackageManager.Infrastructure;

namespace WindowsUpdateAndPackageManager.Tests;

public sealed class PowerShellModuleTests
{
    [Fact]
    public void Build_returns_service_provider_with_required_services()
    {
        var root = Path.Combine(Path.GetTempPath(), "wupm-ps-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var provider = PowerShellModule.Build(root);
            Assert.NotNull(provider);

            Assert.NotNull(provider.GetService(typeof(IStateDatabase)));
            Assert.NotNull(provider.GetService(typeof(IAuditStore)));
            Assert.NotNull(provider.GetService(typeof(IWindowsUpdateManager)));
            Assert.NotNull(provider.GetService(typeof(IPackageManager)));
            Assert.NotNull(provider.GetService(typeof(IRepoSync)));
            Assert.NotNull(provider.GetService(typeof(RollbackManager)));
            Assert.NotNull(provider.GetService(typeof(IAuditor)));
            Assert.NotNull(provider.GetService(typeof(IRepoClient)));
            Assert.NotNull(provider.GetService(typeof(IManifestValidator)));
            Assert.NotNull(provider.GetService(typeof(ICacheManager)));
            Assert.NotNull(provider.GetService(typeof(IPolicyEngine)));
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }
}
