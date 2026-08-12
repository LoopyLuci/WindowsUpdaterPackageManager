<#
.SYNOPSIS
Fast pre-commit CI: format, restore, build, test only.
#>
param(
  [string]$Configuration = 'Release'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Push-Location 'D:\Projects\WindowsUpdatePackageManager'
try {
  Write-Host '--- dotnet format ---'
  dotnet format --verify-no-changes
  Write-Host '--- dotnet restore ---'
  dotnet restore
  Write-Host '--- dotnet build ---'
  dotnet build -c $Configuration --no-restore
  Write-Host '--- dotnet test ---'
  dotnet test -c $Configuration --no-build --verbosity quiet
}
finally {
  Pop-Location
}
