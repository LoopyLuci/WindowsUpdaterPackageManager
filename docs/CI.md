# Local CI/CD

This repo no longer uses GitHub Actions workflows. All build, test, publish, and release steps are performed via local PowerShell scripts.

## Prerequisites

- Windows with PowerShell 5.1 or PowerShell 7+
- .NET 10 SDK
- `gh` CLI authenticated with repo access for release publishing
- Optional: code-signing certificate in the current user or machine store for artifact signing
- Optional: Chocolatey CLI for `choco pack`/`choco push` packaging target. Install requires Administrative permissions; on non-admin shells use the non-admin install method from https://chocolatey.org/install

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

## Fast CI script

Run format/restore/build/test only, skipping publish, signing, and SBOM generation:

```powershell
pwsh ./scripts/ci-quick.ps1
```

Use this for fast pre-commit validation when you don't need release artifacts.

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

# Generate manifests only, skip CI and GitHub release
pwsh ./scripts/release.ps1 -Tag v0.4.0 -ManifestOnly -DeployTarget winget
```

The release script:
1. Validates tag format
2. Runs CI unless `-ManifestOnly` is set
3. Creates or updates the GitHub Release with `wupm-cli.zip`, `wupm-api.zip`, and `sbom.json`

## Typical workflow

```powershell
# 1. Validate release artifacts locally without publishing
pwsh ./scripts/release.ps1 -Tag v0.4.0 -DryRun

# 2. Inspect wupm-cli.zip, wupm-api.zip, and sbom.json

# 3. If everything looks good, create/update the GitHub release
pwsh ./scripts/release.ps1 -Tag v0.4.0
```

## Self-update validation

`wupm self-update` uses GitHub’s API and asset downloads. On this environment, unauthenticated requests may be blocked. To validate self-update on a Windows machine:

1. Ensure `gh` is authenticated: `gh auth status`
2. Set `GITHUB_TOKEN` with `contents:read` and `packages:read` permissions:
   ```powershell
   $env:GITHUB_TOKEN = '<token>'
   ```
3. Publish a test release:
   ```powershell
   pwsh ./scripts/release.ps1 -Tag v0.4.0-test -DryRun:$false
   ```
4. Run self-update against the test tag:
   ```powershell
   dotnet run --project src/Wupm.Cli -- self-update --tag v0.4.0-test
   ```
   Or with explicit token:
   ```powershell
   dotnet run --project src/Wupm.Cli -- self-update --tag v0.4.0-test --token $env:GITHUB_TOKEN
   ```
5. Verify the updater replaced the executable and relaunched.

### Troubleshooting

- **403 Forbidden**: `GITHUB_TOKEN` is missing or lacks `contents:read`. Re-run `gh auth login` with `contents:write` and `packages:read`.
- **Asset missing**: Confirm `wupm-cli.zip` is attached to the test release. Re-run `release.ps1` without `-DryRun`.
- **Relaunch failed**: Check `%TEMP%\wupm-selfupdate-*\apply-update.ps1` for execution-policy or path issues.
- **Rate limit**: Authenticated requests have higher rate limits. If unauthenticated, use `--token` or set `$env:GITHUB_TOKEN`.

## Code-signing rehearsal

To exercise signing without publishing:

```powershell
pwsh ./scripts/release.ps1 -Tag v0.4.0-test -DryRun -SkipSign:$false
```

This builds artifacts and runs the signing step, but skips `gh release` upload. If no certificate is available, it logs a warning and continues.

## Deployment automation

`scripts/release.ps1` includes a deployment hook after release creation. Currently this is a placeholder. To enable a real target, edit the script and implement one of:

- Winget manifest submission
- Chocolatey package push
- Internal artifact feed upload

The hook receives the release tag and artifact paths.

## Scheduled-task CI runner

On Windows, you can run CI on a schedule without GitHub Actions by creating a Scheduled Task:

```powershell
$action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument '-NoProfile -ExecutionPolicy Bypass -File D:\Projects\WindowsUpdatePackageManager\scripts\ci.ps1'
$trigger = New-ScheduledTaskTrigger -Daily -At 09:00
Register-ScheduledTask -TaskName 'WUPM CI' -Action $action -Trigger $trigger -Description 'Daily WUPM CI run'
```

Capture output to a log file for audit by appending `> C:\Logs\wupm-ci.log 2>&1` to the argument list.

## Notes

- Signing is optional. If no certificate is available, the scripts continue without signing.
- If AzureSignTool is installed, it is used automatically when signing parameters are provided.
- `gh auth status` must succeed before `scripts/release.ps1` can publish or update a release.

## Git hook automation

Install the post-commit hook to run local CI automatically after each commit:

```powershell
pwsh ./scripts/install-hook.ps1
```

Uninstall:

```powershell
pwsh ./scripts/install-hook.ps1 -Uninstall
```

The hook runs `scripts/ci.ps1 -SkipSign` from the repo root. If CI fails, the commit is not accepted.

## Rollback and cleanup

- Delete a test GitHub Release:
  ```powershell
  gh release delete v0.4.1-test --repo LoopyLuci/WindowsUpdatePackageManager --yes
  ```
- Delete a local tag:
  ```powershell
  git tag -d v0.4.1-test
  ```
- Clean local publish outputs:
  ```powershell
  Remove-Item -Force -Recurse publish, wupm-cli.zip, wupm-api.zip, sbom.json, 'C:\Users\limpi\AppData\Local\Temp\wupm-pack-output-*'
  ```
- If a signing certificate is compromised, remove it from the store and rotate `WUPM_API_KEY` and any deployment target credentials.
