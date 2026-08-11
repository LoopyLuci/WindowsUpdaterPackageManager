using System.Collections.Generic;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using WindowsUpdateAndPackageManager.Core;
using WindowsUpdateAndPackageManager.Data;
using WindowsUpdateAndPackageManager.Infrastructure;
using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

public sealed class PackageDeltaProviderTests
{
    [Fact]
    public async Task ApplyDeltaAsync_returns_false_when_delta_missing()
    {
        var deltaStore = new Mock<IDeltaStore>();
        deltaStore.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((DeltaManifest?)null);

        var repoClient = new Mock<IRepoClient>();
        var cache = new Mock<ICacheManager>();
        var provider = new PackageDeltaProvider(deltaStore.Object, repoClient.Object, cache.Object);

        var result = await provider.ApplyDeltaAsync("pkg", "1.0", "2.0");
        Assert.False(result);
    }

    [Fact]
    public async Task ApplyDeltaAsync_returns_true_when_delta_applied()
    {
        var payload = Encoding.UTF8.GetBytes("hello");
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(payload);
        var expectedHash = Convert.ToHexString(hash).ToLowerInvariant();

        var deltaStore = new Mock<IDeltaStore>();
        deltaStore.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new DeltaManifest { PackageId = "pkg", FromVersion = "1.0", ToVersion = "2.0", DeltaUrl = "https://example.invalid/delta", DeltaSize = payload.Length, DeltaHash = expectedHash });

        var repoClient = new Mock<IRepoClient>();
        repoClient.Setup(x => x.DownloadPackageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new MemoryStream(payload));
        var cache = new Mock<ICacheManager>();
        cache.Setup(x => x.EnsurePackageCacheAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("C:\\temp\\pkg");
        var provider = new PackageDeltaProvider(deltaStore.Object, repoClient.Object, cache.Object);

        var result = await provider.ApplyDeltaAsync("pkg", "1.0", "2.0");
        Assert.True(result);
    }
}
