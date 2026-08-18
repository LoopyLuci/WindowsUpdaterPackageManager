<#
.SYNOPSIS
    Minimal bulk-retrieve Windows update packages from online Microsoft Update service,
    wrap them as WUPM .wupkg packages, and upload to GitHub with proper tagging.
#>

[CmdletBinding()]
param(
    [ValidateSet("WUA","Online","Manifest")]
    [string]$Source = "Online",

    [ValidateSet("Windows 10","Windows 11","Windows Server 2019","Windows Server 2022")]
    [string]$OSVersion = "Windows 10",

    [ValidateSet("x86","x64","arm64","all")]
    [string]$Architecture = "x64",

    [int]$MaxPackages = 10,

    [string]$GitHubRepo = "LoopyLuci/WindowsUpdateAndPackageManager",

    [string]$TagPrefix = "updates",

    [string]$WorkDir = ".\wupm-sync-work",

    [string]$KbManifestPath = "",

    [switch]$WhatIf,

    [switch]$SkipDownload
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

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

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    Write-Err "gh CLI is not installed."
    exit 1
}
$null = gh auth status 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Err "gh CLI is not authenticated."
    exit 1
}

$workRoot = if (Test-Path $WorkDir) { Resolve-Path $WorkDir } else { New-Item -ItemType Directory -Path $WorkDir -Force | ForEach-Object { $_.FullName } }
Ensure-Directory "$workRoot\downloads"
Ensure-Directory "$workRoot\packages"

$updates = @()

if ($Source -eq "WUA") {
    Write-Info "Querying local Windows Update Agent for $OSVersion updates..."
    $session = New-Object -ComObject Microsoft.Update.Session
    $searcher = $session.CreateUpdateSearcher()
    $query = "IsInstalled=0 and Type='Software' and IsHidden=0"
    $result = $searcher.Search($query)
    if ($result.Updates.Count -gt 0) {
        Write-Info "Found $($result.Updates.Count) updates from WUA."
        foreach ($update in $result.Updates) {
            if ($updates.Count -ge $MaxPackages) { break }
            $downloadUrl = ""
            try {
                foreach ($file in $update.DownloadContents) {
                    $url = $null
                    try { $url = $file.DownloadUrl } catch {}
                    if ($url) { $downloadUrl = $url; break }
                }
            } catch {}
            if (-not $downloadUrl) { continue }
            $kbNumber = "unknown"
            if ($update.Title -match '(?i)kb\d{6,}') { $kbNumber = $matches[0].ToLowerInvariant() }
            elseif ($update.KBArticleIDs.Count -gt 0) { $kbNumber = $update.KBArticleIDs.Item(0).ToLowerInvariant() }
            $version = if ($update.LastDeploymentChangeTime) { $update.LastDeploymentChangeTime.ToString("yyyy-MM") } else { (Get-Date).ToString("yyyy-MM") }
            $updates += [pscustomobject]@{
                Id = $kbNumber
                Version = $version
                DisplayName = $update.Title
                OsVersion = $OSVersion
                Architecture = $Architecture
                ReleaseDate = if ($update.LastDeploymentChangeTime) { $update.LastDeploymentChangeTime.ToString("MMMM d, yyyy") } else { (Get-Date).ToString("MMMM d, yyyy") }
                DownloadUrl = $downloadUrl
                SizeBytes = 0
                SourceUrl = "https://www.catalog.update.microsoft.com/Home/Search?q=$([Uri]::EscapeDataString($update.Title))"
                SupportUrl = "https://support.microsoft.com/help/?kb=$($kbNumber -replace 'kb','')"
            }
            Write-Info "Found: $kbNumber - $($update.Title)"
        }
    } else {
        Write-Warn "No uninstalled updates found from local WUA."
    }
}

