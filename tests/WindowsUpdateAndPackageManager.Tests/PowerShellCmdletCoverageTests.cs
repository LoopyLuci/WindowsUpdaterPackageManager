using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

public sealed class PowerShellCmdletCoverageTests
{
    [Fact]
    public void Psm1_exports_delta_and_offline_cmdlets()
    {
        var psm1 = @"D:\Projects\WindowsUpdatePackageManager\src\WindowsUpdateAndPackageManager.Core\PowerShell\WindowsUpdateAndPackageManager.psm1";
        var content = System.IO.File.ReadAllText(psm1);
        Assert.Contains("function Invoke-WUPMDeltaUpdate", content);
        Assert.Contains("function Mount-WUPOfflineImage", content);
        Assert.Contains("function Dismount-WUPOfflineImage", content);
        Assert.Contains("function Apply-WUPMPackageToImage", content);
        Assert.Contains("Export-ModuleMember", content);
    }
}
