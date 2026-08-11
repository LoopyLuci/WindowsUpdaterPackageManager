function Invoke-WUPMDeltaUpdate {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Id, [Parameter(Mandatory)][string]$FromVersion, [string]$RepositoryUrl = $script:DefaultRepo)
    [WindowsUpdateAndPackageManager.Commands.PowerShellModule]::InvokeDeltaUpdateAsync($script:WupmServices, $Id, $FromVersion, $RepositoryUrl).GetAwaiter().GetResult()
}

function Mount-WUPOfflineImage {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$ImagePath)
    [WindowsUpdateAndPackageManager.Commands.PowerShellModule]::MountOfflineImageAsync($script:WupmServices, $ImagePath).GetAwaiter().GetResult()
}

function Dismount-WUPOfflineImage {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$MountPath, [switch]$Discard)
    [WindowsUpdateAndPackageManager.Commands.PowerShellModule]::DismountOfflineImageAsync($script:WupmServices, $MountPath, $Discard.IsPresent).GetAwaiter().GetResult()
}

function Apply-WUPMPackageToImage {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$MountPath, [Parameter(Mandatory)][string]$PackagePath)
    [WindowsUpdateAndPackageManager.Commands.PowerShellModule]::ApplyPackageToImageAsync($script:WupmServices, $MountPath, $PackagePath).GetAwaiter().GetResult()
}

Export-ModuleMember -Function Sync-WUPMRepository, Install-WUPMPackage, Uninstall-WUPMPackage, Get-WUPMInstalled, Invoke-WUPMWindowsUpdate, Invoke-WUPMDriverUpdate, Invoke-WUPMRollback, Get-WUPMAudit, Search-WUPMPackage, Get-WUPMAvailable, Set-WUPMPolicyAllow, Set-WUPMPolicyDeny, New-WUPMPackage, Get-WUPMLatestRelease, Get-WUPMHealth, Invoke-WUPMDeltaUpdate, Mount-WUPOfflineImage, Dismount-WUPOfflineImage, Apply-WUPMPackageToImage
