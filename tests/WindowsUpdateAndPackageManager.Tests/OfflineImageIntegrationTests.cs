using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

public sealed class OfflineImageIntegrationTests
{
    [Fact(Skip = "Requires Windows with DISM and admin privileges")]
    public async Task MountAndDismount_real_wim_requires_admin()
    {
        var wim = @"C:\mount\test.wim";
        if (!File.Exists(wim)) return;

        var runner = new WindowsUpdateAndPackageManager.Core.DefaultDismProcessRunner();
        var service = new WindowsUpdateAndPackageManager.Core.OfflineImageService(runner);
        var mount = await service.MountOrOpenAsync(wim);
        if (!mount.Success)
        {
            Assert.True(mount.Message?.Contains("administrator") == true || mount.Message?.Contains("Access") == true);
            return;
        }

        var dismount = await service.DismountAsync(mount.MountPath!, commit: false);
        Assert.True(dismount.Success);
    }
}
