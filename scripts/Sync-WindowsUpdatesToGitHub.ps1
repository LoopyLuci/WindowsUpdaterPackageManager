<#
.SYNOPSIS
    Bulk-retrieve Windows update packages from the Microsoft Update Catalog,
    wrap them as WUPM .wupkg packages, and upload to GitHub with proper tagging.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$false)]
    [string]$SearchQuery = "Windows 10 cumulative",

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
gh auth status 2>&1 | Out-Null

# ---------------------------------------------------------------------------
# Prepare work directories
# ---------------------------------------------------------------------------
$workRoot = if (Test-Path $WorkDir) { Resolve-Path $WorkDir } else { New-Item -ItemType Directory -Path $WorkDir -Force | ForEach-Object { $_.FullName } }
Ensure-Directory "$workRoot\downloads"
Ensure-Directory "$workRoot\packages"
Ensure-Directory "$workRoot\manifests"

# ---------------------------------------------------------------------------
# Step 1: Search the Microsoft Update Catalog via official search endpoint
# ---------------------------------------------------------------------------
Write-Info "Searching Microsoft Update Catalog for '$SearchQuery' (arch=$Architecture, max=$MaxPackages)"

$catalogSearchUrl = "https://www.catalog.update.microsoft.com/Home/Search"

$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$session.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"

# Try POST search first
$searchBody = @{ searchText = $SearchQuery } | ConvertTo-Json
$searchHeaders = @{ "Content-Type" = "application/json; charset=utf-8" }

$searchHtml = ""
try {
    $searchResponse = Invoke-RestMethod -Uri $catalogSearchUrl -Method POST -Body $searchBody -ContentType "application/json" -WebSession $session -TimeoutSec 60
    $searchHtml = $searchResponse | Out-String
} catch {
    Write-Warn "POST search failed: $_"
    # Try the search API endpoint
    try {
        $escapedQuery = [Uri]::EscapeDataString($SearchQuery)
        $apiResponse = Invoke-RestMethod -Uri "https://www.catalog.update.microsoft.com/Home/Search?q=$escapedQuery" -WebSession $session -TimeoutSec 60
        $searchHtml = $apiResponse | Out-String
    } catch {
        Write-Warn "API search failed: $_"
    }
}

# If REST failed, try web request
if (-not $searchHtml) {
    try {
        $escapedQuery = [Uri]::EscapeDataString($SearchQuery)
        $searchResponse = Invoke-WebRequest -Uri "https://www.catalog.update.microsoft.com/Search.aspx?q=$escapedQuery" -WebSession $session -UseBasicParsing -TimeoutSec 60
        $searchHtml = $searchResponse.Content
    } catch {
        Write-Warn "Web search failed: $_"
    }
}

if (-not $searchHtml) {
    Write-Err "Could not retrieve catalog search results. The catalog may be blocking automated requests."
    exit 1
}

# Try multiple patterns to find update IDs
$resultIds = @()
$patterns = @(
    'updateIDs\.push\(\{id:\s*"(\d+)"',
    '"updateId"\s*:\s*"(\d+)"',
    'dlgId_(\d+)',
    'DownloadForm\.aspx\?(\d+)',
    'data-id="(\d+)"'
)

foreach ($pattern in $patterns) {
    $matches = [regex]::Matches($searchHtml, $pattern) | ForEach-Object { $_.Groups[1].Value }
    if ($matches) {
        $resultIds += $matches
    }
}

$resultIds = $resultIds | Select-Object -Unique
Write-Info "Found $($resultIds.Count) catalog results."

if ($resultIds.Count -eq 0) {
    Write-Warn "No results found from catalog. Try a different search query or check network access."
    exit 0
}

$selected = $resultIds | Select-Object -First $MaxPackages
Write-Info "Selected $($selected.Count) packages for processing."

# ---------------------------------------------------------------------------
# Step 2: Resolve download URLs from catalog detail pages
# ---------------------------------------------------------------------------
$packages = @()

