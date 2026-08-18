$r = Invoke-WebRequest -Uri "https://www.catalog.update.microsoft.com/Search.aspx?q=Windows+10+cumulative" -UseBasicParsing
$matches = [regex]::Matches($r.Content, 'DownloadForm.aspx\?([^"\''<>\s]+)')
Write-Host "DownloadForm matches: $($matches.Count)"
$matches | Select-Object -First 5 | ForEach-Object { $_.Value }
