# Contributing to WUPM

Thank you for your interest in improving Windows Update and Package Manager. This project prioritizes personal use and enterprise-grade reliability.

## Dev setup

- Windows 10/11
- .NET SDK 10+
- PowerShell 7+

```powershell
git clone git@github.com:LoopyLuci/WindowsUpdateAndPackageManager.git
cd WindowsUpdateAndPackageManager
.\build.ps1 -Configuration Debug
.\run-tests.cmd
```

## Plugin SDK

WUPM supports plugins through the `IPlugin` interface located in `src/WindowsUpdateAndPackageManager.Core/Core/IPlugin.cs`.

### Creating a plugin

1. Create a new .NET class library project targeting `net10.0-windows`
2. Reference `WindowsUpdateAndPackageManager.Core`
3. Implement `IPlugin`:

```csharp
using WindowsUpdateAndPackageManager.Core;
using System.Threading.Tasks;

namespace MyPlugin;

public class MyPlugin : IPlugin
{
    public string Name => "my-plugin";
    public string Version => "1.0.0";

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // Plugin initialization logic
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GetCommandsAsync(CancellationToken cancellationToken = default)
    {
        // Return custom CLI commands
        return Task.FromResult<IReadOnlyList<string>>(new List<string>());
    }
}
```

4. Build the plugin as a `.dll` and place it in `.wupm/plugins/` or `.wupm/data/plugins/`
5. Run `wupm plugin list` to verify loading

## Branching

- `main` is protected. All changes via PR.
- Branch naming: `feat/<topic>`, `fix/<topic>`, `docs/<topic>`, `chore/<topic>`.

## Commit messages

Use Conventional Commits: `feat:`, `fix:`, `docs:`, `chore:`, `refactor:`, `test:`.

## Release process

1. Update `CHANGELOG.md`.
2. Create/merge PR into `main`.
3. Tag release: `git tag v0.x.y && git push --tags`.
