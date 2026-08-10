using System.Diagnostics;
using WindowsUpdateAndPackageManager.Data;
using WindowsUpdateAndPackageManager.Infrastructure;
using WindowsUpdateAndPackageManager.Models;

namespace WindowsUpdateAndPackageManager.Core;

public sealed class PackageManager : IPackageManager
{
    private readonly IStateDatabase _state;
    private readonly IAuditStore _auditStore;

    public PackageManager(IStateDatabase state, IAuditStore auditStore)
    {
        _state = state;
        _auditStore = auditStore;
    }

    public async Task<IReadOnlyList<PackageManifest>> ListInstalledAsync(CancellationToken cancellationToken = default)
        => await _state.ListInstalledAsync(cancellationToken).ConfigureAwait(false);

    public async Task<bool> IsInstalledAsync(string packageId, string? version = null, CancellationToken cancellationToken = default)
        => await _state.IsInstalledAsync(packageId, version, cancellationToken).ConfigureAwait(false);

    public async Task<InstallResult> InstallAsync(PackageManifest package, CancellationToken cancellationToken = default)
    {
        var result = new InstallResult { PackageId = package.Id };
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = package.InstallCommand,
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };
            using var p = Process.Start(psi);
            if (p is null) throw new InvalidOperationException("Process did not start.");

            await p.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            result.Success = p.ExitCode == 0;
            if (!result.Success)
            {
                result.Message = $"Installer exited with code {p.ExitCode}.";
            }
            else
            {
                await _state.RecordInstallAsync(package, cancellationToken).ConfigureAwait(false);
                result.InstalledVersion = package.Version;
            }
        }
        catch (Exception ex)
        {
            result.Message = ex.Message;
        }
        finally
        {
            await _auditStore.AppendAsync(new AuditEntry
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTimeOffset.UtcNow,
                Action = "Package.Install",
                PackageId = package.Id,
                Version = package.Version,
                Success = result.Success,
                Message = result.Message
            }, cancellationToken).ConfigureAwait(false);
        }
        return result;
    }

    public async Task<UninstallResult> UninstallAsync(string packageId, CancellationToken cancellationToken = default)
    {
        var result = new UninstallResult { PackageId = packageId };
        try
        {
            var installed = await _state.ListInstalledAsync(cancellationToken).ConfigureAwait(false);
            var target = installed.FirstOrDefault(x => x.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase));
            if (target is null)
            {
                result.Message = "Package is not installed.";
                return result;
            }

            var psi = new ProcessStartInfo
            {
                FileName = target.UninstallCommand,
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };
            using var p = Process.Start(psi);
            if (p is null) throw new InvalidOperationException("Uninstaller did not start.");

            await p.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            result.Success = p.ExitCode == 0;
            if (!result.Success)
            {
                result.Message = $"Uninstaller exited with code {p.ExitCode}.";
            }
            else
            {
                await _state.RemoveInstallAsync(packageId, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            result.Message = ex.Message;
        }
        finally
        {
            await _auditStore.AppendAsync(new AuditEntry
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTimeOffset.UtcNow,
                Action = "Package.Uninstall",
                PackageId = packageId,
                Success = result.Success,
                Message = result.Message
            }, cancellationToken).ConfigureAwait(false);
        }
        return result;
    }
}
