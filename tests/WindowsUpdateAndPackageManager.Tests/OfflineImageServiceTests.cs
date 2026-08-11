using System.IO;
using System.Threading.Tasks;
using Moq;
using WindowsUpdateAndPackageManager.Core;
using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

public sealed class OfflineImageServiceTests
{
    [Fact]
    public async Task MountOrOpenAsync_returns_failed_when_path_missing()
    {
        var runner = new Mock<IDismProcessRunner>();
        runner.Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var service = new OfflineImageService(runner.Object);
        var result = await service.MountOrOpenAsync(@"C:\nonexistent\image.wim");
        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
    }

    [Fact]
    public async Task ApplyPackageAsync_returns_failed_when_mount_missing()
    {
        var runner = new Mock<IDismProcessRunner>();
        runner.Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var service = new OfflineImageService(runner.Object);
        var result = await service.ApplyPackageAsync(@"C:\nonexistent\mount", @"C:\nonexistent\package.wupkg");
        Assert.False(result.Success);
    }

    [Fact]
    public async Task DismountAsync_returns_failed_when_mount_missing()
    {
        var runner = new Mock<IDismProcessRunner>();
        runner.Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var service = new OfflineImageService(runner.Object);
        var result = await service.DismountAsync(@"C:\nonexistent\mount", commit: false);
        Assert.False(result.Success);
    }
}