foreach ($id in $selected) {
    try {
        # Try to get update details via catalog API
        $detailUrl = "https://www.catalog.update.microsoft.com/Home/GetUpdateDetails/$id"
        $detailResponse = $null
        try {
            $detailResponse = Invoke-RestMethod -Uri $detailUrl -WebSession $session -TimeoutSec 60
        } catch {
            # Fallback to DownloadForm
            $detailUrl = "https://www.catalog.update.microsoft.com/DownloadForm.aspx?$id"
            $detailResponse = Invoke-WebRequest -Uri $detailUrl -WebSession $session -UseBasicParsing -TimeoutSec 60
        }

        $detailHtml = ""
        if ($detailResponse -is [string]) {
            $detailHtml = $detailResponse
        } elseif ($detailResponse -is [System.Management.Automation.PSObject]) {
            $detailHtml = $detailResponse | Out-String
        } else {
            $detailHtml = $detailResponse.ToString()
        }

        # Look for download links
        $downloadLinks = @()
        $patterns = @(
            'https://download\.microsoft\.com/download/[^''"]+\.(msu|cab|exe)',
            'href="(https://[^"]+\.(msu|cab|exe))"',
            'href=''([^'']+\.(msu|cab|exe))'''
        )

        foreach ($pattern in $patterns) {
            $matches = [regex]::Matches($detailHtml, $pattern) | ForEach-Object { $_.Groups[1].Value }
            if ($matches) {
                $downloadLinks += $matches
            }
        }
        $downloadLinks = $downloadLinks | Select-Object -Unique

        if (-not $downloadLinks) {
            Write-Warn "No download link found for catalog ID $id, skipping."
            continue
        }

        # Filter by architecture
        $filtered = @($downloadLinks)
        if ($Architecture -ne "all") {
            $filtered = $downloadLinks | Where-Object { $_ -match $Architecture }
            if (-not $filtered) {
                Write-Warn "No $Architecture link for catalog ID $id, skipping."
                continue
            }
        }

        $downloadUrl = $filtered[0]

        # Extract metadata
        $kbNumber = if ($downloadUrl -match '(?i)kb\d+') { $matches[0].Value.ToUpperInvariant() } else { "KB$id" }
        if (-not $kbNumber -or $kbNumber -eq "KB$id") {
            $kbMatch = [regex]::Match($detailHtml, '(?i)kb\d{6,}')
            if ($kbMatch.Success) { $kbNumber = $kbMatch.Value.ToUpperInvariant() }
        }

        $title = if ($detailHtml -match '<title>([^<]+)</title>') { $matches[1].Value.Trim() } else { "$kbNumber Update" }
        if (-not $title) { $title = "$kbNumber Update" }

        $osVersion = "Windows 10"
        if ($title -match "Windows 11") { $osVersion = "Windows 11" }
        if ($title -match "Server 2019") { $osVersion = "Windows Server 2019" }
        if ($title -match "Server 2022") { $osVersion = "Windows Server 2022" }

        $version = if ($downloadUrl -match '(\d{4}-\d{2})') { $matches[1] } else { (Get-Date).ToString("yyyy-MM") }

        $packages += [pscustomobject]@{
            Id            = $kbNumber.ToLowerInvariant()
            Version       = $version
            DisplayName   = $title
            OsVersion     = $osVersion
            Architecture  = $Architecture
            ReleaseDate   = (Get-Date).ToString("MMMM d, yyyy")
            DownloadUrl   = $downloadUrl
            CatalogId     = $id
            SourceUrl     = "https://www.catalog.update.microsoft.com/DownloadForm.aspx?$id"
            SupportUrl    = "https://support.microsoft.com/help/?kb=$($kbNumber -replace 'kb','')"
        }

        Write-Info "Resolved: $kbNumber - $title"
    } catch {
        Write-Warn "Failed to process catalog ID $id : $_"
    }
}

Write-Info "Resolved $($packages.Count) downloadable packages."

if ($packages.Count -eq 0) {
    Write-Warn "No packages resolved. Exiting."
    exit 0
}

# ---------------------------------------------------------------------------
# Step 3: Download packages
# ---------------------------------------------------------------------------
$downloaded = @()
foreach ($pkg in $packages) {
    $fileName = [System.IO.Path]::GetFileName($pkg.DownloadUrl)
    if (-not $fileName -or $fileName.Length -lt 5) {
        $fileName = "$($pkg.Id)_$($pkg.Version).msu"
    }
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

# ---------------------------------------------------------------------------
# Step 4: Create WUPM .wupkg packages
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
# Step 5: Update local repo/index.json
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
# Step 6: Upload to GitHub Releases
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
# Step 7: Commit and push repo changes
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
