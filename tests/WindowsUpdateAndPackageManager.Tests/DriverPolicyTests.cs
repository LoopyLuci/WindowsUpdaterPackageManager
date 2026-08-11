using WindowsUpdateAndPackageManager.Core;
using WindowsUpdateAndPackageManager.Infrastructure;
using WindowsUpdateAndPackageManager.Models;
using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

public class DriverPolicyTests
{
    [Fact]
    public async Task DefaultDriverVerifier_returns_true_when_required_fields_present()
    {
        var verifier = new DefaultDriverVerifier();
        var manifest = new DriverPackageManifest
        {
            Id = "driver1",
            InfPath = "driver.inf",
            ClassGuid = "abc123",
            Architecture = "x64",
            Manufacturer = "Acme"
        };

        var ok = await verifier.VerifyAsync(manifest);
        Assert.True(ok);
    }

    [Fact]
    public async Task DefaultDriverVerifier_returns_false_when_inf_path_missing()
    {
        var verifier = new DefaultDriverVerifier();
        var ok = await verifier.VerifyAsync(new DriverPackageManifest());
        Assert.False(ok);
    }
}
