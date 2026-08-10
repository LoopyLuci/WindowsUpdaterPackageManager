Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $repoRoot

try {
    Write-Host "Building $Configuration configuration..."
    dotnet build --configuration $Configuration

    Write-Host "Publishing self-contained win-x64..."
    $outputDir = Join-Path $repoRoot 'dist'
    Remove-Item -LiteralPath $outputDir -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Path $outputDir | Out-Null

    dotnet publish src\WindowsUpdateAndPackageManager\WindowsUpdateAndPackageManager.csproj `
        -c $Configuration `
        -o $outputDir `
        --self-contained true `
        -r win-x64 `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true

    Write-Host "Build complete. Output in $outputDir"
    Get-ChildItem $outputDir | Select-Object Name, Length
}
finally {
    Pop-Location
}
