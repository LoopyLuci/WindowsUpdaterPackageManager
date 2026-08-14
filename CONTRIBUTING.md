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

1. Create a new .NET class library project targeting `net10.0`
2. Reference `WindowsUpdateAndPackageManager.Core`
3. Implement `IPlugin`:

```csharp
using System.Threading;
using System.Threading.Tasks;
using WindowsUpdateAndPackageManager.Core;

namespace MyPlugin;

public sealed class MyPlugin : IPlugin
{
    public string Name => "MyPlugin";
    public string Version => "1.0.0";

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GetCommandsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }
}
```

4. Build the plugin as a `.dll` and place it in `.wupm/plugins/` or register it with `wupm plugin registry add`
5. Run `wupm plugin list` to verify loading

### Plugin verification

Use `wupm plugin verify --path <dll>` to compute the SHA256 hash of a plugin assembly before registration. Administrators should verify hashes against trusted sources before installing plugins.

### Plugin marketplace

Use `wupm marketplace search <term>` to discover community plugins from a remote index. Review plugins carefully before installing.

## Branching

- `main` is protected. All changes via PR.
- Branch naming: `feat/<topic>`, `fix/<topic>`, `docs/<topic>`, `chore/<topic>`.

## Commit messages

Use Conventional Commits: `feat:`, `fix:`, `docs:`, `chore:`, `refactor:`, `test:`.

## Release process

1. Update `CHANGELOG.md`.
2. Create/merge PR into `main`.
3. Tag release: `git tag v0.x.y && git push --tags`.
