<#
.SYNOPSIS
Register a Windows Scheduled Task to run local CI on a schedule.
#>
param(
  [string]$Tag = 'WUPM CI',
  [ValidateSet('Daily','Weekly')]
  [string]$Frequency = 'Daily',
  [string]$At = '09:00',
  [string]$LogPath = 'C:\Logs\wupm-ci.log',
  [switch]$Uninstall
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$taskName = $Tag

if ($Uninstall) {
  Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
  Write-Host "Uninstalled scheduled task '$taskName'."
  return
}

$action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument @(
  '-NoProfile',
  '-ExecutionPolicy','Bypass',
  '-File','D:\Projects\WindowsUpdatePackageManager\scripts\ci.ps1',
  '-AsHook'
) -WorkingDirectory 'D:\Projects\WindowsUpdatePackageManager'

if ($Frequency -eq 'Daily') {
  $trigger = New-ScheduledTaskTrigger -Daily -At $At
}
else {
  $trigger = New-ScheduledTaskTrigger -Weekly -DaysOfWeek Sunday -At $At
}

$settings = New-ScheduledTaskSettingsSet -AllowHardTerminate -DontStopOnIdleEnd -ExecutionTimeLimit ([TimeSpan]::Zero)

Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Settings $settings -Description 'Local WUPM CI runner' -RunLevel Highest | Out-Null
Write-Host "Registered scheduled task '$taskName' ($Frequency at $At). Logs: $LogPath"
