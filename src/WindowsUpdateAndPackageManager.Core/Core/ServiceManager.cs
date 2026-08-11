using System.Diagnostics;
using System.Runtime.InteropServices;

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

        try
        {
            dynamic? taskService = null;
            try
            {
                taskService = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")!)!;
                taskService.Connect();

                var rootFolder = taskService.GetFolder(@"\");
                var folder = rootFolder.GetFolder(TaskPath);

                var definition = taskService.NewTask(0);
                definition.RegistrationInfo.Description = "WUPM automatic sync and Windows Update";
                definition.RegistrationInfo.Author = "WUPM";

                var trigger = definition.Triggers.Create(2);
                trigger.StartBoundary = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
                trigger.Enabled = true;

                var action = definition.Actions.Create(0);
                action.Path = _wupmPath;
                action.Arguments = $"sync --repo \"{repo}\"";

                definition.Settings.Enabled = true;
                definition.Settings.AllowDemandStart = true;
                definition.Settings.DisallowStartIfOnBatteries = false;
                definition.Settings.StopIfGoingOnBatteries = false;

                folder.RegisterTaskDefinition(
                    TaskName,
                    definition,
                    6,
                    null,
                    null,
                    3);

                return true;
            }
            finally
            {
                if (taskService is not null)
                {
                    Marshal.ReleaseComObject(taskService);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Service install failed: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> UninstallAsync(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        if (!IsRunningAsAdministrator())
        {
            Console.WriteLine("Service uninstall requires administrator privileges. Please run from an elevated terminal.");
            return false;
        }

        try
        {
            dynamic? taskService = null;
            try
            {
                taskService = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")!)!;
                taskService.Connect();

                var rootFolder = taskService.GetFolder(@"\");
                try
                {
                    var folder = rootFolder.GetFolder(TaskPath);
                    folder.DeleteTask(TaskName, 0);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            finally
            {
                if (taskService is not null)
                {
                    Marshal.ReleaseComObject(taskService);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Service uninstall failed: {ex.Message}");
            return false;
        }
    }

    public async Task<string?> StatusAsync(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        try
        {
            dynamic? taskService = null;
            try
            {
                taskService = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")!)!;
                taskService.Connect();

                var rootFolder = taskService.GetFolder(@"\");
                try
                {
                    var folder = rootFolder.GetFolder(TaskPath);
                    var task = folder.GetTask(TaskName);
                    var state = task.State;
                    var lastRun = task.LastRunTime.ToString("u");
                    var nextRun = task.NextRunTime.ToString("u");
                    return $"State={state}; LastRun={lastRun}; NextRun={nextRun}";
                }
                catch
                {
                    return "Task not found.";
                }
            }
            finally
            {
                if (taskService is not null)
                {
                    Marshal.ReleaseComObject(taskService);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Service status failed: {ex.Message}");
            return null;
        }
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
}
