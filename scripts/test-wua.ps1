$s = New-Object -ComObject Microsoft.Update.Session
$ss = $s.CreateUpdateSearcher()
$r = $ss.Search("IsInstalled=0 and Type='Software' and IsHidden=0")
Write-Host ("Results: {0}" -f $r.Updates.Count)
if ($r.Updates.Count -eq 0) {
    $r2 = $ss.Search("Type='Software'")
    Write-Host ("All software results: {0}" -f $r2.Updates.Count)
    $r = $r2
}
for ($i=0; $i -lt [Math]::Min(5, $r.Updates.Count); $i++) {
    $u = $r.Updates.Item($i)
    Write-Host ('{0}: {1}' -f $i, $u.Title)
    foreach ($f in $u.DownloadContents) {
        Write-Host ('  URL: {0}' -f $f.DownloadUrl)
        break
    }
}
