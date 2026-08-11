using System.IO;
using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

[Collection("Console")]
public sealed class VerifyCliTests
{
    [Fact]
    public async Task Verify_command_reports_pass_for_valid_file_with_sha()
    {
        var services = WindowsUpdateAndPackageManager.Commands.Composition.Build(AppContext.BaseDirectory);
        try
        {
            var packagePath = Path.Combine(Path.GetTempPath(), "wupm-verify-" + Guid.NewGuid().ToString("N") + ".txt");
            File.WriteAllText(packagePath, "wupm-package");
            using var sha = System.Security.Cryptography.SHA256.Create();
            await using var stream = File.OpenRead(packagePath);
            var hash = await sha.ComputeHashAsync(stream);
            var expectedSha = Convert.ToHexString(hash).ToLowerInvariant();

            var output = new StringWriter();
            var original = Console.Out;
            try
            {
                Console.SetOut(output);
                await WindowsUpdateAndPackageManager.Commands.Cli.Run(new[] { "verify", packagePath, "--sha256", expectedSha }, services);
            }
            finally
            {
                Console.SetOut(original);
            }
            Assert.Contains("Verify passed:", output.ToString());
        }
        finally
        {
            if (services is IDisposable d) d.Dispose();
        }
    }

    [Fact]
    public async Task Verify_command_reports_fail_when_sha_mismatch()
    {
        var services = WindowsUpdateAndPackageManager.Commands.Composition.Build(AppContext.BaseDirectory);
        try
        {
            var packagePath = Path.Combine(Path.GetTempPath(), "wupm-verify-" + Guid.NewGuid().ToString("N") + ".txt");
            File.WriteAllText(packagePath, "wupm-package");

            var output = new StringWriter();
            var original = Console.Out;
            try
            {
                Console.SetOut(output);
                await WindowsUpdateAndPackageManager.Commands.Cli.Run(new[] { "verify", packagePath, "--sha256", "deadbeef" }, services);
            }
            finally
            {
                Console.SetOut(original);
            }
            Assert.Contains("Verify failed:", output.ToString());
        }
        finally
        {
            if (services is IDisposable d) d.Dispose();
        }
    }
}
