<#
.SYNOPSIS
Local CI script: restore, build, test, publish, zip, SBOM, optional signing.
#>
param(
  [string]$Configuration = 'Release',
  [string]$OutputDir = 'publish',
  [switch]$SkipTests,
  [switch]$SkipPublish,
  [switch]$SkipSign,
  [switch]$AsHook,
  [string]$SigningClientId,
  [string]$SigningTenantId,
  [string]$SigningSecret,
  [string]$KeyVaultUrl
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Fail-Fast([string]$message) {
  Write-Host "CI ABORT: $message"
  exit 1
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { Fail-Fast "dotnet SDK not found." }
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { Fail-Fast "gh CLI not found." }

Push-Location 'D:\Projects\WindowsUpdatePackageManager'
try {
  Write-Host '--- Restore ---'
  dotnet restore

  Write-Host '--- Build ---'
  dotnet build WindowsUpdateAndPackageManager.sln --no-restore --configuration $Configuration
  if ($LASTEXITCODE -ne 0) { Fail-Fast "Build failed." }

  if (-not $SkipTests) {
    Write-Host '--- Test ---'
    dotnet test tests/WindowsUpdateAndPackageManager.Tests/WindowsUpdateAndPackageManager.Tests.csproj --no-build --configuration $Configuration --verbosity normal
    if ($LASTEXITCODE -ne 0) { Fail-Fast "Tests failed." }
  }

  if (-not $SkipPublish -and -not $AsHook) {
    Write-Host '--- Publish ---'
    $cliOut = Join-Path $OutputDir 'cli'
    $apiOut = Join-Path $OutputDir 'api'
    if (Test-Path $cliOut) { Remove-Item -Force -Recurse $cliOut }
    if (Test-Path $apiOut) { Remove-Item -Force -Recurse $apiOut }
    New-Item -ItemType Directory -Path $cliOut,$apiOut -Force | Out-Null

    dotnet publish src/Wupm.Cli/Wupm.Cli.csproj --no-build -c $Configuration -o $cliOut
    dotnet publish src/WupmApi/WupmApi.csproj --no-build -c $Configuration -o $apiOut

    Write-Host '--- Zip ---'
    $cliZip = Join-Path (Get-Location) 'wupm-cli.zip'
    $apiZip = Join-Path (Get-Location) 'wupm-api.zip'
    if (Test-Path $cliZip) { Remove-Item $cliZip -Force }
    if (Test-Path $apiZip) { Remove-Item $apiZip -Force }
    $cliFiles = Get-ChildItem -Path $cliOut -File -Recurse | Select-Object -ExpandProperty FullName
    $apiFiles = Get-ChildItem -Path $apiOut -File -Recurse | Select-Object -ExpandProperty FullName
    Compress-Archive -Path $cliFiles -DestinationPath $cliZip
    if ($LASTEXITCODE -ne 0) { Fail-Fast 'Failed to create CLI zip.' }
    Compress-Archive -Path $apiFiles -DestinationPath $apiZip
    if ($LASTEXITCODE -ne 0) { Fail-Fast 'Failed to create API zip.' }
    Write-Host "Created $cliZip and $apiZip"

    Write-Host '--- SBOM ---'
    $sbomPath = Join-Path (Get-Location) 'sbom.json'
    $components = @()
    foreach ($proj in @(
        'src/WindowsUpdateAndPackageManager.Core/WindowsUpdateAndPackageManager.Core.csproj',
        'src/Wupm.Cli/Wupm.Cli.csproj',
        'src/WupmApi/WupmApi.csproj')) {
      if (Test-Path $proj) {
        $name = [System.IO.Path]::GetFileNameWithoutExtension($proj)
        $components += @{ type = 'library'; name = $name; version = '1.0.0'; description = $proj }
      }
    }
    $sbom = @{ bomFormat = 'CycloneDX'; specVersion = '1.5'; version = 1; components = $components }
    $sbom | ConvertTo-Json -Depth 5 | Out-File -FilePath $sbomPath -Encoding utf8
    Write-Host "Created $sbomPath"

    if (-not $SkipSign) {
      Write-Host '--- Sign ---'
      $files = @($cliZip, $apiZip)
      if (Get-Command azuresigntool -ErrorAction SilentlyContinue) {
        if ([string]::IsNullOrWhiteSpace($SigningClientId)) { throw 'SigningClientId is required for AzureSignTool.' }
        foreach ($f in $files) {
          azuresigntool sign `
            --azure-key-vault-url ("https://{0}/" -f $KeyVaultUrl) `
            --azure-key-vault-client-id $SigningClientId `
            --azure-key-vault-tenant-id $SigningTenantId `
            --azure-key-vault-secret $SigningSecret `
            --azure-key-vault-certificate 'WUPMSigningCert' `
            --timestamp-server 'http://timestamp.digicert.com' `
            --file $f
        }
        Write-Host 'Signed artifacts with AzureSignTool.'
      }
      else {
        $cert = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert | Select-Object -First 1
        if ($null -eq $cert) {
          Write-Warning 'No code-signing certificate found; skipping signing.'
        }
        else {
          foreach ($f in $files) {
            Set-AuthenticodeSignature -FilePath $f -Certificate $cert -TimestampServer 'http://timestamp.digicert.com'
          }
          Write-Host 'Signed artifacts with local certificate.'
        }
      }
    }
  }
}
finally {
  Pop-Location
}
