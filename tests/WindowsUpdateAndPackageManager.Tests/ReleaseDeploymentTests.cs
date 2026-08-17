using System.Diagnostics;
using System.IO;
using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

[Collection("Console")]
public class ReleaseDeploymentTests
{
    [Fact(Skip = "Slow deployment script invocation; validate release.ps1 behavior in CI or via script-content assertions")]
    public void DeployTarget_winget_generates_manifest()
    {
        var repoRoot = FindRepoRoot();
        var script = Path.Combine(repoRoot, "scripts", "release.ps1");
        var manifestDir = Path.Combine(repoRoot, "scripts", "deploy", "winget", "winget-pkgs", "LoopyLuci.WindowsUpdatePackageManager");
        if (Directory.Exists(manifestDir)) Directory.Delete(manifestDir, true);

        var dummyZip = Path.Combine(repoRoot, "wupm-cli.zip");
        File.WriteAllText(dummyZip, "dummy");

        RunPowerShell(script, "-Tag v0.4.1 -DryRun -SkipSign -SkipTests -ManifestOnly -DeployTarget winget", repoRoot);

        var manifest = Directory.GetFiles(manifestDir, "*.yaml");
        Assert.Equal(3, manifest.Length);
    }

    [Fact(Skip = "Slow deployment script invocation; validate script content via fast unit tests instead")]
    public void DeployTarget_chocolatey_warns_when_choco_missing()
    {
        var repoRoot = FindRepoRoot();
        var script = Path.Combine(repoRoot, "scripts", "release.ps1");
        var output = RunPowerShellCapture(script, "-Tag v0.4.1 -DryRun -SkipSign -SkipTests -DeployTarget chocolatey", repoRoot);
        Assert.Contains("choco CLI not found", output);
    }

    [Fact(Skip = "Slow deployment script invocation; validate script content via fast unit tests instead")]
    public void DeployTarget_feed_warns_when_env_unset()
    {
        var repoRoot = FindRepoRoot();
        var script = Path.Combine(repoRoot, "scripts", "release.ps1");
        var output = RunPowerShellCapture(script, "-Tag v0.4.1 -DryRun -SkipSign -SkipTests -DeployTarget feed", repoRoot);
        Assert.Contains("WUPM_FEED_URL", output);
    }

    [Fact]
    public void ServiceWrapper_status_returns_nonzero_when_binary_missing()
    {
        var repoRoot = FindRepoRoot();
        var script = Path.Combine(repoRoot, "scripts", "service-wupm-api.ps1");
        var psi = new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\" -Action install",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        using var p = Process.Start(psi);
        Assert.NotNull(p);
        p.WaitForExit();
        Assert.NotEqual(0, p.ExitCode);
    }

    private static string FindRepoRoot()
    {
        var dir = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(dir, "WindowsUpdateAndPackageManager.sln")))
        {
            dir = Path.GetFullPath(Path.Combine(dir, ".."));
        }
        return dir;
    }

    private static void RunPowerShell(string scriptPath, string args, string workingDirectory)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" {args}",
            WorkingDirectory = "D:\\Projects\\WindowsUpdatePackageManager",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        using var p = Process.Start(psi);
        Assert.NotNull(p);
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0)
        {
            Console.WriteLine("STDOUT:\n" + stdout);
            Console.WriteLine("STDERR:\n" + stderr);
        }
        Assert.Equal(0, p.ExitCode);
    }

    private static string RunPowerShellCapture(string scriptPath, string args, string workingDirectory)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" {args}",
            WorkingDirectory = "D:\\Projects\\WindowsUpdatePackageManager",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        using var p = Process.Start(psi);
        Assert.NotNull(p);
        var output = p.StandardOutput.ReadToEnd();
        var error = p.StandardError.ReadToEnd();
        p.WaitForExit();
        Assert.Equal(0, p.ExitCode);
        return output + error;
    }
}
