<#
.SYNOPSIS
Chocolatey install script for WUPM CLI.
#>
$ErrorActionPreference = 'Stop'
$packageName = 'wupm-cli'
$toolsDir = Split-Path -Parent $MyInvocation.MySmart.Definition
$zipPath = Join-Path $toolsDir 'wupm-cli.zip'

if (-not (Test-Path $zipPath)) {
  throw "wupm-cli.zip not found at $zipPath"
}

$installDir = Join-Path $env:ProgramFiles $packageName
if (-not (Test-Path $installDir)) {
  New-Item -ItemType Directory -Path $installDir -Force | Out-Null
}

Expand-Archive -Path $zipPath -DestinationPath $installDir -Force
$exePath = Join-Path $installDir 'Wupm.Cli.exe'
if (-not (Test-Path $exePath)) {
  throw "Wupm.Cli.exe not found in $installDir"
}

# Add to PATH for current user if not present
$userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
if ($userPath -notlike "*$installDir*") {
  [Environment]::SetEnvironmentVariable('Path', "$userPath;$installDir", 'User')
  $env:Path = "$env:Path;$installDir"
}

Write-Host "Installed $packageName to $installDir"
