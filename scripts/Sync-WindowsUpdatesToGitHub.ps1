<#
.SYNOPSIS
    Bulk-retrieve Windows update packages from Windows Update Agent or a known KB manifest list,
    wrap them as WUPM .wupkg packages, and upload to GitHub with proper tagging.

.EXAMPLE
    # Discover updates from local WUA and download them
    .\scripts\Sync-WindowsUpdatesToGitHub.ps1 -Source WUA -OSVersion "Windows 10" -Architecture all -MaxPackages 20

    # Use known KB manifest list
    .\scripts\Sync-WindowsUpdatesToGitHub.ps1 -Source Manifest -KbManifestPath .\scripts\known-kbs.json -MaxPackages 50

    # Dry run to see what would happen
    .\scripts\Sync-WindowsUpdatesToGitHub.ps1 -Source WUA -WhatIf
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("WUA","Online","Manifest")]
    [string]$Source = "WUA",

    [Parameter(Mandatory=$false)]
    [ValidateSet("Windows 10","Windows 11","Windows Server 2019","Windows Server 2022")]
    [string]$OSVersion = "Windows 10",

    [Parameter(Mandatory=$false)]
    [ValidateSet("x86","x64","arm64","all")]
    [string]$Architecture = "x64",

    [Parameter(Mandatory=$false)]
    [int]$MaxPackages = 50,

    [Parameter(Mandatory=$false)]
    [string]$GitHubRepo = "LoopyLuci/WindowsUpdateAndPackageManager",

    [Parameter(Mandatory=$false)]
    [string]$TagPrefix = "updates",

    [Parameter(Mandatory=$false)]
    [string]$WorkDir = ".\\wupm-sync-work",

    [Parameter(Mandatory=$false)]
    [string]$KbManifestPath = "",

    [Parameter(Mandatory=$false)]
    [switch]$WhatIf,

    [Parameter(Mandatory=$false)]
    [switch]$SkipDownload
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------
function Write-Info($msg) { Write-Host "[INFO] $msg" -ForegroundColor Cyan }
function Write-Ok($msg)   { Write-Host "[OK]   $msg" -ForegroundColor Green }
function Write-Warn($msg) { Write-Host "[WARN] $msg" -ForegroundColor Yellow }
function Write-Err($msg)  { Write-Host "[ERR]  $msg" -ForegroundColor Red }

function Ensure-Directory($path) {
    if (-not (Test-Path $path)) { New-Item -ItemType Directory -Path $path | Out-Null }
}

function Get-FileHashSha256($path) {
    (Get-FileHash -Path $path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Safe-Tag($id, $version) {
    $tag = "$TagPrefix/$id/$version"
    return $tag -replace '[^A-Za-z0-9._\-/]', '_'
}

# ---------------------------------------------------------------------------
# Resolve gh CLI
# ---------------------------------------------------------------------------
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    Write-Err "gh CLI is not installed. Install it from https://cli.github.com/ and run 'gh auth login'."
    exit 1
}
$null = gh auth status 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Err "gh CLI is not authenticated. Run 'gh auth login' first."
    exit 1
}

# ---------------------------------------------------------------------------
# Prepare work directories
# ---------------------------------------------------------------------------
$workRoot = if (Test-Path $WorkDir) { Resolve-Path $WorkDir } else { New-Item -ItemType Directory -Path $WorkDir -Force | ForEach-Object { $_.FullName } }
Ensure-Directory "$workRoot\downloads"
Ensure-Directory "$workRoot\packages"
Ensure-Directory "$workRoot\manifests"

# ---------------------------------------------------------------------------
# Step 1: Discover updates
# ---------------------------------------------------------------------------
$updates = @()

