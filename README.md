# Windows Update and Package Manager (WUPM)

Personal-first, offline-capable Windows update and package manager for Windows 10 and older.

## Building

```bash
dotnet restore
dotnet build
dotnet test
dotnet run -- --help
```

## CLI

```bash
dotnet run -- sync --repo https://github.com/LoopyLuci/WindowsUpdateAndPackageManager
dotnet run -- install firefox
dotnet run -- search browser
dotnet run -- list-available
dotnet run -- installed
dotnet run -- rollback
dotnet run -- audit
dotnet run -- windows-update
dotnet run -- driver-update
dotnet run -- policy-allow firefox
dotnet run -- policy-deny badapp
dotnet run -- health
dotnet run -- pack ./myapp ./out
dotnet run -- self-update
dotnet run -- delta-update myapp 1.0
dotnet run -- offline mount C:\images\install.wim
dotnet run -- offline apply <mountPath> C:\packages\driver.cab
dotnet run -- offline dismount <mountPath>
```

## PowerShell

Import `src/WindowsUpdateAndPackageManager.Core/PowerShell/WindowsUpdateAndPackageManager.psd1` and use cmdlets like `Sync-WUPMRepository`, `Install-WUPMPackage`, `Invoke-WUPMDriverUpdate`, `Get-WUPMHealth`, `Invoke-WUPMDeltaUpdate`, `Mount-WUPOfflineImage`, `Dismount-WUPOfflineImage`, and `Apply-WUPMPackageToImage`.

## REST API

Run `dotnet run --project src/WupmApi` and use endpoints:
- `GET /` - API health
- `GET /packages?repositoryUrl=...` - List available packages
- `GET /installed` - List installed packages
- `POST /install` with `PackageManifest` body - Install package
- `POST /sync?repositoryUrl=...` - Sync repository index
- `POST /windows-update` - Run Windows Update scan/install
- `GET /audit?from=&to=&action=` - Query audit log

## Contributing

See `CONTRIBUTING.md`.

## Roadmap

- [x] Manifest authoring guide
- [x] First curated package set
- [x] Authenticode verification
- [x] Windows Update Agent integration
- [ ] REST API surface for remote management
- [ ] Signed package catalog
- [ ] Delta update support
