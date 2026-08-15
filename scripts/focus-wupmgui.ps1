Add-Type @'
using System;
using System.Runtime.InteropServices;
public class Win32 {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
}
'@

$p = Get-Process WupmGui -ErrorAction SilentlyContinue | Select-Object -First 1
if ($p) {
    $h = $p.MainWindowHandle
    if ([Win32]::IsIconic($h)) { [Win32]::ShowWindow($h, 9) }
    [Win32]::SetForegroundWindow($h)
    Write-Host "Focused WupmGui window"
} else {
    Write-Host "No WupmGui process"
}
