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

```bash
# Run CI locally
pwsh ./scripts/ci.ps1

# Run CI without tests
pwsh ./scripts/ci.ps1 -SkipTests

# Create/update a GitHub release for a tag
pwsh ./scripts/release.ps1 -Tag v0.4.0
```

`scripts/ci.ps1` performs restore, build, test, publish, zip, SBOM generation, and optional signing. `scripts/release.ps1` runs CI, then creates or updates the GitHub Release assets with `wupm-cli.zip`, `wupm-api.zip`, and `sbom.json`.

## Releases

Pushing a tag like `v0.2.0` triggers the release workflow, which builds and publishes `wupm-cli.zip` and `wupm-api.zip` as GitHub Release assets.

### Delta packaging

Use `wupm pack` with a `manifest.json` that includes `previousSha256` to produce a `.delta.json`:

```json
{
  "id": "windows-update-bundle",
  "version": "2.0",
  "previousSha256": "<sha256 of previous package>"
}
```

Output includes `deltaAvailable` and `previousSha256` in `.delta.json`. Apply deltas with `wupm delta-update --id <id> --from <version>` or combine with offline servicing via `wupm delta-apply --id <id> --from <version> --mountPath <path>`.

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

### Installing a release locally

```bash
# Download from the GitHub Release page and extract
Expand-Archive -Path wupm-cli.zip -DestinationPath C:\Tools\wupm
# or for API host
Expand-Archive -Path wupm-api.zip -DestinationPath C:\Tools\wupm-api
```

### Local publish workflow

```bash
dotnet run --project src/Wupm.Cli -- publish --tag v0.2.0-test --dry-run
dotnet run --project src/Wupm.Cli -- publish --tag v0.2.0-test --token $env:GITHUB_TOKEN
```

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
