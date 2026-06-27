param([int]$ProcId, [string]$Out)
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class W {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int n);
  [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
  [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int a, out RECT r, int s);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  public struct RECT { public int Left, Top, Right, Bottom; }
}
"@
$p = Get-Process -Id $ProcId -ErrorAction Stop
$h = $p.MainWindowHandle
if ($h -eq 0) { Write-Output "no-hwnd"; exit 1 }
if ([W]::IsIconic($h)) { [W]::ShowWindow($h, 9) | Out-Null }   # SW_RESTORE
[W]::SetForegroundWindow($h) | Out-Null
Start-Sleep -Milliseconds 900
$r = New-Object W+RECT
# DWMWA_EXTENDED_FRAME_BOUNDS = 9 (accurate visible bounds, excludes shadow)
$ok = [W]::DwmGetWindowAttribute($h, 9, [ref]$r, 16)
if ($ok -ne 0) { [W]::GetWindowRect($h, [ref]$r) | Out-Null }
$w = $r.Right - $r.Left; $hgt = $r.Bottom - $r.Top
if ($w -le 0 -or $hgt -le 0) { Write-Output "bad-rect $w x $hgt"; exit 1 }
$bmp = New-Object System.Drawing.Bitmap($w, $hgt)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($r.Left, $r.Top, 0, 0, (New-Object System.Drawing.Size($w, $hgt)))
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Output ("saved {0} ({1}x{2})" -f $Out, $w, $hgt)
