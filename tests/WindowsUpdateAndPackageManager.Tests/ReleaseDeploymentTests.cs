using System.Diagnostics;
using System.IO;
using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

[Collection("Console")]
public class ReleaseDeploymentTests
{
    [Fact]
    public void DeployTarget_winget_generates_manifest()
    {
        var repoRoot = FindRepoRoot();
        var script = Path.Combine(repoRoot, "scripts", "release.ps1");
        var manifestDir = Path.Combine(repoRoot, "scripts", "deploy", "winget", "winget-pkgs", "LoopyLuci.WindowsUpdatePackageManager");
        if (Directory.Exists(manifestDir)) Directory.Delete(manifestDir, true);

        RunPowerShell(script, "-Tag v0.4.1-test -DryRun -SkipSign -DeployTarget winget", repoRoot);

        var manifest = Directory.GetFiles(manifestDir, "*.yaml");
        Assert.Single(manifest);
    }

    [Fact]
    public void DeployTarget_chocolatey_warns_when_choco_missing()
    {
        var repoRoot = FindRepoRoot();
        var script = Path.Combine(repoRoot, "scripts", "release.ps1");
        var output = RunPowerShellCapture(script, "-Tag v0.4.1-test -DryRun -SkipSign -DeployTarget chocolatey", repoRoot);
        Assert.Contains("choco CLI not found", output);
    }

    [Fact]
    public void DeployTarget_feed_warns_when_env_unset()
    {
        var repoRoot = FindRepoRoot();
        var script = Path.Combine(repoRoot, "scripts", "release.ps1");
        var output = RunPowerShellCapture(script, "-Tag v0.4.1-test -DryRun -SkipSign -DeployTarget feed", repoRoot);
        Assert.Contains("WUPM_FEED_URL", output);
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
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        using var p = Process.Start(psi);
        Assert.NotNull(p);
        p.WaitForExit();
        Assert.Equal(0, p.ExitCode);
    }

    private static string RunPowerShellCapture(string scriptPath, string args, string workingDirectory)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" {args}",
            WorkingDirectory = workingDirectory,
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
