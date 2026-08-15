$p = Get-Process WupmGui -ErrorAction SilentlyContinue | Select-Object -First 1
if ($p) {
    Write-Host ("Id=" + $p.Id)
    Write-Host ("Responding=" + $p.Responding)
    Write-Host ("MainWindowTitle=" + $p.MainWindowTitle)
    Write-Host ("MainWindowHandle=" + $p.MainWindowHandle)
} else {
    Write-Host "No WupmGui process"
}
