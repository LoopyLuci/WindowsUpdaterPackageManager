Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$screen = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$bmp = New-Object System.Drawing.Bitmap($screen.Width, $screen.Height)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($screen.Location, [System.Drawing.Point]::Empty, $screen.Size)

$path = "D:\Projects\WindowsUpdatePackageManager\publish\screenshot.png"
$bmp.Save($path)
$g.Dispose()
$bmp.Dispose()
Write-Host "Saved screenshot to $path"
