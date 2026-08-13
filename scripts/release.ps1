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
  [switch]$ManifestOnly,
  [string]$SigningClientId,
  [string]$SigningTenantId,
  [string]$SigningSecret,
  [string]$KeyVaultUrl,
  [string]$Repo = 'LoopyLuci/WindowsUpdateAndPackageManager',
  [string]$DeployConfig,
  [string]$DeployTarget
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $Tag -match '^v\d+\.\d+\.\d+') { throw "Tag must match semver, got '$Tag'." }
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { throw 'gh CLI is required for release publishing.' }

function Deploy-Chocolatey {
  param([string]$Tag, [string]$Repo)
  $version = $Tag.TrimStart('v')
  $zip = Join-Path $PWD.Path 'wupm-cli.zip'
  if (-not (Test-Path $zip)) { throw 'wupm-cli.zip not found for Chocolatey packaging.' }

  $packDir = Join-Path $PWD.Path 'choco-pack'
  $toolsDir = Join-Path $packDir 'tools'
  if (Test-Path $packDir) { Remove-Item $packDir -Recurse -Force }
  New-Item -ItemType Directory -Path $toolsDir | Out-Null
  Expand-Archive -Path $zip -DestinationPath $toolsDir -Force
  Copy-Item (Join-Path $PWD.Path 'scripts\deploy\chocolatey\tools\install.ps1') (Join-Path $toolsDir 'install.ps1') -Force

  $nuspec = Join-Path $packDir 'wupm-cli.nuspec'
  $nuspecTemplate = Join-Path $PWD.Path 'scripts\deploy\chocolatey\tools\wupm-cli.nuspec'
  if (-not (Test-Path $nuspecTemplate)) { throw 'Chocolatey nuspec template not found.' }
  (Get-Content $nuspecTemplate) -replace '__VERSION__', $version | Set-Content -Path $nuspec -Encoding UTF8

  if (Get-Command choco -ErrorAction SilentlyContinue) {
    Push-Location $packDir
    try {
      choco pack 'wupm-cli.nuspec' --out $packDir
      if ($LASTEXITCODE -ne 0) { throw 'choco pack failed.' }
      $nupkg = Get-ChildItem $packDir -Filter '*.nupkg' | Select-Object -First 1
      if ($nupkg) {
        Write-Host "Created Chocolatey package: $($nupkg.FullName)"
        if ($env:CHOCO_API_KEY) {
          choco push $nupkg.FullName --source 'https://push.chocolatey.org/' --api-key $env:CHOCO_API_KEY --force
          if ($LASTEXITCODE -ne 0) { Write-Warning 'choco push failed; check CHOCO_API_KEY.' }
        }
        else {
          Write-Host 'CHOCO_API_KEY not set; skipping choco push.'
        }
      }
    }
    finally {
      Pop-Location
    }
  }
  else {
    Write-Warning 'choco CLI not found; skipping Chocolatey packaging.'
  }

  if (Test-Path $packDir) { Remove-Item $packDir -Recurse -Force }
}