if ($Source -eq "Online") {
    Write-Info "Querying online Microsoft Update service for $OSVersion updates..."
    $session = New-Object -ComObject Microsoft.Update.Session
    $searcher = $session.CreateUpdateSearcher()
    $searcher.ServerSelection = 2
    $query = "IsInstalled=0 and Type='Software' and IsHidden=0"
    try {
        $result = $searcher.Search($query)
    } catch {
        Write-Warn "Online search failed: $_"
        $result = $null
    }
    if ($result -and $result.Updates.Count -gt 0) {
        Write-Info "Found $($result.Updates.Count) updates from online Microsoft Update service."
        foreach ($update in $result.Updates) {
            if ($updates.Count -ge $MaxPackages) { break }
            $downloadUrl = ""
            try {
                foreach ($file in $update.DownloadContents) {
                    $url = $null
                    try { $url = $file.DownloadUrl } catch {}
                    if ($url) { $downloadUrl = $url; break }
                }
            } catch {}
            if (-not $downloadUrl) { continue }
            $kbNumber = "unknown"
            if ($update.Title -match '(?i)kb\d{6,}') { $kbNumber = $matches[0].ToLowerInvariant() }
            elseif ($update.KBArticleIDs.Count -gt 0) { $kbNumber = $update.KBArticleIDs.Item(0).ToLowerInvariant() }
            $version = if ($update.LastDeploymentChangeTime) { $update.LastDeploymentChangeTime.ToString("yyyy-MM") } else { (Get-Date).ToString("yyyy-MM") }
            $updates += [pscustomobject]@{
                Id = $kbNumber
                Version = $version
                DisplayName = $update.Title
                OsVersion = $OSVersion
                Architecture = $Architecture
                ReleaseDate = if ($update.LastDeploymentChangeTime) { $update.LastDeploymentChangeTime.ToString("MMMM d, yyyy") } else { (Get-Date).ToString("MMMM d, yyyy") }
                DownloadUrl = $downloadUrl
                SizeBytes = 0
                SourceUrl = "https://www.catalog.update.microsoft.com/Home/Search?q=$([Uri]::EscapeDataString($update.Title))"
                SupportUrl = "https://support.microsoft.com/help/?kb=$($kbNumber -replace 'kb','')"
            }
            Write-Info "Found: $kbNumber - $($update.Title)"
        }
    } else {
        Write-Warn "No updates found from online WUA for $OSVersion."
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
    Write-Info "Example: .\scripts\Sync-WindowsUpdatesToGitHub.ps1 -Source Manifest -KbManifestPath .\scripts\known-kbs.json"
    exit 0
}

Write-Info "Total updates to process: $($updates.Count)"

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
    $downloadOk = $false
    try {
        $urlToTry = $pkg.DownloadUrl
        if ($urlToTry -match '^http://tlu\.dl\.delivery') {
            $urlToTry = $urlToTry -replace '^http://', 'https://'
        }
        $cookieFile = Join-Path $workRoot "cookies.txt"
        curl.exe -L --max-time 900 --connect-timeout 60 --retry 5 --retry-delay 15 -A "Microsoft-Windows-Client-OS/10.0" -c $cookieFile -b $cookieFile -o $localPath $urlToTry 2>&1 | Out-Null
        if (-not (Test-Path $localPath)) { throw "Download failed: file not created" }
        $size = (Get-Item $localPath).Length
        if ($size -lt 1024) { throw "Downloaded file is too small ($size bytes), probably an HTML error page." }

        Write-Ok "Downloaded $fileName ($size bytes)"
        $downloaded += [pscustomobject]@{ Package = $pkg; LocalPath = $localPath }
        $downloadOk = $true
    } catch {
        Write-Warn "Direct download failed for $fileName : $_"
    }

    if (-not $downloadOk) {
        Write-Info "Attempting download via Windows Update Agent for $fileName ..."
        try {
            $dlSession = New-Object -ComObject Microsoft.Update.Session
            $dlSearcher = $dlSession.CreateUpdateSearcher()
            $dlSearcher.ServerSelection = 2

            $kbQuery = if ($pkg.Id -match '(?i)kb(\d+)') { "KBArticleIDs='$($matches[1])'" } else { "Title='$($pkg.DisplayName)'" }
            $searchResult2 = $dlSearcher.Search($kbQuery)

            if ($searchResult2.Updates.Count -gt 0) {
                $update2 = $searchResult2.Updates.Item(0)
                $dlCollection = $update2.Updates
                if ($dlCollection.Count -eq 0) { $dlCollection = $update2 }

                $downloader = $dlSession.CreateUpdateDownloader()
                $downloader.Updates = $dlCollection
                if ($update2.IsDownloaded) {
                    Write-Info "WUA reports update already downloaded/cached."
                    $dlResult = $null
                } else {
                    $dlResult = $downloader.Download()
                }

                if ($null -ne $dlResult -and $dlResult.ResultCode -ne 2) {
                    Write-Warn "WUA download result code: $($dlResult.ResultCode) for $($pkg.Id)"
                }

                $update2.Refresh()
                $cachedPath = $null
                for ($i=0; $i -lt $update2.DownloadContents.Count; $i++) {
                    $dc = $update2.DownloadContents.Item($i)
                    try {
                        $url = $dc.DownloadUrl
                        if ($url) {
                            $fname = [System.IO.Path]::GetFileName($url.Split('?')[0])
                            if (-not $fname) { $fname = "$($pkg.Id).msu" }
                            $candidate = Join-Path $env:TEMP $fname
                            if (Test-Path $candidate) { $cachedPath = $candidate; break }
                            $candidate2 = Join-Path $workRoot "downloads" $fname
                            if (Test-Path $candidate2) { $cachedPath = $candidate2; break }
                            $candidate3 = Join-Path $env:SystemRoot "SoftwareDistribution\Download\$fname"
                            if (Test-Path $candidate3) { $cachedPath = $candidate3; break }
                        }
                    } catch {}
                }

                if ($cachedPath) {
                    Write-Ok "WUA downloaded to $cachedPath"
                    $downloaded += [pscustomobject]@{ Package = $pkg; LocalPath = $cachedPath }
                } else {
                    Write-Warn "WUA download completed but cached file path unknown for $($pkg.Id)"
                }
            } else {
                Write-Warn "WUA fallback found 0 results for $fileName"
            }
        } catch {
            Write-Warn "WUA fallback failed for $fileName : $_"
        }
    }
}

