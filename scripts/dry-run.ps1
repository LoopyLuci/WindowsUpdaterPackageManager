param(
    [string]$Artifacts = "publish",
    [string]$WingetManifest = "docs/winget/LoopyLuci.WindowsUpdateAndPackageManager.yaml"
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path | Split-Path -Parent
Push-Location $root

Write-Host "=== Release dry-run ==="
if (-not (Test-Path $Artifacts/wupm-api/WupmApi.exe)) { throw "Missing $Artifacts/wupm-api/WupmApi.exe" }
if (-not (Test-Path $Artifacts/wupm-mcp/WupmMcp.exe)) { throw "Missing $Artifacts/wupm-mcp/WupmMcp.exe" }
if (-not (Test-Path $Artifacts/wupm-gui/WupmGui.exe)) { throw "Missing $Artifacts/wupm-gui/WupmGui.exe" }

if (Test-Path $WingetManifest) {
  $winget = Get-Command winget -ErrorAction SilentlyContinue
  if ($winget) {
    Write-Host "Validating winget manifest: $WingetManifest"
    winget validate --manifest $WingetManifest | ForEach-Object { Write-Host $_ }
  } else {
    Write-Host "winget not available; skip manifest validation"
  }
} else {
  Write-Host "Winget manifest not found at $WingetManifest"
}

Write-Host "Artifacts layout:"
Get-ChildItem $Artifacts -Recurse | Select-Object FullName, Length | Format-Table -AutoSize

Pop-Location