Push-Location 'D:\Projects\WindowsUpdatePackageManager'
try {
  if ($ManifestOnly) {
    Write-Host 'ManifestOnly enabled; skipping full CI and GitHub release.'
  }
  else {
    & '.\\scripts\\ci.ps1' -Configuration $Configuration -SkipTests:$SkipTests -SkipSign:$SkipSign `
      -SigningClientId $SigningClientId -SigningTenantId $SigningTenantId -SigningSecret $SigningSecret -KeyVaultUrl $KeyVaultUrl

    Write-Host '--- GitHub Release ---'
    $assets = @('wupm-cli.zip','wupm-api.zip','sbom.json')
    $parent = git rev-parse --verify -q $Tag^ 2>$null
    $range = if ($parent) { "$parent..$Tag" } else { $Tag }
    $changes = ''
    if (git rev-parse --verify -q refs/tags/$Tag 2>$null) {
      $changes = git log --date=short --pretty=format:'- %ad %s' $range 2>$null | Out-String
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
  }

  Write-Host '--- Optional deployment ---'
  $deployTarget = $DeployTarget
  if ([string]::IsNullOrWhiteSpace($deployTarget)) {
    $deployTarget = $env:WUPM_DEPLOY_TARGET
  }
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
    Write-Host 'No deployment target configured. Set WUPM_DEPLOY_TARGET or pass -DeployTarget.'
  }
  else {
    switch ($deployTarget.ToLowerInvariant()) {
      'winget' {
        Write-Host "Generating Winget manifest for tag $Tag ..."
        $version = $Tag.TrimStart('v')
        $wingetDir = Join-Path $PWD.Path 'scripts/deploy/winget/winget-pkgs/LoopyLuci.WindowsUpdatePackageManager'
        if (-not (Test-Path $wingetDir)) { New-Item -ItemType Directory -Path $wingetDir | Out-Null }
        $manifest = Join-Path $wingetDir "$version.yaml"
        $zip = Join-Path $PWD.Path 'wupm-cli.zip'
        if (-not (Test-Path $zip)) { throw 'wupm-cli.zip not found for Winget manifest generation.' }
        $sha = (Get-FileHash -Path $zip -Algorithm SHA256).Hash.ToLowerInvariant()
        $manifestContent = @"
PackageIdentifier: LoopyLuci.WindowsUpdatePackageManager
PackageVersion: $version
PackageName: Windows Update Package Manager
Publisher: LoopyLuci
License: MIT
ShortDescription: Windows Update Package Manager CLI
PackageUrl: https://github.com/LoopyLuci/WindowsUpdateAndPackageManager/releases/tag/$Tag
Installers:
  - Architecture: x64
    InstallerType: zip
    InstallerUrl: https://github.com/LoopyLuci/WindowsUpdateAndPackageManager/releases/download/$Tag/wupm-cli.zip
    InstallerSha256: $sha
    NestedInstallerType: exe
    NestedInstallerPath: Wupm.Cli.exe
    NestedInstallerFiles:
      - RelativeFilePath: Wupm.Cli.exe
    Commands:
      - wupm
ManifestType: installer
ManifestVersion: 1.6.0
"@
        Set-Content -Path $manifest -Value $manifestContent -Encoding UTF8
        Write-Host "Wrote Winget manifest to $manifest"
        Write-Host 'Run `winget validate <manifest>` to verify syntax, then commit under `winget-pkgs/`.'
      }
      'chocolatey' {
        Deploy-Chocolatey -Tag $Tag -Repo $Repo
      }
      'feed' {
        Write-Host "Deploying to internal feed for tag $Tag ..."
        $feedUrl = $env:WUPM_FEED_URL
        $feedApiKey = $env:WUPM_FEED_API_KEY
        if ([string]::IsNullOrWhiteSpace($feedUrl) -or [string]::IsNullOrWhiteSpace($feedApiKey)) {
          Write-Warning 'WUPM_FEED_URL and WUPM_FEED_API_KEY must be set for internal feed deployment.'
          return
        }
        $zip = Join-Path $PWD.Path 'wupm-cli.zip'
        if (-not (Test-Path $zip)) { throw 'wupm-cli.zip not found for feed deployment.' }
        $uri = $feedUrl
        if (-not $uri.EndsWith('/')) { $uri = "$uri/" }
        $uri = "$uri$($Tag.TrimStart('v'))/wupm-cli.zip"
        try {
          $response = Invoke-RestMethod -Uri $uri -Method Put -InFile $zip -ContentType 'application/octet-stream' -Headers @{ 'X-Api-Key' = $feedApiKey } -ErrorAction Stop
          Write-Host "Internal feed upload response: $($response | ConvertTo-Json -Compress)"
        }
        catch {
          Write-Warning "Internal feed upload failed: $_"
        }
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

exit 0
