using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace WupmGui.Services;

public static class ElevationService
{
    public static bool IsRunningAsAdministrator()
    {
        var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var principal = new System.Security.Principal.WindowsPrincipal(identity);
        return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }
}
