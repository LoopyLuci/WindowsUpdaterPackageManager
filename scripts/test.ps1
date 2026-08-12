<#
.SYNOPSIS
Run tests only, with optional coverage output.
#>
param(
  [ValidateSet('Debug', 'Release')]
  [string]$Configuration = 'Release',
  [string]$OutputDir = 'test-results',
  [switch]$NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Push-Location 'D:\Projects\WindowsUpdatePackageManager'
try {
  if (-not $NoBuild) {
    Write-Host '--- Build ---'
    dotnet build -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
  }

  Write-Host '--- Test ---'
  if (-not (Test-Path $OutputDir)) { New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null }
  dotnet test --no-build -c $Configuration --logger "trx;LogFileName=$OutputDir/results.trx" --logger "html;LogFileName=$OutputDir/results.html"
  if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
  Write-Host "Test results written to $OutputDir"
}
finally {
  Pop-Location
}
