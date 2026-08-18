1|<#
2|.SYNOPSIS
3|    Minimal bulk-retrieve Windows update packages from online Microsoft Update service,
4|    wrap them as WUPM .wupkg packages, and upload to GitHub with proper tagging.
5|#>
6|
7|[CmdletBinding()]
8|param(
9|    [ValidateSet("WUA","Online","Manifest")]
10|    [string]$Source = "Online",
11|
12|    [ValidateSet("Windows 10","Windows 11","Windows Server 2019","Windows Server 2022")]
13|    [string]$OSVersion = "Windows 10",
14|
15|    [ValidateSet("x86","x64","arm64","all")]
16|    [string]$Architecture = "x64",
17|
18|    [int]$MaxPackages = 10,
19|
20|    [string]$GitHubRepo = "LoopyLuci/WindowsUpdateAndPackageManager",
21|
22|    [string]$TagPrefix = "updates",
23|
24|    [string]$WorkDir = ".\\wupm-sync-work",
25|
26|    [string]$KbManifestPath = "",
27|
28|    [switch]$WhatIf,
29|
30|    [switch]$SkipDownload
31|)
32|
33|Set-StrictMode -Version Latest
34|$ErrorActionPreference = "Stop"
35|
36|function Write-Info($msg) { Write-Host "[INFO] $msg" -ForegroundColor Cyan }
37|function Write-Ok($msg)   { Write-Host "[OK]   $msg" -ForegroundColor Green }
38|function Write-Warn($msg) { Write-Host "[WARN] $msg" -ForegroundColor Yellow }
39|function Write-Err($msg)  { Write-Host "[ERR]  $msg" -ForegroundColor Red }
40|
41|function Ensure-Directory($path) {
42|    if (-not (Test-Path $path)) { New-Item -ItemType Directory -Path $path | Out-Null }
43|}
44|
45|function Get-FileHashSha256($path) {
46|    (Get-FileHash -Path $path -Algorithm SHA256).Hash.ToLowerInvariant()
47|}
48|
49|function Safe-Tag($id, $version) {
50|    $tag = "$TagPrefix/$id/$version"
51|    return $tag -replace '[^A-Za-z0-9._\-/]', '_'
52|}
53|
54|if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
55|    Write-Err "gh CLI is not installed."
56|    exit 1
57|}
58|$null = gh auth status 2>$null
59|if ($LASTEXITCODE -ne 0) {
60|    Write-Err "gh CLI is not authenticated."
61|    exit 1
62|}
63|
64|$workRoot = if (Test-Path $WorkDir) { Resolve-Path $WorkDir } else { New-Item -ItemType Directory -Path $WorkDir -Force | ForEach-Object { $_.FullName } }
65|Ensure-Directory "$workRoot\downloads"
66|Ensure-Directory "$workRoot\packages"
67|
68|$updates = @()
69|
70|if ($Source -eq "WUA") {
71|    Write-Info "Querying local Windows Update Agent for $OSVersion updates..."
72|    $session = New-Object -ComObject Microsoft.Update.Session
73|    $searcher = $session.CreateUpdateSearcher()
74|    $query = "IsInstalled=0 and Type='Software' and IsHidden=0"
75|    $result = $searcher.Search($query)
76|    if ($result.Updates.Count -gt 0) {
77|        Write-Info "Found $($result.Updates.Count) updates from WUA."
78|        foreach ($update in $result.Updates) {
79|            if ($updates.Count -ge $MaxPackages) { break }
80|            $downloadUrl = ""
81|            try {
82|                foreach ($file in $update.DownloadContents) {
83|                    $url = $null
84|                    try { $url = $file.DownloadUrl } catch {}
85|                    if ($url) { $downloadUrl = $url; break }
86|                }
87|            } catch {}
88|            if (-not $downloadUrl) { continue }
89|            $kbNumber = "unknown"
90|            if ($update.Title -match '(?i)kb\d{6,}') { $kbNumber = $matches[0].ToLowerInvariant() }
91|            elseif ($update.KBArticleIDs.Count -gt 0) { $kbNumber = $update.KBArticleIDs.Item(0).ToLowerInvariant() }
92|            $version = if ($update.LastDeploymentChangeTime) { $update.LastDeploymentChangeTime.ToString("yyyy-MM") } else { (Get-Date).ToString("yyyy-MM") }
93|            $updates += [pscustomobject]@{
94|                Id = $kbNumber
95|                Version = $version
96|                DisplayName = $update.Title
97|                OsVersion = $OSVersion
98|                Architecture = $Architecture
99|                ReleaseDate = if ($update.LastDeploymentChangeTime) { $update.LastDeploymentChangeTime.ToString("MMMM d, yyyy") } else { (Get-Date).ToString("MMMM d, yyyy") }
100|                DownloadUrl = $downloadUrl
101|                SizeBytes = 0
102|                SourceUrl = "https://www.catalog.update.microsoft.com/Home/Search?q=$([Uri]::EscapeDataString($update.Title))"
103|                SupportUrl = "https://support.microsoft.com/help/?kb=$($kbNumber -replace 'kb','')"
104|            }
105|            Write-Info "Found: $kbNumber - $($update.Title)"
106|        }
107|    } else {
108|        Write-Warn "No uninstalled updates found from local WUA."
109|    }
110|}
111|
112|if ($Source -eq "Online") {
113|    Write-Info "Querying online Microsoft Update service for $OSVersion updates..."
114|    $session = New-Object -ComObject Microsoft.Update.Session
115|    $searcher = $session.CreateUpdateSearcher()
116|    $searcher.ServerSelection = 2
117|    $query = "IsInstalled=0 and Type='Software' and IsHidden=0"
118|    try {
119|        $result = $searcher.Search($query)
120|    } catch {
121|        Write-Warn "Online search failed: $_"
122|        $result = $null
123|    }
124|    if ($result -and $result.Updates.Count -gt 0) {
125|        Write-Info "Found $($result.Updates.Count) updates from online Microsoft Update service."
126|        foreach ($update in $result.Updates) {
127|            if ($updates.Count -ge $MaxPackages) { break }
128|            $downloadUrl = ""
129|            try {
130|                foreach ($file in $update.DownloadContents) {
131|                    $url = $null
132|                    try { $url = $file.DownloadUrl } catch {}
133|                    if ($url) { $downloadUrl = $url; break }
134|                }
135|            } catch {}
136|            if (-not $downloadUrl) { continue }
137|            $kbNumber = "unknown"
138|            if ($update.Title -match '(?i)kb\d{6,}') { $kbNumber = $matches[0].ToLowerInvariant() }
139|            elseif ($update.KBArticleIDs.Count -gt 0) { $kbNumber = $update.KBArticleIDs.Item(0).ToLowerInvariant() }
140|            $version = if ($update.LastDeploymentChangeTime) { $update.LastDeploymentChangeTime.ToString("yyyy-MM") } else { (Get-Date).ToString("yyyy-MM") }
141|            $updates += [pscustomobject]@{
142|                Id = $kbNumber
143|                Version = $version
144|                DisplayName = $update.Title
145|                OsVersion = $OSVersion
146|                Architecture = $Architecture
147|                ReleaseDate = if ($update.LastDeploymentChangeTime) { $update.LastDeploymentChangeTime.ToString("MMMM d, yyyy") } else { (Get-Date).ToString("MMMM d, yyyy") }
148|                DownloadUrl = $downloadUrl
149|                SizeBytes = 0
150|                SourceUrl = "https://www.catalog.update.microsoft.com/Home/Search?q=$([Uri]::EscapeDataString($update.Title))"
151|                SupportUrl = "https://support.microsoft.com/help/?kb=$($kbNumber -replace 'kb','')"
152|            }
153|            Write-Info "Found: $kbNumber - $($update.Title)"
154|        }
155|    } else {
156|        Write-Warn "No updates found from online WUA for $OSVersion."
157|    }
158|}
159|
160|if ($Source -eq "Manifest") {
161|    Write-Info "Loading KB manifest from $KbManifestPath..."
162|    if (-not (Test-Path $KbManifestPath)) {
163|        Write-Err "KbManifestPath not found: $KbManifestPath"
164|        exit 1
165|    }
166|    $manifestItems = Get-Content $KbManifestPath -Raw | ConvertFrom-Json
167|    foreach ($item in $manifestItems) {
168|        if ($updates.Count -ge $MaxPackages) { break }
169|        $updates += $item
170|    }
171|    Write-Info "Loaded $($updates.Count) items from manifest."
172|}
173|
174|if ($updates.Count -eq 0) {
175|    Write-Warn "No updates discovered. Create a KB manifest file or run on a machine with pending updates."
176|    Write-Info "Example: .\scripts\Sync-WindowsUpdatesToGitHub.ps1 -Source Manifest -KbManifestPath .\scripts\known-kbs.json"
177|    exit 0
178|}
179|
180|Write-Info "Total updates to process: $($updates.Count)"
181|
182|$downloaded = @()
183|foreach ($pkg in $updates) {
184|    $fileName = if ($pkg.DownloadUrl -match '\/([^\/]+\.(msu|cab|exe))') { $matches[1] } else { "$($pkg.Id).msu" }
185|    $localPath = Join-Path "$workRoot\downloads" $fileName
186|
187|    if ($SkipDownload -and (Test-Path $localPath)) {
188|        Write-Info "Using cached download: $fileName"
189|        $downloaded += [pscustomobject]@{ Package = $pkg; LocalPath = $localPath }
190|        continue
191|    }
192|
193|    Write-Info "Downloading $fileName ..."
194|    $downloadOk = $false
195|    try {
196|        $urlToTry = $pkg.DownloadUrl
197|        if ($urlToTry -match '^http://tlu\.dl\.delivery') {
198|            $urlToTry = $urlToTry -replace '^http://', 'https://'
199|        }
200|        $cookieFile = Join-Path $workRoot "cookies.txt"
201|        curl.exe -L --max-time 900 --connect-timeout 60 --retry 5 --retry-delay 15 -A "Microsoft-Windows-Client-OS/10.0" -c $cookieFile -b $cookieFile -o $localPath $urlToTry 2>&1 | Out-Null
202|        if (-not (Test-Path $localPath)) { throw "Download failed: file not created" }
203|        $size = (Get-Item $localPath).Length
204|        if ($size -lt 1024) { throw "Downloaded file is too small ($size bytes), probably an HTML error page." }
205|
206|        Write-Ok "Downloaded $fileName ($size bytes)"
207|        $downloaded += [pscustomobject]@{ Package = $pkg; LocalPath = $localPath }
208|        $downloadOk = $true
209|    } catch {
210|        Write-Warn "Direct download failed for $fileName : $_"
211|    }
212|
213|    if (-not $downloadOk) {
214|        Write-Info "Attempting download via Windows Update Agent for $fileName ..."
215|        try {
216|            $dlSession = New-Object -ComObject Microsoft.Update.Session
217|            $dlSearcher = $dlSession.CreateUpdateSearcher()
218|            $dlSearcher.ServerSelection = 2
219|
220|            $kbQuery = if ($pkg.Id -match '(?i)kb(\d+)') { "KBArticleIDs='$($matches[1])'" } else { "Title='$($pkg.DisplayName)'" }
221|            $searchResult2 = $dlSearcher.Search($kbQuery)
222|
223|            if ($searchResult2.Updates.Count -gt 0) {
224|                $update2 = $searchResult2.Updates.Item(0)
225|                $dlCollection = $update2.Updates
226|                if ($dlCollection.Count -eq 0) { $dlCollection = $update2 }
227|
228|                $downloader = $dlSession.CreateUpdateDownloader()
229|                $downloader.Updates = $dlCollection
230|                $dlResult = $downloader.Download()
231|
232|                if ($dlResult.ResultCode -eq 2) {
233|                    $update2.Refresh()
234|                    $cachedPath = $null
235|                    for ($i=0; $i -lt $update2.DownloadContents.Count; $i++) {
236|                        $dc = $update2.DownloadContents.Item($i)
237|                        try {
238|                            $url = $dc.DownloadUrl
239|                            if ($url) {
240|                                $fname = [System.IO.Path]::GetFileName($url.Split('?')[0])
241|                                if (-not $fname) { $fname = "$($pkg.Id).msu" }
242|                                $candidate = Join-Path $env:TEMP $fname
243|                                if (Test-Path $candidate) { $cachedPath = $candidate; break }
244|                                $candidate2 = Join-Path $workRoot "downloads" $fname
245|                                if (Test-Path $candidate2) { $cachedPath = $candidate2; break }
246|                            }
247|                        } catch {}
248|                    }
249|
250|                    if ($cachedPath) {
251|                        Write-Ok "WUA downloaded to $cachedPath"
252|                        $downloaded += [pscustomobject]@{ Package = $pkg; LocalPath = $cachedPath }
253|                    } else {
254|                        Write-Warn "WUA download completed but file path unknown for $($pkg.Id)"
255|                    }
256|                } else {
257|                    Write-Warn "WUA download result code: $($dlResult.ResultCode) for $($pkg.Id)"
258|                }
259|            } else {
260|                Write-Warn "WUA fallback found 0 results for $fileName"
261|            }
262|        } catch {
263|            Write-Warn "WUA fallback failed for $fileName : $_"
264|        }
265|    }
266|}
267|
268|Write-Info "Successfully downloaded $($downloaded.Count) packages."
269|
270|if ($downloaded.Count -eq 0) {
271|    Write-Err "No packages downloaded. Exiting."
272|    exit 1
273|}
274|
275|$wupkgFiles = @()
276|foreach ($item in $downloaded) {
277|    $pkg = $item.Package
278|    $localPath = $item.LocalPath
279|    $pkgDir = Join-Path "$workRoot\packages" $pkg.Id
280|    Ensure-Directory $pkgDir
281|
282|    $payloadDest = Join-Path $pkgDir ([System.IO.Path]::GetFileName($localPath))
283|    Copy-Item $localPath $payloadDest -Force
284|
285|    $sha256 = Get-FileHashSha256 $localPath
286|    $payloadSize = (Get-Item $localPath).Length
287|
288|    $manifest = @{
289|        id            = $pkg.Id
290|        version       = $pkg.Version
291|        displayName   = $pkg.DisplayName
292|        description   = "$($pkg.DisplayName) for $($pkg.OsVersion) $($pkg.Architecture)"
293|        publisher     = "Microsoft"
294|        osVersion     = $pkg.OsVersion
295|        architecture  = $pkg.Architecture
296|        channel       = "stable"
297|        publishedAt   = $pkg.ReleaseDate
298|        created       = (Get-Date).ToString("yyyy-MM-dd")
299|        sizeBytes     = $payloadSize
300|        sha256        = $sha256
301|        sourceUrl     = $pkg.DownloadUrl
302|        supportUrl    = $pkg.SupportUrl
303|        tags          = @("windows-update", $pkg.Id, $pkg.OsVersion.ToLowerInvariant())
304|        install       = @{
305|            type     = "wusa"
306|            command  = "wusa.exe"
307|            args     = @($payloadDest, "/quiet", "/norestart")
308|            requiresReboot = $true
309|        }
310|        rollback      = @{
311|            type     = "wusa"
312|            command  = "wusa.exe"
313|            args     = @("/uninstall", "/kb:$($pkg.Id -replace 'kb','')", "/quiet", "/norestart")
314|        }
315|    } | ConvertTo-Json -Depth 10
316|
317|    $manifestPath = Join-Path $pkgDir "manifest.json"
318|    Set-Content -Path $manifestPath -Value $manifest -Encoding UTF8
319|
320|    $wupkgPath = Join-Path "$workRoot\packages" "$($pkg.Id).wupkg"
321|    if (Test-Path $wupkgPath) { Remove-Item $wupkgPath -Force }
322|    $zipPath = $wupkgPath -replace '\.wupkg$','.zip'
323|    Compress-Archive -Path (Join-Path $pkgDir "*") -DestinationPath $zipPath -Force
324|    Rename-Item $zipPath $wupkgPath -Force
325|
326|    $wupkgSize = (Get-Item $wupkgPath).Length
327|    Write-Ok "Created $($pkg.Id).wupkg ($wupkgSize bytes)"
328|    $wupkgFiles += [pscustomobject]@{ Package = $pkg; WupkgPath = $wupkgPath; ManifestPath = $manifestPath }
329|}
330|
331|$repoIndexPath = Join-Path $workRoot "..\repo\index.json"
332|if (-not (Test-Path $repoIndexPath)) {
333|    $repoIndexPath = Join-Path (Get-Location) "repo\index.json"
334|}
335|if (Test-Path $repoIndexPath) {
336|    $index = Get-Content $repoIndexPath -Raw | ConvertFrom-Json
337|    if (-not $index.packages) { $index | Add-Member -NotePropertyName packages -NotePropertyValue @() }
338|} else {
339|    $index = [pscustomobject]@{
340|        schemaVersion  = "1.0"
341|        generatedAt    = (Get-Date).ToString("o")
342|        repositoryUrl  = "https://github.com/$GitHubRepo"
343|        packages       = @()
344|    }
345|}
346|
347|foreach ($item in $wupkgFiles) {
348|    $pkg = $item.Package
349|    $existing = $index.packages | Where-Object { $_.id -eq $pkg.Id -and $_.version -eq $pkg.Version }
350|    if ($existing) {
351|        Write-Info "Updating existing entry for $($pkg.Id) $($pkg.Version)"
352|        $existing.displayName = $pkg.DisplayName
353|        $existing.architecture = $pkg.Architecture
354|        $existing.sha256 = (Get-FileHashSha256 $item.WupkgPath)
355|    } else {
356|        $index.packages += [pscustomobject]@{
357|            id            = $pkg.Id
358|            version       = $pkg.Version
359|            displayName   = $pkg.DisplayName
360|            description   = $pkg.Description
361|            architecture  = $pkg.Architecture
362|            osVersion     = $pkg.OsVersion
363|            channel       = "stable"
364|            publishedAt   = $pkg.ReleaseDate
365|            sizeBytes     = (Get-Item $item.WupkgPath).Length
366|            sha256        = (Get-FileHashSha256 $item.WupkgPath)
367|            sourceUrl     = $pkg.SourceUrl
368|            supportUrl    = $pkg.SupportUrl
369|            tags          = @("windows-update", $pkg.Id)
370|        }
371|        Write-Info "Added index entry for $($pkg.Id) $($pkg.Version)"
372|    }
373|}
374|
375|$index.generatedAt = (Get-Date).ToString("o")
376|$index | ConvertTo-Json -Depth 10 | Set-Content $repoIndexPath -Encoding UTF8
377|Write-Ok "Updated repo/index.json with $($index.packages.Count) packages."
378|
379|if ($WhatIf) {
380|    Write-Warn "WhatIf mode: skipping GitHub upload."
381|    foreach ($item in $wupkgFiles) {
382|        Write-Info "WOULD UPLOAD: $($item.WupkgPath) -> tag: $(Safe-Tag $item.Package.Id $item.Package.Version)"
383|    }
384|    exit 0
385|}
386|
387|foreach ($item in $wupkgFiles) {
388|    $pkg = $item.Package
389|    $tag = Safe-Tag $pkg.Id $pkg.Version
390|
391|    Write-Info "Ensuring release exists for tag: $tag"
392|    $releaseExists = gh release view $tag --repo $GitHubRepo 2>&1 | Out-Null
393|    if ($LASTEXITCODE -ne 0) {
394|        $notesPath = Join-Path $workRoot "notes-$($pkg.Id).md"
395|        $notes = ""
396|        $notes += "## $($pkg.DisplayName)`r`n`r`n"
397|        $notes += "- **ID:** $($pkg.Id)`r`n"
398|        $notes += "- **Version:** $($pkg.Version)`r`n"
399|        $notes += "- **OS:** $($pkg.OsVersion) ($($pkg.Architecture))`r`n"
400|        $notes += "- **Published:** $($pkg.ReleaseDate)`r`n"
401|        $notes += "- **SHA256:** $(Get-FileHashSha256 $item.WupkgPath)`r`n"
402|        $notes += "- **Source:** [$($pkg.SourceUrl)]($($pkg.SourceUrl))`r`n`r`n"
403|        $notes += "### Install`r`n```powershell`r`n"
404|        $notes += "wusa.exe $($pkg.Id).wupkg /quiet /norestart`r`n"
405|        $notes += "```"
406|        Set-Content -Path $notesPath -Value $notes -Encoding UTF8
407|        gh release create $tag --repo $GitHubRepo --title $tag --notes-file $notesPath 2>&1 | Out-Null
408|        Write-Host ("OK_   Created release {0}" -f $tag) -ForegroundColor Green
409|    } else {
410|        Write-Info "Release $tag already exists, updating assets."
411|    }
412|
413|    Write-Info "Uploading $($pkg.Id).wupkg ..."
414|    gh release upload $tag $item.WupkgPath --repo $GitHubRepo 2>&1 | Out-Null
415|    Write-Host ("OK_   Uploaded {0}.wupkg" -f $pkg.Id) -ForegroundColor Green
416|
417|    $manifestAsset = $item.WupkgPath -replace '\.wupkg$','.manifest.json'
418|    Copy-Item $item.ManifestPath $manifestAsset -Force
419|    gh release upload $tag $manifestAsset --repo $GitHubRepo 2>&1 | Out-Null
420|    Write-Host ("OK_   Uploaded {0}.manifest.json" -f $pkg.Id) -ForegroundColor Green
421|}
422|
423|if (-not $WhatIf) {
424|    Push-Location (Split-Path $repoIndexPath -Parent)
425|    try {
426|        git add repo/index.json
427|        $diff = git diff --cached --stat
428|        if ($diff) {
429|            git commit -m "chore: update package index from catalog sync"
430|            git push
431|            Write-Host "OK_   Pushed index updates." -ForegroundColor Green
432|        } else {
433|            Write-Info "No index changes to commit."
434|        }
435|    } finally {
436|        Pop-Location
437|    }
438|}
439|
440|Write-Host ("OK_   Sync complete. Processed {0} packages." -f $wupkgFiles.Count) -ForegroundColor Green
441|