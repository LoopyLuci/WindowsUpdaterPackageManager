<#
.SYNOPSIS
Install the WUPM post-commit hook into .git/hooks.
#>
param(
  [switch]$Uninstall
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$hookDir = '.git/hooks'
if (-not (Test-Path $hookDir)) { throw "Git hooks directory not found at $hookDir." }
$source = 'scripts/hooks/post-commit.ps1'
if (-not (Test-Path $source)) { throw "Hook source not found at $source." }
$target = Join-Path $hookDir 'post-commit'

if ($Uninstall) {
  if (Test-Path $target) {
    Remove-Item $target -Force
    Write-Host "Uninstalled $target"
  }
  exit 0
}

$content = Get-Content -Raw -Path $source
$wrapper = @()
$wrapper += '#!/bin/sh'
$wrapper += 'exec pwsh -NoProfile -ExecutionPolicy Bypass -File "$(git rev-parse --show-toplevel)/scripts/hooks/post-commit.ps1" "$@"'
Set-Content -Path $target -Value ($wrapper -join "`n") -Encoding ASCII
Write-Host "Installed $target"
