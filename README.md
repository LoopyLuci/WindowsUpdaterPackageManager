# Windows Update and Package Manager (WUPM)

Personal-first, offline-capable Windows update and package manager for Windows 10 and older.

## Architecture

```
Interface Layer
  - CLI
  - PowerShell module
  - REST API placeholder

Core Engine
  - WindowsUpdateManager
  - PackageManager
  - RollbackManager
  - RepoSync
  - Auditor

Infrastructure Layer
  - RepoClient
  - ManifestValidator
  - CacheManager
  - PolicyEngine

Data Layer
  - StateDatabase
  - AuditStore
```

## Repository manifest

This repo hosts package manifests and optional package assets. The canonical manifest file is `releases/latest/download/index.json` in this repository.

## Repo manifest schema (`index.json`)

```json
{
  "schemaVersion": "1.0",
  "generatedAt": "2026-08-10T00:00:00Z",
  "repositoryUrl": "https://github.com/LoopyLuci/WindowsUpdateAndPackageManager",
  "packages": [
    {
      "id": "firefox",
      "version": "130.0",
      "displayName": "Mozilla Firefox",
      "description": "Web browser",
      "license": "MPL-2.0",
      "isDriver": false,
      "architecture": "x64",
      "minWindowsVersion": "Windows 10",
      "maxWindowsVersion": "Windows 11",
      "installCommand": "winget install Mozilla.Firefox",
      "uninstallCommand": "winget uninstall Mozilla.Firefox",
      "requiresReboot": false,
      "sha256": "",
      "publishedAt": "2026-08-10T00:00:00Z",
      "isDeprecated": false
    }
  ]
}
```

## Building

Requires .NET SDK 10.0+ and Windows.

```bash
dotnet restore
dotnet build
dotnet test
dotnet run -- --help
```

## Using the CLI

```bash
# sync repo index
dotnet run -- sync --repo https://github.com/LoopyLuci/WindowsUpdateAndPackageManager

# install package
dotnet run -- install firefox

# list installed
dotnet run -- installed

# rollback
dotnet run -- rollback

# audit
dotnet run -- audit

# windows update scan
dotnet run -- windows-update
```

## PowerShell module

See `src/WindowsUpdateAndPackageManager/PowerShell/WindowsUpdateAndPackageManager.psd1`.

## Offline design

- Repo index is a JSON manifest.
- Package install/uninstall paths are recorded in a local SQLite state database.
- Audit trail is stored in a separate SQLite database.
- Network sync is optional; cached install records remain usable offline.

## Roadmap

- Manifest authoring guide
- First curated package set
- Authenticode verification
- Windows Update Agent integration
- WIM/ISO support for offline Windows servicing

## License

MIT
