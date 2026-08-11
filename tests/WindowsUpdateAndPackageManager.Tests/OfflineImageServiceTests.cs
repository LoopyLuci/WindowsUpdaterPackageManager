using System.Diagnostics;
using System.Threading.Tasks;
using Moq;
using WindowsUpdateAndPackageManager.Core;
using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

public sealed class OfflineImageServiceTests
{
    [Fact]
    public async Task MountOrOpenAsync_returns_failed_when_dism_fails()
    {
        var service = new OfflineImageService();
        var result = await service.MountOrOpenAsync("C:\\nonexistent\\image.wim");
        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
    }
}
