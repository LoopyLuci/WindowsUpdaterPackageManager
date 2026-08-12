<#
.SYNOPSIS
Local release script: validates tag, builds release artifacts, SBOM, signs, and publishes/updates a GitHub release.
#>
param(
  [Parameter(Mandatory=$true)]
  [string]$Tag,
  [string]$Configuration = 'Release',
  [switch]$SkipTests,
  [switch]$SkipSign,
  [switch]$DryRun,
  [string]$SigningClientId,
  [string]$SigningTenantId,
  [string]$SigningSecret,
  [string]$KeyVaultUrl,
  [string]$Repo = 'LoopyLuci/WindowsUpdateAndPackageManager'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $Tag -match '^v\d+\.\d+\.\d+') { throw "Tag must match semver, got '$Tag'." }
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { throw 'gh CLI is required for release publishing.' }

Push-Location 'D:\Projects\WindowsUpdatePackageManager'
try {
  & '.\scripts\ci.ps1' -Configuration $Configuration -SkipTests:$SkipTests -SkipSign:$SkipSign `
    -SigningClientId $SigningClientId -SigningTenantId $SigningTenantId -SigningSecret $SigningSecret -KeyVaultUrl $KeyVaultUrl

  Write-Host '--- GitHub Release ---'
  $assets = @('wupm-cli.zip','wupm-api.zip','sbom.json')
  $notes = @"
Release $Tag

Artifacts:
- wupm-cli.zip
- wupm-api.zip
- sbom.json
"@
  if ($DryRun) {
    Write-Host 'DryRun enabled; skipping gh release create/upload.'
  }
  else {
    $releaseJson = ''
    try {
      $releaseJson = gh release view $Tag --repo $Repo --json url 2>&1 | Out-String
    } catch {
      # ignore missing release
    }
    if ($releaseJson -notmatch 'url') {
      gh release create $Tag --repo $Repo --title "WUPM $Tag" --notes $notes @assets
    }
    else {
      foreach ($a in $assets) { if (Test-Path $a) { gh release upload $Tag --repo $Repo $a --clobber } }
    }
  }

  Write-Host '--- Optional deployment ---'
  Write-Host 'No deployment target configured. Hook Winget/Chocolatey/internal feed here if needed.'
}
finally {
  Pop-Location
}