if ($Source -eq "WUA") {
    Write-Info "Querying Windows Update Agent for $OSVersion updates..."

    $updateSession = New-Object -ComObject Microsoft.Update.Session
    $updateSearcher = $updateSession.CreateUpdateSearcher()

    $searchQuery = "IsInstalled=0 and Type='Software' and IsHidden=0"
    try {
        $searchResult = $updateSearcher.Search($searchQuery)
    } catch {
        Write-Warn "WUA search failed: $_"
        $searchResult = $null
    }

    if ($searchResult -and $searchResult.Updates.Count -gt 0) {
        Write-Info "Found $($searchResult.Updates.Count) available updates from WUA."

        foreach ($update in $searchResult.Updates) {
            if ($updates.Count -ge $MaxPackages) { break }

            if ($Architecture -ne "all") {
                $archMatch = $false
                foreach ($string in $update.SupportedArchitectures) {
                    if ($string -match $Architecture) {
                        $archMatch = $true
                        break
                    }
                }
                if (-not $archMatch) { continue }
            }

            $downloadUrl = ""
            $fileSize = 0
            foreach ($file in $update.DownloadContents) {
                if ($file.DownloadUrl) {
                    $downloadUrl = $file.DownloadUrl
                    $fileSize = $file.FileSize
                    break
                }
            }

            if (-not $downloadUrl) {
                foreach ($file in $update.DownloadUrls) {
                    if ($file) {
                        $downloadUrl = $file
                        break
                    }
                }
            }

            if (-not $downloadUrl) { continue }

            $kbNumber = "unknown"
            if ($update.Title -match '(?i)kb\d{6,}') {
                $kbNumber = $matches[0].Value.ToLowerInvariant()
            } elseif ($update.KBArticleIDs.Count -gt 0) {
                $kbNumber = $update.KBArticleIDs.Item(0).ToLowerInvariant()
            }

            $osVersionDetected = $OSVersion
            if ($update.Title -match "Windows 11") { $osVersionDetected = "Windows 11" }
            elseif ($update.Title -match "Windows 10") { $osVersionDetected = "Windows 10" }

            $version = if ($update.LastDeploymentChangeTime) {
                $update.LastDeploymentChangeTime.ToString("yyyy-MM")
            } else {
                (Get-Date).ToString("yyyy-MM")
            }

            $updates += [pscustomobject]@{
                Id            = $kbNumber
                Version       = $version
                DisplayName   = $update.Title
                OsVersion     = $osVersionDetected
                Architecture  = $Architecture
                ReleaseDate   = if ($update.LastDeploymentChangeTime) { $update.LastDeploymentChangeTime.ToString("MMMM d, yyyy") } else { (Get-Date).ToString("MMMM d, yyyy") }
                DownloadUrl   = $downloadUrl
                SizeBytes     = $fileSize
                SourceUrl     = "https://www.catalog.update.microsoft.com/Home/Search?q=$([Uri]::EscapeDataString($update.Title))"
                SupportUrl    = "https://support.microsoft.com/help/?kb=$($kbNumber -replace 'kb','')"
            }

            Write-Info "Found: $kbNumber - $($update.Title)"
        }
    } else {
        Write-Warn "No uninstalled updates found from WUA. This machine may be fully patched."
    }
}

