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
  [string]$Repo = 'LoopyLuci/WindowsUpdatePackageManager',
  [string]$DeployConfig
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $Tag -match '^v\d+\.\d+\.\d+') { throw "Tag must match semver, got '$Tag'." }
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { throw 'gh CLI is required for release publishing.' }

function Deploy-Chocolatey {
  param([string]$Tag, [string]$Repo)
  $version = $Tag.TrimStart('v')
  $nuspec = Join-Path $PWD.Path "wupm.cli.$version.nuspec"
  $nupkg = Join-Path $PWD.Path "wupm.cli.$version.nupkg"
  $zip = Join-Path $PWD.Path "wupm-cli.zip"
  if (-not (Test-Path $zip)) { throw "wupm-cli.zip not found for Chocolatey packaging." }

  $nuspecContent = @"
<?xml version="1.0" encoding="utf-8"?>
<package xmlns="http://schemas.microsoft.com/packaging/2015/06/nuspec.xsd">
  <metadata>
    <id>wupm.cli</id>
    <version>$version</version>
    <authors>LoopyLuci</authors>
    <owners>LoopyLuci</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Windows Update Package Manager CLI</description>
    <tags>windows update package manager wupm</tags>
  </metadata>
  <files>
    <file src="$zip" target="tools\wupm-cli.zip" />
  </files>
</package>
"@
  Set-Content -Path $nuspec -Value $nuspecContent -Encoding UTF8
  if (Get-Command choco -ErrorAction SilentlyContinue) {
    choco pack $nuspec --out $PWD.Path
    if ($LASTEXITCODE -ne 0) { throw 'choco pack failed.' }
    if ($env:CHOCO_API_KEY) {
      choco push $nupkg --source https://push.chocolatey.org/ --api-key $env:CHOCO_API_KEY --force
      if ($LASTEXITCODE -ne 0) { Write-Warning 'choco push failed; check CHOCO_API_KEY.' }
    }
    else {
      Write-Host 'CHOCO_API_KEY not set; skipping choco push.'
    }
  }
  else {
    Write-Warning 'choco CLI not found; skipping Chocolatey packaging.'
  }
}

Push-Location 'D:\Projects\WindowsUpdatePackageManager'
try {
  & '.\scripts\ci.ps1' -Configuration $Configuration -SkipTests:$SkipTests -SkipSign:$SkipSign `
    -SigningClientId $SigningClientId -SigningTenantId $SigningTenantId -SigningSecret $SigningSecret -KeyVaultUrl $KeyVaultUrl

  Write-Host '--- GitHub Release ---'
  $assets = @('wupm-cli.zip','wupm-api.zip','sbom.json')
  $parent = git rev-parse --verify -q $Tag^ 2>$null
  $range = if ($parent) { "$parent..$Tag" } else { $Tag }
  $changes = ''
  $gitTagCheck = git cat-file -t $Tag 2>&1 | Out-String
  if ($LASTEXITCODE -eq 0) {
    $changes = git log --date=short --pretty=format:'- %ad %s' $range 2>&1 | Out-String
  }
  if (-not $changes) { $changes = '- Automated release via release.ps1' }
  $notes = @"
Release $Tag

Artifacts:
- wupm-cli.zip
- wupm-api.zip
- sbom.json

Changes:
$changes
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
  $deployTarget = $env:WUPM_DEPLOY_TARGET
  $deployConfigPath = $env:WUPM_DEPLOY_CONFIG
  if (-not [string]::IsNullOrWhiteSpace($DeployConfig) -and (Test-Path $DeployConfig)) {
    $deployConfigPath = $DeployConfig
  }
  if (-not [string]::IsNullOrWhiteSpace($deployConfigPath) -and (Test-Path $deployConfigPath)) {
    try {
      $deployConfig = Get-Content -Raw -Path $deployConfigPath | ConvertFrom-Json
      if ($deployConfig.target) { $deployTarget = $deployConfig.target }
    } catch {
      Write-Warning "Failed to parse deployment config: $_"
    }
  }
  if ([string]::IsNullOrWhiteSpace($deployTarget)) {
    Write-Host 'No deployment target configured. Set WUPM_DEPLOY_TARGET or pass -DeployConfig.'
  }
  else {
    switch ($deployTarget.ToLowerInvariant()) {
      'winget' {
        Write-Host "Deploying to Winget manifest for tag $Tag ..."
        # TODO: implement winget manifest submission
      }
      'chocolatey' {
        Deploy-Chocolatey -Tag $Tag -Repo $Repo
      }
      'feed' {
        Write-Host "Deploying to internal feed for tag $Tag ..."
        # TODO: implement internal artifact feed upload
      }
      default {
        Write-Host "Unknown deployment target: $deployTarget"
      }
    }
  }
}
finally {
  Pop-Location
}
