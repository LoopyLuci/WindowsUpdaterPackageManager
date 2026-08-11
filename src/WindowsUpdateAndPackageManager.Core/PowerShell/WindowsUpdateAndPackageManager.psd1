@{
    RootModule = 'WindowsUpdateAndPackageManager.psm1'
    ModuleVersion = '0.1.0-alpha'
    GUID = '2d2c7b4a-1e6a-4b3f-9c2d-5e8f1a7b3c4d'
    Author = 'LoopyLuci'
    CompanyName = 'LoopyLuci'
    Copyright = 'MIT'
    Description = 'Personal-first, offline-capable update and package manager for Windows 10 and older.'
    PowerShellVersion = '5.1'
    FunctionsToExport = @(
        'Sync-WUPMRepository',
        'Install-WUPMPackage',
        'Uninstall-WUPMPackage',
        'Get-WUPMInstalled',
        'Invoke-WUPMWindowsUpdate',
        'Invoke-WUPMDriverUpdate',
        'Invoke-WUPMRollback',
        'Get-WUPMAudit',
        'Search-WUPMPackage',
        'Get-WUPMAvailable',
        'Set-WUPMPolicyAllow',
        'Set-WUPMPolicyDeny',
        'New-WUPMPackage',
        'Get-WUPMLatestRelease',
        'Get-WUPMHealth'
    )
    CmdletsToExport = @()
    VariablesToExport = @()
    AliasesToExport = @()
}