if ($Source -eq "Online") {
    Write-Info "Querying online Microsoft Update service for $OSVersion updates..."

    $updateSession = New-Object -ComObject Microsoft.Update.Session
    $updateSearcher = $updateSession.CreateUpdateSearcher()

    # Server selection: 2 = Microsoft Update, 1 = Windows Update, 0 = WSUS
    $updateSearcher.ServerSelection = 2

    $searchQuery = switch ($OSVersion) {
        "Windows 10" { "IsInstalled=0 and Type='Software' and IsHidden=0" }
        "Windows 11" { "IsInstalled=0 and Type='Software' and IsHidden=0" }
        default { "IsInstalled=0 and Type='Software' and IsHidden=0" }
    }

    try {
        Write-Info "Searching online Microsoft Update service..."
        $searchResult = $updateSearcher.Search($searchQuery)
    } catch {
        Write-Warn "Online WUA search failed: $_"
        $searchResult = $null
    }

    if ($searchResult -and $searchResult.Updates.Count -gt 0) {
        Write-Info "Found $($searchResult.Updates.Count) available updates from Microsoft Update service."

        foreach ($update in $searchResult.Updates) {
            if ($updates.Count -ge $MaxPackages) { break }

            if ($Architecture -ne "all") {
                $archMatch = $false
                foreach ($string in $update.SupportedArchitectures) {
                    if ($string -match $Architecture) {
                        $archMatch = $true
                        break
                    }
                }
                if (-not $archMatch) { continue }
            }

            $downloadUrl = ""
            $fileSize = 0
            foreach ($file in $update.DownloadContents) {
                if ($file.DownloadUrl) {
                    $downloadUrl = $file.DownloadUrl
                    $fileSize = $file.FileSize
                    break
                }
            }

            if (-not $downloadUrl) {
                foreach ($file in $update.DownloadUrls) {
                    if ($file) {
                        $downloadUrl = $file
                        break
                    }
                }
            }

            if (-not $downloadUrl) { continue }

            $kbNumber = "unknown"
            if ($update.Title -match '(?i)kb\d{6,}') {
                $kbNumber = $matches[0].Value.ToLowerInvariant()
            } elseif ($update.KBArticleIDs.Count -gt 0) {
                $kbNumber = $update.KBArticleIDs.Item(0).ToLowerInvariant()
            }

            $osVersionDetected = $OSVersion
            if ($update.Title -match "Windows 11") { $osVersionDetected = "Windows 11" }
            elseif ($update.Title -match "Windows 10") { $osVersionDetected = "Windows 10" }

            $version = if ($update.LastDeploymentChangeTime) {
                $update.LastDeploymentChangeTime.ToString("yyyy-MM")
            } else {
                (Get-Date).ToString("yyyy-MM")
            }

            $updates += [pscustomobject]@{
                Id            = $kbNumber
                Version       = $version
                DisplayName   = $update.Title
                OsVersion     = $osVersionDetected
                Architecture  = $Architecture
                ReleaseDate   = if ($update.LastDeploymentChangeTime) { $update.LastDeploymentChangeTime.ToString("MMMM d, yyyy") } else { (Get-Date).ToString("MMMM d, yyyy") }
                DownloadUrl   = $downloadUrl
                SizeBytes     = $fileSize
                SourceUrl     = "https://www.catalog.update.microsoft.com/Home/Search?q=$([Uri]::EscapeDataString($update.Title))"
                SupportUrl    = "https://support.microsoft.com/help/?kb=$($kbNumber -replace 'kb','')"
            }

            Write-Info "Found: $kbNumber - $($update.Title)"
        }
    } else {
        Write-Warn "No updates found from online WUA for $OSVersion. This service requires internet access and Windows Update service enabled."
        Write-Warn "Falling back to manifest mode or manual KB list."
    }
}

if ($Source -eq "Manifest") {
    Write-Info "Loading KB manifest from $KbManifestPath..."
    if (-not (Test-Path $KbManifestPath)) {
        Write-Err "KbManifestPath not found: $KbManifestPath"
        exit 1
    }
    $manifestItems = Get-Content $KbManifestPath -Raw | ConvertFrom-Json
    foreach ($item in $manifestItems) {
        if ($updates.Count -ge $MaxPackages) { break }
        $updates += $item
    }
    Write-Info "Loaded $($updates.Count) items from manifest."
}

if ($updates.Count -eq 0) {
    Write-Warn "No updates discovered. Create a KB manifest file or run on a machine with pending updates."
    Write-Info "Example KB manifest format:"
    Write-Info @'
[
  {
    "Id": "kb5034441",
    "Version": "2024-01",
    "DisplayName": "Windows 10 KB5034441",
    "OsVersion": "Windows 10",
    "Architecture": "x64",
    "ReleaseDate": "January 9, 2024",
    "DownloadUrl": "https://download.microsoft.com/.../windows10.0-kb5034441-x64.msu",
    "SupportUrl": "https://support.microsoft.com/help/5034441"
  }
]
'@
    exit 0
}

Write-Info "Total updates to process: $($updates.Count)"

# ---------------------------------------------------------------------------
# Step 2: Download packages
# ---------------------------------------------------------------------------
$downloaded = @()
foreach ($pkg in $updates) {
    $fileName = if ($pkg.DownloadUrl -match '\/([^\/]+\.(msu|cab|exe))') { $matches[1] } else { "$($pkg.Id).msu" }
    $localPath = Join-Path "$workRoot\downloads" $fileName

    if ($SkipDownload -and (Test-Path $localPath)) {
        Write-Info "Using cached download: $fileName"
        $downloaded += [pscustomobject]@{ Package = $pkg; LocalPath = $localPath }
        continue
    }

    Write-Info "Downloading $fileName ..."
    try {
        curl.exe -L --retry 3 --retry-delay 5 -o $localPath $pkg.DownloadUrl 2>&1 | Out-Null
        if (-not (Test-Path $localPath)) { throw "Download failed: file not created" }
        $size = (Get-Item $localPath).Length
        if ($size -lt 1024) { throw "Downloaded file is too small ($size bytes), probably an HTML error page." }

        Write-Ok "Downloaded $fileName ($size bytes)"
        $downloaded += [pscustomobject]@{ Package = $pkg; LocalPath = $localPath }
    } catch {
        Write-Warn "Failed to download $fileName : $_"
    }
}

