<#
.SYNOPSIS
Pre-commit hook: enforce code formatting before commit.
#>
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
  Write-Host '[wupm pre-commit] Checking code formatting...'
  $output = dotnet format --verify-no-changes 2>&1
  if ($LASTEXITCODE -ne 0) {
    Write-Host "[wupm pre-commit] Formatting check failed:`n$output"
    Write-Host '[wupm pre-commit] Run `dotnet format` to fix, then commit again.'
    exit 1
  }
  Write-Host '[wupm pre-commit] Formatting OK.'
}
finally {
  Pop-Location
}
