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
dotnet run -- verify ./package.zip --sha256 <hex>
dotnet run -- delta-update --id myapp --from 1.0
dotnet run -- offline mount C:\images\install.wim
dotnet run -- offline apply <mountPath> C:\packages\driver.cab
dotnet run -- offline dismount <mountPath>
dotnet run -- service install --repo https://github.com/LoopyLuci/WindowsUpdateAndPackageManager
dotnet run -- service status
dotnet run -- service uninstall
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

### API Authentication

The REST API supports optional authentication. Enable it by setting the `WUPM_API_KEY` environment variable. When set, endpoints require one of:
- `Authorization: Bearer ***`
- `X-Api-Key: ***`

Unauthenticated requests receive `401 Unauthorized` when auth is enabled. When `WUPM_API_KEY` is not set, the API remains open for local use.

### mTLS Client Certificates

Enable client certificate enforcement with:
- `WUPM_API_MTLS_ENABLED=true`
- `WUPM_API_MTLS_ALLOWED_THUMBPRINTS=<thumbprint1>,<thumbprint2>`

Use `*` to allow any client certificate thumbprint during staged rollout. Missing or disallowed certificates return `403 Forbidden`. The `/` health endpoint still enforces mTLS when enabled.

### Deployment Guidance

Recommended production deployment pattern:
- Place WupmApi behind a reverse proxy or load balancer with TLS termination.
- Require HTTPS from clients; avoid exposing HTTP externally.
- Rotate `WUPM_API_KEY` regularly and store it in a secret manager, not on disk.
- For mTLS, provision client certificates per operator/managed node and store thumbprints in `WUPM_API_MTLS_ALLOWED_THUMBPRINTS`.
- Limit network access to audit and package endpoints according to your trust model.

## Security Notes

- Set `WUPM_API_KEY` to enable REST API auth. Unauthenticated requests receive `401 Unauthorized`. The `/` health endpoint remains accessible when auth is enabled.
- Audit logs are written to `logs/wupm-api-.log` and local SQLite state. Treat these files as sensitive; they contain package action metadata. Rotate logs and restrict filesystem access.
- `wupm publish` and `SelfUpdater` use `GITHUB_TOKEN` only as a Bearer token in `Authorization` headers; values are not logged or persisted by WUPM.
- Package install verifies Authenticode signatures when a signature verifier is configured. By default, unsigned packages are blocked unless `AllowUntrusted` is explicitly enabled in policy. Maintainers should sign release artifacts with a trusted code-signing certificate before publishing.

## CI/CD

This repo uses local PowerShell CI/release scripts instead of GitHub Actions workflows.

```powershell
# Run CI locally
pwsh ./scripts/ci.ps1

# Run CI without tests
pwsh ./scripts/ci.ps1 -SkipTests

# Install post-commit hook to run CI automatically after each commit
pwsh ./scripts/install-hook.ps1

# Validate a release without publishing
pwsh ./scripts/release.ps1 -Tag v0.2.0 -DryRun

# Create/update a GitHub release for a tag
pwsh ./scripts/release.ps1 -Tag v0.2.0
```

`scripts/ci.ps1` performs restore, build, test, publish, zip, SBOM generation, and optional signing. `scripts/release.ps1` runs CI, then creates or updates the GitHub Release assets with `wupm-cli.zip`, `wupm-api.zip`, and `sbom.json`.

### Recommended release flow

1. Run `pwsh ./scripts/release.ps1 -Tag v0.2.0 -DryRun`
2. Inspect `wupm-cli.zip`, `wupm-api.zip`, and `sbom.json`
3. If everything looks good, run `pwsh ./scripts/release.ps1 -Tag v0.2.0`

### Triggering a release

1. Ensure `gh` is installed and authenticated with repo access.
2. Create and push an annotated tag:
   ```bash
   git tag -a v0.2.0 -m "Release v0.2.0"
   git push origin v0.2.0
   ```
3. Run the local release script to build and publish release assets:
   ```bash
   pwsh ./scripts/release.ps1 -Tag v0.2.0
   ```
   This produces two artifacts:
   - `wupm-cli.zip` - standalone CLI
   - `wupm-api.zip` - standalone API host

### Code signing

`scripts/release.ps1` supports two signing paths:

For faster local validation, use `scripts/ci-quick.ps1` to run format, restore, build, and test without publish/zip/signing.

Generate manifests without running full CI:

```powershell
pwsh ./scripts/release.ps1 -Tag v0.2.0 -ManifestOnly -DeployTarget winget
```
- AzureSignTool with Key Vault client credentials (`-SigningClientId`, `-SigningTenantId`, `-SigningSecret`, `-KeyVaultUrl`)
- Local code-signing certificate from `Cert:\CurrentUser\My`

