<#
.SYNOPSIS
Post-commit hook: run local CI after commit to catch breakages early.
#>
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$outputDir = Join-Path $repoRoot '.git' 'hooks' 'output'
if (-not (Test-Path $outputDir)) { New-Item -ItemType Directory -Path $outputDir | Out-Null }
$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$outputFile = Join-Path $outputDir "post-commit-$timestamp.log"

Push-Location $repoRoot
try {
  $script = Join-Path $repoRoot 'scripts' 'ci.ps1'
  Write-Host "[wupm post-commit] Running local CI..."
  $result = powershell -NoProfile -ExecutionPolicy Bypass -File $script -SkipSign -AsHook 2>&1 | Out-String
  $exitCode = $LASTEXITCODE
  $log = @"
[wupm post-commit] $(Get-Date -Format o)
CI exit code: $exitCode
$result
"@
  Set-Content -Path $outputFile -Value $log -Encoding UTF8
  if ($exitCode -ne 0) {
    Write-Host "[wupm post-commit] CI failed: $exitCode"
    Write-Host $result
    exit $exitCode
  }
}
finally {
  Pop-Location
}
