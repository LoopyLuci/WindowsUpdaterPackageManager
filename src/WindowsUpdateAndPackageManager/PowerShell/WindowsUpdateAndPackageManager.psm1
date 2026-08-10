Import-Module -Name 'WindowsUpdateAndPackageManager' -Force

function Sync-WUPMRepository {
    [CmdletBinding()]
    param([string]$RepositoryUrl = 'https://github.com/LoopyLuci/WindowsUpdateAndPackageManager')
    Write-Host "Sync requested: $RepositoryUrl"
}

function Install-WUPMPackage {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Id)
    Write-Host "Install requested: $Id"
}

function Uninstall-WUPMPackage {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Id)
    Write-Host "Uninstall requested: $Id"
}

function Get-WUPMInstalled {
    [CmdletBinding()]
    Write-Host "Listing installed packages."
}

function Invoke-WUPMWindowsUpdate {
    [CmdletBinding()]
    Write-Host "Windows Update scan requested."
}

function Invoke-WUPMRollback {
    [CmdletBinding()]
    Write-Host "Rollback requested."
}

function Get-WUPMAudit {
    [CmdletBinding()]
    Write-Host "Audit query requested."
}

Export-ModuleMember -Function Sync-WUPMRepository, Install-WUPMPackage, Uninstall-WUPMPackage, Get-WUPMInstalled, Invoke-WUPMWindowsUpdate, Invoke-WUPMRollback, Get-WUPMAudit
