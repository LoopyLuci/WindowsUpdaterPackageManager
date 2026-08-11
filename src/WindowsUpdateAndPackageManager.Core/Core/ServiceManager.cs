using System.Diagnostics;

namespace WindowsUpdateAndPackageManager.Core;

public interface IServiceManager
{
    Task<bool> InstallAsync(string? repositoryUrl = null, string? schedule = null, CancellationToken cancellationToken = default);
    Task<bool> UninstallAsync(CancellationToken cancellationToken = default);
    Task<string?> StatusAsync(CancellationToken cancellationToken = default);
}

public sealed class ServiceManager : IServiceManager
{
    private const string TaskName = "WUPMAutoSync";
    private const string TaskPath = @"\WUPM";
    private readonly string _wupmPath;
    private readonly string? _repositoryUrl;

    public ServiceManager(string? wupmPath = null, string? repositoryUrl = null)
    {
        _wupmPath = wupmPath ?? Environment.ProcessPath ?? throw new InvalidOperationException("Cannot determine current executable path.");
        _repositoryUrl = repositoryUrl;
    }

    public async Task<bool> InstallAsync(string? repositoryUrl = null, string? schedule = null, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        if (!IsRunningAsAdministrator())
        {
            Console.WriteLine("Service install requires administrator privileges. Please run from an elevated terminal.");
            return false;
        }

        var repo = string.IsNullOrWhiteSpace(repositoryUrl) ? _repositoryUrl : repositoryUrl;
        if (string.IsNullOrWhiteSpace(repo))
        {
            throw new InvalidOperationException("Repository URL is required for service install.");
        }

        var sc = ParseSchedule(schedule) ?? "DAILY";
        var st = ParseStartTime(schedule) ?? "09:00";

        var args = $"sync --repo \"{repo}\"";
        var psi = new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            ArgumentList = { "/Create", "/TN", TaskName, "/TR", $"\"{_wupmPath}\" {args}", "/SC", sc, "/ST", st, "/F" },
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi);
        if (proc is null) return false;
        await proc.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return proc.ExitCode == 0;
    }

    public async Task<bool> UninstallAsync(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        if (!IsRunningAsAdministrator())
        {
            Console.WriteLine("Service uninstall requires administrator privileges. Please run from an elevated terminal.");
            return false;
        }

        var psi = new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            ArgumentList = { "/Delete", "/TN", TaskName, "/F" },
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi);
        if (proc is null) return false;
        await proc.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return proc.ExitCode == 0;
    }

    public async Task<string?> StatusAsync(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        var psi = new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            ArgumentList = { "/Query", "/TN", TaskName, "/FO", "LIST", "/V" },
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true
        };

        using var proc = Process.Start(psi);
        if (proc is null) return null;
        var output = await proc.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        await proc.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return proc.ExitCode == 0 ? output : null;
    }

    private static bool IsRunningAsAdministrator()
    {
        try
        {
            var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private static string? ParseSchedule(string? schedule)
    {
        if (string.IsNullOrWhiteSpace(schedule)) return null;
        return schedule.Trim().ToUpperInvariant() switch
        {
            "DAILY" or "HOURLY" or "MINUTE" => schedule.Trim().ToUpperInvariant(),
            _ => null
        };
    }

    private static string? ParseStartTime(string? schedule)
    {
        if (string.IsNullOrWhiteSpace(schedule)) return null;
        var parts = schedule.Trim().Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 2 && int.TryParse(parts[0], out _) && int.TryParse(parts[1], out _))
        {
            return schedule.Trim();
        }
        return null;
    }
}