Write-Info "Successfully downloaded $($downloaded.Count) packages."

if ($downloaded.Count -eq 0) {
    Write-Err "No packages downloaded. Exiting."
    exit 1
}

# ---------------------------------------------------------------------------
# Step 3: Create WUPM .wupkg packages
# ---------------------------------------------------------------------------
$wupkgFiles = @()
foreach ($item in $downloaded) {
    $pkg = $item.Package
    $localPath = $item.LocalPath
    $pkgDir = Join-Path "$workRoot\packages" $pkg.Id
    Ensure-Directory $pkgDir

    $payloadDest = Join-Path $pkgDir ([System.IO.Path]::GetFileName($localPath))
    Copy-Item $localPath $payloadDest -Force

    $sha256 = Get-FileHashSha256 $localPath
    $payloadSize = (Get-Item $localPath).Length

    $manifest = @{
        id            = $pkg.Id
        version       = $pkg.Version
        displayName   = $pkg.DisplayName
        description   = "$($pkg.DisplayName) for $($pkg.OsVersion) $($pkg.Architecture)"
        publisher     = "Microsoft"
        osVersion     = $pkg.OsVersion
        architecture  = $pkg.Architecture
        channel       = "stable"
        publishedAt   = $pkg.ReleaseDate
        created       = (Get-Date).ToString("yyyy-MM-dd")
        sizeBytes     = $payloadSize
        sha256        = $sha256
        sourceUrl     = $pkg.DownloadUrl
        supportUrl    = $pkg.SupportUrl
        tags          = @("windows-update", $pkg.Id, $pkg.OsVersion.ToLowerInvariant())
        install       = @{
            type     = "wusa"
            command  = "wusa.exe"
            args     = @($payloadDest, "/quiet", "/norestart")
            requiresReboot = $true
        }
        rollback      = @{
            type     = "wusa"
            command  = "wusa.exe"
            args     = @("/uninstall", "/kb:$($pkg.Id -replace 'kb','')", "/quiet", "/norestart")
        }
    } | ConvertTo-Json -Depth 10

    $manifestPath = Join-Path $pkgDir "manifest.json"
    Set-Content -Path $manifestPath -Value $manifest -Encoding UTF8

    $wupkgPath = Join-Path "$workRoot\packages" "$($pkg.Id).wupkg"
    if (Test-Path $wupkgPath) { Remove-Item $wupkgPath -Force }
    $zipPath = $wupkgPath -replace '\.wupkg$','.zip'
    Compress-Archive -Path (Join-Path $pkgDir "*") -DestinationPath $zipPath -Force
    Rename-Item $zipPath $wupkgPath -Force

    $wupkgSize = (Get-Item $wupkgPath).Length
    Write-Ok "Created $($pkg.Id).wupkg ($wupkgSize bytes)"
    $wupkgFiles += [pscustomobject]@{ Package = $pkg; WupkgPath = $wupkgPath; ManifestPath = $manifestPath }
}

# ---------------------------------------------------------------------------
# Step 4: Update local repo/index.json
# ---------------------------------------------------------------------------
$repoIndexPath = Join-Path $workRoot "..\repo\index.json"
if (-not (Test-Path $repoIndexPath)) {
    $repoIndexPath = Join-Path (Get-Location) "repo\index.json"
}
if (Test-Path $repoIndexPath) {
    $index = Get-Content $repoIndexPath -Raw | ConvertFrom-Json
    if (-not $index.packages) { $index | Add-Member -NotePropertyName packages -NotePropertyValue @() }
} else {
    $index = [pscustomobject]@{
        schemaVersion  = "1.0"
        generatedAt    = (Get-Date).ToString("o")
        repositoryUrl  = "https://github.com/$GitHubRepo"
        packages       = @()
    }
}