Write-Info "Successfully downloaded $($downloaded.Count) packages."

if ($downloaded.Count -eq 0) {
    Write-Err "No packages downloaded. Exiting."
    exit 1
}

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
        $mdLines = @()
        $mdLines += "## $($pkg.DisplayName)"
        $mdLines += ""
        $mdLines += "- **ID:** $($pkg.Id)"
        $mdLines += "- **Version:** $($pkg.Version)"
        $mdLines += "- **OS:** $($pkg.OsVersion) ($($pkg.Architecture))"
        $mdLines += "- **Published:** $($pkg.ReleaseDate)"
        $mdLines += "- **SHA256:** $(Get-FileHashSha256 $item.WupkgPath)"
        $mdLines += "- **Source:** $($pkg.SourceUrl)"
        $mdLines += ""
        $mdLines += "### Install"
        $mdLines += "```powershell"
        $mdLines += "wusa.exe $($pkg.Id).wupkg /quiet /norestart"
        $mdLines += '```'
        Set-Content -Path $notesPath -Value ($mdLines -join "`r`n") -Encoding UTF8
        gh release create $tag --repo $GitHubRepo --title $tag --notes-file $notesPath 2>&1 | Out-Null
        Write-Host $("OK_   Created release {0}" -f $tag) -ForegroundColor Green
    } else {
        Write-Info "Release $tag already exists, updating assets."
    }

    Write-Info "Uploading $($pkg.Id).wupkg ..."
    gh release upload $tag $item.WupkgPath --repo $GitHubRepo 2>&1 | Out-Null
    Write-Host $("OK_   Uploaded {0}.wupkg" -f $pkg.Id) -ForegroundColor Green

    $manifestAsset = $item.WupkgPath -replace '\.wupkg$','.manifest.json'
    Copy-Item $item.ManifestPath $manifestAsset -Force
    gh release upload $tag $manifestAsset --repo $GitHubRepo 2>&1 | Out-Null
    Write-Host $("OK_   Uploaded {0}.manifest.json" -f $pkg.Id) -ForegroundColor Green
}

if (-not $WhatIf) {
    Push-Location (Split-Path $repoIndexPath -Parent)
    try {
        git add repo/index.json
        $diff = git diff --cached --stat
        if ($diff) {
            git commit -m "chore: update package index from catalog sync"
            git push
            Write-Host "OK_   Pushed index updates." -ForegroundColor Green
        } else {
            Write-Info "No index changes to commit."
        }
    } finally {
        Pop-Location
    }
}

Write-Host $("OK_   Sync complete. Processed {0} packages." -f $wupkgFiles.Count) -ForegroundColor Green
