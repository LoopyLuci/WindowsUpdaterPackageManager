param(
    [string]$Artifacts = "publish",
    [string]$WingetManifest = "docs/winget/LoopyLuci.WindowsUpdateAndPackageManager.yaml"
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path | Split-Path -Parent
Push-Location $root

Write-Host "=== Release dry-run ==="

# Accept legacy publish layout for dry-run compatibility
$legacyApi = Join-Path $Artifacts "wupm-api"
$canonicalApi = Join-Path $Artifacts "api"
$apiDir = if (Test-Path $legacyApi) { $legacyApi } elseif (Test-Path $canonicalApi) { $canonicalApi } else { $null }
$mcpDir = Join-Path $Artifacts "wupm-mcp"
$guiDir = Join-Path $Artifacts "wupm-gui"

if (-not $apiDir -or -not (Test-Path (Join-Path $apiDir "WupmApi.exe"))) { throw "Missing $Artifacts/wupm-api/WupmApi.exe or $Artifacts/api/WupmApi.exe" }
if (-not (Test-Path (Join-Path $mcpDir "WupmMcp.exe"))) { throw "Missing $Artifacts/wupm-mcp/WupmMcp.exe" }
if (-not (Test-Path (Join-Path $guiDir "WupmGui.exe"))) { throw "Missing $Artifacts/wupm-gui/WupmGui.exe" }

$winget = Get-Command winget -ErrorAction SilentlyContinue
if ($winget) {
    $wingetVersion = (winget --version 2>&1 | Select-String -Pattern "\d+\.\d+\.\d+" | ForEach-Object { $_.Matches[0].Value } | Select-Object -First 1).ToString()
    Write-Host "winget version: $wingetVersion"

    if (Test-Path $WingetManifest) {
        Write-Host "Validating winget manifest: $WingetManifest"
        winget validate --manifest $WingetManifest | ForEach-Object { Write-Host $_ }

        if ([version]$wingetVersion -lt [version]"1.6.0") {
            Write-Host "NOTE: winget < 1.6.0 only supports single-file manifests. Multi-file manifests require winget >= 1.6.0."
        }
    } else {
        Write-Host "Winget manifest not found at $WingetManifest"
        Write-Host "Create a single-file manifest at $WingetManifest for winget validation."
    }

    Write-Host ""
    Write-Host "winget manifest format notes:"
    Write-Host "- WUPM release artifacts are zip archives containing multiple executables."
    Write-Host "- winget v1.29.280 requires NestedInstallerType/NestedInstallerFiles for zip manifests."
    Write-Host "- If validation fails with schema errors, use winget >= 1.6.0 with multi-file manifest support,"
    Write-Host "  or convert release artifacts to single-installer packages (exe/msi/portable)."
} else {
    Write-Host "winget not available; skip manifest validation"
}

Write-Host ""
Write-Host "Artifact checks:"
Write-Host "- API:  $apiDir/WupmApi.exe"
Write-Host "- MCP:  $mcpDir/WupmMcp.exe"
Write-Host "- GUI:  $guiDir/WupmGui.exe"

Write-Host ""
Write-Host "Local marketplace seeding:"
$localMarketplaceRoot = Join-Path $Artifacts ".wupm\marketplace"
if (Test-Path $localMarketplaceRoot) {
    $manifests = Get-ChildItem $localMarketplaceRoot -Filter "*.json" | Measure-Object | Select-Object -ExpandProperty Count
    Write-Host "Found $manifests local marketplace manifest(s) in $localMarketplaceRoot"
} else {
    Write-Host "No local marketplace directory found at $localMarketplaceRoot"
    Write-Host "To seed local marketplace:"
    Write-Host "  1. mkdir $localMarketplaceRoot"
    Write-Host "  2. Copy plugin JSON manifests into $localMarketplaceRoot"
    Write-Host "  3. Use 'wupm marketplace search <term>' to verify fallback"
}

Pop-Location
