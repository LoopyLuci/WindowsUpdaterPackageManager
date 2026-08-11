using Microsoft.Extensions.DependencyInjection;
using Moq;
using WindowsUpdateAndPackageManager.Core;
using WindowsUpdateAndPackageManager.Data;
using WindowsUpdateAndPackageManager.Models;
using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

public sealed class RollbackCliTests
{
    [Fact]
    public async Task RollbackManager_returns_true_when_rollback_completes()
    {
        var state = new Mock<IStateDatabase>();
        state.Setup(x => x.ListInstalledAsync(default)).ReturnsAsync(new List<PackageManifest>
        {
            new PackageManifest { Id = "pkg", UninstallCommand = "cmd.exe" }
        });
        var audit = new Mock<IAuditStore>();
        var runner = new Mock<IProcessRunner>();
        runner.Setup(r => r.StartAndWaitAsync(It.IsAny<string>(), It.IsAny<string>(), default)).ReturnsAsync(0);
        var manager = new RollbackManager(state.Object, audit.Object, runner.Object);
        var ok = await manager.RollbackAsync("pkg");
        Assert.True(ok);
    }
}
