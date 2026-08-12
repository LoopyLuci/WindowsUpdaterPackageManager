<#
.SYNOPSIS
Install the WUPM git hooks into .git/hooks.
#>
param(
  [switch]$Uninstall
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$hookDir = '.git/hooks'
if (-not (Test-Path $hookDir)) { throw "Git hooks directory not found at $hookDir." }
$outputDir = Join-Path $hookDir 'output'
if (-not (Test-Path $outputDir)) { New-Item -ItemType Directory -Path $outputDir | Out-Null }

if ($Uninstall) {
  Remove-Item (Join-Path $hookDir 'post-commit') -Force -ErrorAction SilentlyContinue
  Remove-Item (Join-Path $hookDir 'pre-commit') -Force -ErrorAction SilentlyContinue
  Write-Host "Uninstalled WUPM git hooks."
  exit 0
}

$wrapper = @()
$wrapper += '#!/bin/sh'
$wrapper += 'exec pwsh -NoProfile -ExecutionPolicy Bypass -File "$(git rev-parse --show-toplevel)/scripts/hooks/post-commit.ps1" "$@"'
Set-Content -Path (Join-Path $hookDir 'post-commit') -Value ($wrapper -join "`n") -Encoding ASCII

$wrapper = @()
$wrapper += '#!/bin/sh'
$wrapper += 'exec pwsh -NoProfile -ExecutionPolicy Bypass -File "$(git rev-parse --show-toplevel)/scripts/hooks/pre-commit.ps1" "$@"'
Set-Content -Path (Join-Path $hookDir 'pre-commit') -Value ($wrapper -join "`n") -Encoding ASCII

Write-Host "Installed WUPM git hooks: post-commit, pre-commit"
