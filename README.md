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
```

## PowerShell

Import `src/WindowsUpdateAndPackageManager/PowerShell/WindowsUpdateAndPackageManager.psd1` and use cmdlets like `Sync-WUPMRepository`, `Install-WUPMPackage`, `Invoke-WUPMDriverUpdate`, and `Get-WUPMHealth`.

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
