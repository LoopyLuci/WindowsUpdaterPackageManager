<#
.SYNOPSIS
Windows Service wrapper for WupmApi.
#>
param(
  [ValidateSet('install','start','stop','uninstall','status')]
  [string]$Action = 'status',
  [string]$ServiceName = 'WupmApi',
  [string]$DisplayName = 'WUPM API Host',
  [string]$BinaryPath = 'D:\Projects\WindowsUpdatePackageManager\publish\api\WupmApi.exe'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path $BinaryPath)) {
  throw "WupmApi executable not found at $BinaryPath. Run .\scripts\ci.ps1 to publish first."
}

switch ($Action) {
  'install' {
    New-Service -Name $ServiceName -DisplayName $DisplayName -BinaryPathName $BinaryPath -Description 'WUPM REST API host' -StartupType Automatic | Out-Null
    Write-Host "Installed service '$ServiceName'."
  }
  'start' {
    Start-Service -Name $ServiceName
    Write-Host "Started service '$ServiceName'."
  }
  'stop' {
    Stop-Service -Name $ServiceName -Force
    Write-Host "Stopped service '$ServiceName'."
  }
  'uninstall' {
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    sc.exe delete $ServiceName | Out-Null
    Write-Host "Uninstalled service '$ServiceName'."
  }
  'status' {
    $svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($svc) {
      Write-Host "$($svc.Name): $($svc.Status)"
    }
    else {
      Write-Host "Service '$ServiceName' not found."
    }
  }
}
