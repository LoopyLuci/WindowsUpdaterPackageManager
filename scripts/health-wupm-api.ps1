<#
.SYNOPSIS
Wait for WupmApi to become healthy.
#>
param(
  [string]$ServiceName = 'WupmApi',
  [int]$TimeoutSeconds = 60,
  [string]$HealthUrl = 'http://localhost:5000/'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$sw = [System.Diagnostics.Stopwatch]::StartNew()
while ($sw.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
  $svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
  if ($svc -and $svc.Status -eq 'Running') {
    try {
      $r = Invoke-WebRequest -Uri $HealthUrl -UseBasicParsing -TimeoutSec 5
      if ($r.StatusCode -eq 200) { Write-Host "WupmApi healthy at $HealthUrl"; exit 0 }
    } catch {
      Start-Sleep -Seconds 2
      continue
    }
  }
  Start-Sleep -Seconds 2
}
Write-Error "WupmApi did not become healthy within ${TimeoutSeconds}s."
exit 1
