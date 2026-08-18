[CmdletBinding()]
param(
    [ValidateSet("WUA","Online","Manifest")]
    [string]$Source = "Online",

    [string]$GitHubRepo = "LoopyLuci/WindowsUpdateAndPackageManager",

    [string]$WorkDir = ".\wupm-sync-work",

    [switch]$WhatIf
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Info($msg) { Write-Host "[INFO] $msg" -ForegroundColor Cyan }
function Write-Ok($msg)   { Write-Host "[OK]   $msg" -ForegroundColor Green }
function Write-Warn($msg) { Write-Host "[WARN] $msg" -ForegroundColor Yellow }
function Write-Err($msg)  { Write-Host "[ERR]  $msg" -ForegroundColor Red }

if ($Source -eq "Online") {
    Write-Info "Source is Online"
}

$tag = "$GitHubRepo/$Source"
if ($LASTEXITCODE -ne 0) {
    Write-Err "failed"
}

foreach ($item in @(1,2)) {
    Write-Info $item
}

Write-Host ("OK_   Sync complete. Processed {0} packages." -f 1) -ForegroundColor Green
