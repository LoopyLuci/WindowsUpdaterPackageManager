<#
.SYNOPSIS
Post-commit hook: run local CI after commit to catch breakages early.
#>
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
  Write-Host '[wupm post-commit] Running local CI...'
  powershell -NoProfile -ExecutionPolicy Bypass -File '.\scripts\ci.ps1' -SkipSign
}
catch {
  Write-Host "[wupm post-commit] CI failed: $_"
  exit 1
}
finally {
  Pop-Location
}
