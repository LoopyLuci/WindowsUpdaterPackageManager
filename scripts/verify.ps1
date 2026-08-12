Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$SkipTests
)

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $repoRoot

function Write-Step([string]$text) {
    Write-Host "`n[STEP] $text" -ForegroundColor Cyan
}
function Write-Ok([string]$text) {
    Write-Host "[OK] $text" -ForegroundColor Green
}
function Write-Fail([string]$text) {
    Write-Host "[FAIL] $text" -ForegroundColor Red
}

try {
    Write-Step 'Running local CI'
    powershell -NoProfile -ExecutionPolicy Bypass -File '.\scripts\ci.ps1' -Configuration $Configuration -SkipTests:$SkipTests
    if ($LASTEXITCODE -ne 0) {
        Write-Fail "CI failed with exit code $LASTEXITCODE"
        exit $LASTEXITCODE
    }
    Write-Ok 'CI passed'

    Write-Step 'Validating repo manifest'
    $manifestPath = Join-Path $repoRoot 'repo/index.json'
    if (-not (Test-Path $manifestPath)) {
        Write-Fail "Missing repo manifest at $manifestPath"
        exit 1
    }
    $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
    if (-not $manifest.schemaVersion -or -not $manifest.packages) {
        Write-Fail 'Invalid manifest: schemaVersion or packages missing'
        exit 1
    }
    Write-Ok "Manifest OK: schemaVersion=$($manifest.schemaVersion) packages=$($manifest.packages.Count)"

    Write-Host "`n[RESULT] Verification succeeded." -ForegroundColor Green
    exit 0
}
catch {
    Write-Fail $_
    exit 1
}
finally {
    Pop-Location
}
