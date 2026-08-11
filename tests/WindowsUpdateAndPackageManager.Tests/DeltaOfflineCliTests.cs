using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using WindowsUpdateAndPackageManager.Commands;
using WindowsUpdateAndPackageManager.Core;
using WindowsUpdateAndPackageManager.Infrastructure;
using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

public sealed class DeltaOfflineCliTests
{
    private static IServiceProvider BuildServices(params (Type type, object instance)[] services)
    {
        var collection = new ServiceCollection();
        foreach (var (type, instance) in services)
        {
            collection.AddSingleton(type, instance);
        }
        return collection.BuildServiceProvider();
    }

    [Fact]
    public void DeltaUpdate_prints_applied_when_delta_succeeds()
    {
        var provider = new Mock<IPackageDeltaProvider>();
        provider.Setup(x => x.GetDeltaAsync(It.IsAny<string>(), It.IsAny<string>(), "latest", default)).ReturnsAsync(new WindowsUpdateAndPackageManager.Core.DeltaManifest { PackageId = "pkg", FromVersion = "1.0", ToVersion = "2.0", DeltaUrl = "https://example.invalid/delta", DeltaSize = 1, DeltaHash = "abc" });
        provider.Setup(x => x.ApplyDeltaAsync(It.IsAny<string>(), It.IsAny<string>(), "latest", default)).ReturnsAsync(true);
        var services = BuildServices((typeof(IPackageDeltaProvider), provider.Object));

        var output = new StringBuilder();
        using var writer = new StringWriter(output);
        Console.SetOut(writer);

        Cli.Run(new[] { "delta-update", "pkg", "1.0" }, services).GetAwaiter().GetResult();

        Assert.Contains("Delta update applied successfully.", output.ToString());
    }

    [Fact]
    public void DeltaUpdate_prints_no_delta_when_unavailable()
    {
        var provider = new Mock<IPackageDeltaProvider>();
        provider.Setup(x => x.GetDeltaAsync(It.IsAny<string>(), It.IsAny<string>(), "latest", default)).ReturnsAsync((WindowsUpdateAndPackageManager.Core.DeltaManifest?)null);
        var services = BuildServices((typeof(IPackageDeltaProvider), provider.Object));

        var output = new StringBuilder();
        using var writer = new StringWriter(output);
        Console.SetOut(writer);

        Cli.Run(new[] { "delta-update", "pkg", "1.0" }, services).GetAwaiter().GetResult();

        Assert.Contains("No delta available for pkg from 1.0 to latest.", output.ToString());
    }

    [Fact]
    public void OfflineMount_prints_mount_failed_when_path_missing()
    {
        var offline = new Mock<IOfflineImageService>();
        offline.Setup(x => x.MountOrOpenAsync(It.IsAny<string>(), default)).ReturnsAsync(new OfflineImageResult { Success = false, Message = "not found" });
        var services = BuildServices((typeof(IOfflineImageService), offline.Object));

        var output = new StringBuilder();
        using var writer = new StringWriter(output);
        Console.SetOut(writer);

        Cli.Run(new[] { "offline", "mount", "C:\\missing\\image.wim" }, services).GetAwaiter().GetResult();

        Assert.Contains("Mount failed: not found", output.ToString());
    }

    [Fact]
    public void OfflineApply_prints_apply_failed_when_not_mounted()
    {
        var offline = new Mock<IOfflineImageService>();
        offline.Setup(x => x.ApplyPackageAsync(It.IsAny<string>(), It.IsAny<string>(), default)).ReturnsAsync(new OfflineImageResult { Success = false, Message = "not mounted" });
        var services = BuildServices((typeof(IOfflineImageService), offline.Object));

        var output = new StringBuilder();
        using var writer = new StringWriter(output);
        Console.SetOut(writer);

        Cli.Run(new[] { "offline", "apply", "C:\\missing\\mount", "C:\\pkg.wupkg" }, services).GetAwaiter().GetResult();

        Assert.Contains("Apply failed: not mounted", output.ToString());
    }

    [Fact]
    public void OfflineDismount_prints_dismount_failed_when_mount_missing()
    {
        var offline = new Mock<IOfflineImageService>();
        offline.Setup(x => x.DismountAsync(It.IsAny<string>(), It.IsAny<bool>(), default)).ReturnsAsync(new OfflineImageResult { Success = false, Message = "missing" });
        var services = BuildServices((typeof(IOfflineImageService), offline.Object));

        var output = new StringBuilder();
        using var writer = new StringWriter(output);
        Console.SetOut(writer);

        Cli.Run(new[] { "offline", "dismount", "C:\\missing\\mount" }, services).GetAwaiter().GetResult();

        Assert.Contains("Dismount failed: missing", output.ToString());
    }
}
