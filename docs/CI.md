# Local CI/CD

This repo no longer uses GitHub Actions workflows. All build, test, publish, and release steps are performed via local PowerShell scripts.

## Prerequisites

- Windows with PowerShell 5.1 or PowerShell 7+
- .NET 10 SDK
- `gh` CLI authenticated with repo access for release publishing
- Optional: code-signing certificate in the current user or machine store for artifact signing

## CI script

Run the full CI pipeline locally:

```powershell
pwsh ./scripts/ci.ps1
```

Common options:

```powershell
# Skip tests
pwsh ./scripts/ci.ps1 -SkipTests

# Skip publish/zip/SBOM/signing
pwsh ./scripts/ci.ps1 -SkipPublish

# Skip signing
pwsh ./scripts/ci.ps1 -SkipSign

# Use AzureSignTool
pwsh ./scripts/ci.ps1 `
  -SigningClientId $env:AZURE_SIGNING_CLIENT_ID `
  -SigningTenantId $env:AZURE_SIGNING_TENANT_ID `
  -SigningSecret $env:AZURE_SIGNING_SECRET `
  -KeyVaultUrl $env:AZURE_KEY_VAULT_URL
```

Outputs in repo root:
- `publish/cli/` - published CLI outputs
- `publish/api/` - published API outputs
- `wupm-cli.zip` - standalone CLI artifact
- `wupm-api.zip` - standalone API artifact
- `sbom.json` - CycloneDX SBOM

## Release script

Create or update a GitHub Release for an existing tag:

```powershell
pwsh ./scripts/release.ps1 -Tag v0.4.0
```

Common options:

```powershell
# Dry run: build artifacts without publishing the release
pwsh ./scripts/release.ps1 -Tag v0.4.0 -DryRun

# Skip tests during release build
pwsh ./scripts/release.ps1 -Tag v0.4.0 -SkipTests

# Skip signing during release
pwsh ./scripts/release.ps1 -Tag v0.4.0 -SkipSign
```

The release script:
1. Validates tag format
2. Runs CI
3. Creates or updates the GitHub Release with `wupm-cli.zip`, `wupm-api.zip`, and `sbom.json`

## Typical workflow

```powershell
# 1. Run CI locally
pwsh ./scripts/ci.ps1

# 2. Create and push a tag
git tag -a v0.4.0 -m "Release v0.4.0"
git push origin v0.4.0

# 3. Build and publish release assets
pwsh ./scripts/release.ps1 -Tag v0.4.0
```

## Notes

- Signing is optional. If no certificate is available, the scripts continue without signing.
- If AzureSignTool is installed, it is used automatically when signing parameters are provided.
- `gh auth status` must succeed before `scripts/release.ps1` can publish or update a release.