To rehearse signing without publishing:

```powershell
pwsh ./scripts/release.ps1 -Tag v0.2.0-test -DryRun -SkipSign:$false
```

If no certificate is configured, the script skips signing with a warning.

### Deployment automation

`scripts/release.ps1` includes an optional deployment hook after release creation.

Supported targets:
- `chocolatey`: builds `wupm-cli.nupkg` from `wupm-cli.zip` with an install script, then optionally pushes to Chocolatey.org when `CHOCO_API_KEY` is set.
- `feed`: uploads `wupm-cli.zip` to an internal artifact feed when `WUPM_FEED_URL` and `WUPM_FEED_API_KEY` are set.
- `winget`: generates a versioned manifest under `scripts/deploy/winget/winget-pkgs/LoopyLuci.WindowsUpdatePackageManager/`.

Example usage:
```powershell
pwsh ./scripts/release.ps1 -Tag v0.2.0 -DeployTarget chocolatey
pwsh ./scripts/release.ps1 -Tag v0.2.0 -DeployTarget feed
```

### Chocolatey quick-install

If published to Chocolatey.org:
```powershell
choco install wupm-cli
```

Manual fallback:
```powershell
choco install wupm-cli -Source https://push.chocolatey.org/
```

If you maintain your own feed:
```powershell
choco install wupm-cli -Source https://your-feed.example.com/v3/index.json
```

### Winget manifest

Generate a versioned manifest with:
```powershell
pwsh ./scripts/release.ps1 -Tag v0.2.0 -DryRun -DeployTarget winget
```

The manifest is written to:
- `scripts/deploy/winget/winget-pkgs/LoopyLuci.WindowsUpdatePackageManager/<version>.yaml`

Validate syntax with:
```powershell
winget validate <manifest>
```

Commit the manifest under `winget-pkgs/` for submission to the Winget repository.

On Windows, you can run CI on a schedule without GitHub Actions by creating a Scheduled Task that runs:
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File D:\Projects\WindowsUpdatePackageManager\scripts\ci.ps1 -AsHook
```

Set it to run on your desired cadence, e.g., daily at 09:00. Capture output to a log file for audit.

### Post-commit hook status output

When installed via `scripts/install-hook.ps1`, the post-commit hook writes CI results to `.git/hooks/output/` so commit status is visible without GitHub Actions.

### Authenticode signing

WUPM verifies Authenticode signatures when the signature verifier is configured. To publish signed artifacts:

1. Obtain a code-signing certificate and install it in the current user or machine store.
2. Sign published executables before zipping:
   ```powershell
   $cert = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert | Select-Object -First 1
   Set-AuthenticodeSignature -FilePath .\publish\cli\Wupm.Cli.exe -Certificate $cert
   Set-AuthenticodeSignature -FilePath .\publish\api\WupmApi.exe -Certificate $cert
   ```
3. Zip the signed outputs and publish as release assets.

Unsigned packages are blocked by default unless `AllowUntrusted` is explicitly enabled in policy. Treat unsigned downloads as untrusted.

## Contributing

See `CONTRIBUTING.md`.

## Windows Service wrapper validation

`scripts/service-wupm-api.ps1` supports `install`, `start`, `stop`, `uninstall`, and `status` actions. `install`, `start`, `stop`, and `uninstall` require Administrator privileges. `status` works without admin rights.

Validation steps:
1. Publish API binaries: `pwsh ./scripts/ci.ps1 -SkipTests`
2. Install service: `pwsh ./scripts/service-wupm-api.ps1 -Action install`
3. Start service: `pwsh ./scripts/service-wupm-api.ps1 -Action start`
4. Check status: `pwsh ./scripts/service-wupm-api.ps1 -Action status`
5. Stop service: `pwsh ./scripts/service-wupm-api.ps1 -Action stop`
6. Uninstall service: `pwsh ./scripts/service-wupm-api.ps1 -Action uninstall`

If installation fails, verify `WUPM_API_KEY` and mTLS settings are configured before starting the service.

## Repository Setup

Recommended `main` branch protection:

- Require status checks: `build`, `test`
- Require branches to be up to date before merging
- Require a pull request before merging
- Require 1 approving review
- Require review from `CODEOWNERS`
- Include administrators in restrictions
- Disable force pushes
- Disable deletions

`CODEOWNERS` is configured so `@LoopyLuci` reviews all changes.

## Repository Schema Versioning

- Current schema version: `1.0`
- WUPM validates `schemaVersion` strictly; unknown versions are rejected.
- Additive field additions must bump `schemaVersion` and be reflected in `repo/index.schema.json`.
- Removal or renaming of fields requires a new major schema version and a migration path documented in `RELEASES.md`.
- The parser rejects indices with missing or unsupported `schemaVersion` values.
