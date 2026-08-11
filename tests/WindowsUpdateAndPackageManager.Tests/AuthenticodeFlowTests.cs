using System;
using System.Threading.Tasks;
using Moq;
using WindowsUpdateAndPackageManager.Core;
using WindowsUpdateAndPackageManager.Data;
using WindowsUpdateAndPackageManager.Infrastructure;
using WindowsUpdateAndPackageManager.Models;
using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

public sealed class AuthenticodeFlowTests
{
    [Fact]
    public async Task InstallAsync_blocks_when_signature_verification_fails()
    {
        var state = new Mock<IStateDatabase>();
        var audit = new Mock<IAuditStore>();
        var cache = new Mock<ICacheManager>();
        var policy = new Mock<IPolicyEngine>();
        var verifier = new Mock<ISignatureVerifier>();

        state.Setup(x => x.IsInstalledAsync(It.IsAny<string>(), It.IsAny<string?>(), default)).ReturnsAsync(false);
        cache.Setup(x => x.IsCachedAsync(It.IsAny<string>(), It.IsAny<string?>(), default)).ReturnsAsync(true);
        cache.Setup(x => x.EnsurePackageCacheAsync(It.IsAny<string>(), It.IsAny<string?>(), default)).ReturnsAsync(@"C:\temp\wupm-cache");
        policy.Setup(x => x.IsAllowedAsync(It.IsAny<string>(), default)).ReturnsAsync(true);
        verifier.Setup(x => x.Verify(It.IsAny<string>())).Returns(false);

        var pm = new PackageManager(state.Object, audit.Object, cache.Object, policy.Object, verifier.Object);
        var result = await pm.InstallAsync(new PackageManifest { Id = "bad", Version = "1.0", InstallCommand = "notepad.exe" });

        Assert.False(result.Success);
        Assert.Contains("signature", result.Message ?? "", StringComparison.OrdinalIgnoreCase);
    }
}
