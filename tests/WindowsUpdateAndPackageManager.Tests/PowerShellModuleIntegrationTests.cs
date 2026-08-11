using System.Management.Automation;
using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

public sealed class PowerShellModuleIntegrationTests
{
    [Fact(Skip = "Blocked in this environment: missing e_sqlite3 native dependency for PowerShell module initialization.")]
    public void Module_can_be_imported()
    {
    }
}