foreach ($item in $wupkgFiles) {
    $pkg = $item.Package
    $existing = $index.packages | Where-Object { $_.id -eq $pkg.Id -and $_.version -eq $pkg.Version }
    if ($existing) {
        Write-Info "Updating existing entry for $($pkg.Id) $($pkg.Version)"
        $existing.displayName = $pkg.DisplayName
        $existing.architecture = $pkg.Architecture
        $existing.sha256 = (Get-FileHashSha256 $item.WupkgPath)
    } else {
        $index.packages += [pscustomobject]@{
            id            = $pkg.Id
            version       = $pkg.Version
            displayName   = $pkg.DisplayName
            description   = $pkg.Description
            architecture  = $pkg.Architecture
            osVersion     = $pkg.OsVersion
            channel       = "stable"
            publishedAt   = $pkg.ReleaseDate
            sizeBytes     = (Get-Item $item.WupkgPath).Length
            sha256        = (Get-FileHashSha256 $item.WupkgPath)
            sourceUrl     = $pkg.SourceUrl
            supportUrl    = $pkg.SupportUrl
            tags          = @("windows-update", $pkg.Id)
        }
        Write-Info "Added index entry for $($pkg.Id) $($pkg.Version)"
    }
}

$index.generatedAt = (Get-Date).ToString("o")
$index | ConvertTo-Json -Depth 10 | Set-Content $repoIndexPath -Encoding UTF8
Write-Ok "Updated repo/index.json with $($index.packages.Count) packages."

# ---------------------------------------------------------------------------
# Step 5: Upload to GitHub Releases
# ---------------------------------------------------------------------------
if ($WhatIf) {
    Write-Warn "WhatIf mode: skipping GitHub upload."
    foreach ($item in $wupkgFiles) {
        Write-Info "WOULD UPLOAD: $($item.WupkgPath) -> tag: $(Safe-Tag $item.Package.Id $item.Package.Version)"
    }
    exit 0
}

foreach ($item in $wupkgFiles) {
    $pkg = $item.Package
    $tag = Safe-Tag $pkg.Id $pkg.Version

    Write-Info "Ensuring release exists for tag: $tag"
    $releaseExists = gh release view $tag --repo $GitHubRepo 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        $notesPath = Join-Path $workRoot "notes-$($pkg.Id).md"
        $notes = '## ' + $pkg.DisplayName + "`r`n`r`n"
        $notes += '- **ID:** ' + $pkg.Id + "`r`n"
        $notes += '- **Version:** ' + $pkg.Version + "`r`n"
        $notes += '- **OS:** ' + $pkg.OsVersion + ' (' + $pkg.Architecture + ')`r`n'
        $notes += '- **Published:** ' + $pkg.ReleaseDate + "`r`n"
        $notes += '- **SHA256:** ' + (Get-FileHashSha256 $item.WupkgPath) + "`r`n"
        $notes += '- **Source:** [' + $pkg.SourceUrl + '](' + $pkg.SourceUrl + ")`r`n`r`n"
        $notes += '### Install' + "`r`n```powershell`r`n"
        $notes += 'wusa.exe ' + $pkg.Id + '.wupkg /quiet /norestart' + "`r`n"
        $notes += '```'
        Set-Content -Path $notesPath -Value $notes -Encoding UTF8
        gh release create $tag --repo $GitHubRepo --title $tag --notes-file $notesPath 2>&1 | Out-Null
        Write-Host "[OK]   Created release $tag" -ForegroundColor Green
    } else {
        Write-Info "Release $tag already exists, updating assets."
    }

    Write-Info "Uploading $($pkg.Id).wupkg ..."
    gh release upload $tag $item.WupkgPath --repo $GitHubRepo 2>&1 | Out-Null
    Write-Host "[OK]   Uploaded $($pkg.Id).wupkg" -ForegroundColor Green

    $manifestAsset = $item.WupkgPath -replace '\.wupkg$','.manifest.json'
    Copy-Item $item.ManifestPath $manifestAsset -Force
    gh release upload $tag $manifestAsset --repo $GitHubRepo 2>&1 | Out-Null
    Write-Host "[OK]   Uploaded $($pkg.Id).manifest.json" -ForegroundColor Green
}

# ---------------------------------------------------------------------------
# Step 6: Commit and push repo changes
# ---------------------------------------------------------------------------
if (-not $WhatIf) {
    Push-Location (Split-Path $repoIndexPath -Parent)
    try {
        git add repo/index.json
        $diff = git diff --cached --stat
        if ($diff) {
            git commit -m "chore: update package index from catalog sync"
            git push
            Write-Host "[OK]   Pushed index updates." -ForegroundColor Green
        } else {
            Write-Info "No index changes to commit."
        }
    } finally {
        Pop-Location
    }
}

Write-Host ("[OK]   Sync complete. Processed {0} packages." -f $wupkgFiles.Count) -ForegroundColor Green
