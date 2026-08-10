Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
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
    Write-Step 'Verifying dotnet CLI'
    $dotnet = dotnet --version
    Write-Ok "dotnet $dotnet"

    Write-Step 'Restoring NuGet packages'
    dotnet restore
    Write-Ok 'Restore attempted'

    Write-Step 'Building solution'
    $buildOutput = dotnet build --configuration $Configuration --no-restore 2>&1
    $buildOutput | ForEach-Object { Write-Host $_ }
    if ($LASTEXITCODE -ne 0) {
        Write-Fail "Build failed with exit code $LASTEXITCODE"
        exit $LASTEXITCODE
    }
    Write-Ok 'Build succeeded'

    Write-Step 'Running xUnit tests'
    $testOutput = dotnet test --configuration $Configuration --no-build --verbosity normal 2>&1
    $testOutput | ForEach-Object { Write-Host $_ }
    if ($LASTEXITCODE -ne 0) {
        Write-Fail "Tests failed with exit code $LASTEXITCODE"
        exit $LASTEXITCODE
    }
    Write-Ok 'Tests passed'

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
