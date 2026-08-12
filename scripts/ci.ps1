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
  [string]$SigningClientId,
  [string]$SigningTenantId,
  [string]$SigningSecret,
  [string]$KeyVaultUrl
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Push-Location 'D:\Projects\WindowsUpdatePackageManager'
try {
  Write-Host '--- Restore ---'
  dotnet restore

  Write-Host '--- Build ---'
  dotnet build WindowsUpdateAndPackageManager.sln --no-restore --configuration $Configuration

  if (-not $SkipTests) {
    Write-Host '--- Test ---'
    dotnet test tests/WindowsUpdateAndPackageManager.Tests/WindowsUpdateAndPackageManager.Tests.csproj --no-build --configuration $Configuration --verbosity normal
  }

  if (-not $SkipPublish) {
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
    Compress-Archive -Path (Join-Path $cliOut '*') -DestinationPath $cliZip
    Compress-Archive -Path (Join-Path $apiOut '*') -DestinationPath $apiZip
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
