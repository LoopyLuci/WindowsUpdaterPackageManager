$ErrorActionPreference = 'Stop'
$PSModuleAutoLoadingPreference = 'None'
Import-Module -Name 'WindowsUpdateAndPackageManager' -Force -ErrorAction Stop

$script:WupmRoot = Join-Path -Path $PSScriptRoot -ChildPath '..' | Resolve-Path | Select-Object -ExpandProperty Path
$script:WupmServices = [WindowsUpdateAndPackageManager.Commands.PowerShellModule]::Build($script:WupmRoot)
$script:DefaultRepo = 'https://github.com/LoopyLuci/WindowsUpdateAndPackageManager'

function Sync-WUPMRepository {
    [CmdletBinding()]
    param([string]$RepositoryUrl = $script:DefaultRepo)
    $result = [WindowsUpdateAndPackageManager.Commands.PowerShellModule]::SyncRepositoryAsync($script:WupmServices, $RepositoryUrl).GetAwaiter().GetResult()
    [PSCustomObject]@{ Success = $result.Success; PackagesUpdated = $result.PackagesUpdated; Message = $result.Message }
}

function Install-WUPMPackage {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Id, [string]$RepositoryUrl = $script:DefaultRepo)
    $index = [WindowsUpdateAndPackageManager.Commands.PowerShellModule]::ListAvailableAsync($script:WupmServices, $RepositoryUrl).GetAwaiter().GetResult()
    $package = $index | Where-Object { $_.Id -eq $Id } | Select-Object -First 1
    if (-not $package) { throw "Package '$Id' was not found in the repository." }
    [WindowsUpdateAndPackageManager.Commands.PowerShellModule]::InstallPackageAsync($script:WupmServices, $package).GetAwaiter().GetResult()
}

function Uninstall-WUPMPackage {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Id)
    [WindowsUpdateAndPackageManager.Commands.PowerShellModule]::UninstallPackageAsync($script:WupmServices, $Id).GetAwaiter().GetResult()
}

function Get-WUPMInstalled {
    [CmdletBinding()]
    [WindowsUpdateAndPackageManager.Commands.PowerShellModule]::ListInstalledAsync($script:WupmServices).GetAwaiter().GetResult()
}

function Invoke-WUPMWindowsUpdate {
    [CmdletBinding()]
    param([switch]$DriversOnly)
    [WindowsUpdateAndPackageManager.Commands.PowerShellModule]::InvokeWindowsUpdateAsync($script:WupmServices, $DriversOnly.IsPresent).GetAwaiter().GetResult()
}

function Invoke-WUPMDriverUpdate {
    [CmdletBinding()]
    [WindowsUpdateAndPackageManager.Commands.PowerShellModule]::InvokeWindowsUpdateAsync($script:WupmServices, $true).GetAwaiter().GetResult()
}

function Invoke-WUPMRollback {
    [CmdletBinding()]
    [WindowsUpdateAndPackageManager.Commands.PowerShellModule]::RollbackAsync($script:WupmServices).GetAwaiter().GetResult()
}

function Get-WUPMAudit {
    [CmdletBinding()]
    param([DateTimeOffset]$From, [DateTimeOffset]$To, [string]$Action)
    [WindowsUpdateAndPackageManager.Commands.PowerShellModule]::QueryAuditAsync($script:WupmServices, $From, $To, $Action).GetAwaiter().GetResult()
}

function Search-WUPMPackage {
    [CmdletBinding()]
    param([string]$Query, [string]$RepositoryUrl = $script:DefaultRepo)
    $packages = [WindowsUpdateAndPackageManager.Commands.PowerShellModule]::ListAvailableAsync($script:WupmServices, $RepositoryUrl).GetAwaiter().GetResult()
    if ([string]::IsNullOrWhiteSpace($Query)) { return $packages }
    $q = $Query.Trim()
    $packages | Where-Object { $_.Id -like "*$q*" -or ($_.DisplayName -like "*$q*") }
}

function Get-WUPMAvailable {
    [CmdletBinding()]
    param([string]$RepositoryUrl = $script:DefaultRepo)
    [WindowsUpdateAndPackageManager.Commands.PowerShellModule]::ListAvailableAsync($script:WupmServices, $RepositoryUrl).GetAwaiter().GetResult()
}

function Set-WUPMPolicyAllow {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Id)
    [WindowsUpdateAndPackageManager.Commands.PowerShellModule]::SetPolicyAllow($script:WupmServices, $Id)
}

function Set-WUPMPolicyDeny {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Id)
    [WindowsUpdateAndPackageManager.Commands.PowerShellModule]::SetPolicyDeny($script:WupmServices, $Id)
}

function New-WUPMPackage {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$SourceDir, [Parameter(Mandatory)][string]$OutputDir)
    $source = Resolve-Path -Path $SourceDir | Select-Object -ExpandProperty Path
    $output = Resolve-Path -Path $OutputDir | Select-Object -ExpandProperty Path
    [WindowsUpdateAndPackageManager.Commands.Cli]::PackPackage($script:WupmServices, $source, $output).GetAwaiter().GetResult()
}

function Get-WUPMLatestRelease {
    [CmdletBinding()]
    param([string]$RepositoryUrl = $script:DefaultRepo)
    [WindowsUpdateAndPackageManager.Commands.PowerShellModule]::GetLatestReleaseAsync($script:WupmServices, $RepositoryUrl).GetAwaiter().GetResult()
}

function Get-WUPMHealth {
    [CmdletBinding()]
    param([string]$RepositoryUrl = $script:DefaultRepo)
    $client = $script:WupmServices.GetService([WindowsUpdateAndPackageManager.Infrastructure.IRepoClient])
    $validator = $script:WupmServices.GetService([WindowsUpdateAndPackageManager.Infrastructure.IManifestValidator])
    if (-not $client -or -not $validator) { throw "Health check is not fully configured." }
    $indexJson = $client.DownloadIndexAsync($RepositoryUrl).GetAwaiter().GetResult()
    $valid = $validator.ValidateAsync($indexJson).GetAwaiter().GetResult()
    [PSCustomObject]@{ RepositoryUrl = $RepositoryUrl; Reachable = $true; ManifestValid = $valid }
}

Export-ModuleMember -Function Sync-WUPMRepository, Install-WUPMPackage, Uninstall-WUPMPackage, Get-WUPMInstalled, Invoke-WUPMWindowsUpdate, Invoke-WUPMDriverUpdate, Invoke-WUPMRollback, Get-WUPMAudit, Search-WUPMPackage, Get-WUPMAvailable, Set-WUPMPolicyAllow, Set-WUPMPolicyDeny, New-WUPMPackage, Get-WUPMLatestRelease, Get-WUPMHealth
