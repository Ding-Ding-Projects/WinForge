param([int]$ProcId, [string]$Out, [int]$Ticks = 6)
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class M {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, int data, UIntPtr e);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int a, out RECT r, int s);
  public struct RECT { public int Left, Top, Right, Bottom; }
}
"@
$p = Get-Process -Id $ProcId -ErrorAction Stop
$h = $p.MainWindowHandle
$r = New-Object M+RECT
[M]::DwmGetWindowAttribute($h, 9, [ref]$r, 16) | Out-Null
[M]::SetForegroundWindow($h) | Out-Null
Start-Sleep -Milliseconds 300
$cx = [int][math]::Round(($r.Left + $r.Right) * 0.5)
$cy = [int][math]::Round(($r.Top + $r.Bottom) * 0.5)
[M]::SetCursorPos($cx, $cy) | Out-Null
for ($k = 0; $k -lt $Ticks; $k++) { [M]::mouse_event(0x0800, 0, 0, -120, [UIntPtr]::Zero); Start-Sleep -Milliseconds 110 }
Start-Sleep -Milliseconds 350
# inline capture
Add-Type @"
using System;using System.Runtime.InteropServices;
public class W2 {
  [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int a, out RECT r, int s);
  public struct RECT { public int Left, Top, Right, Bottom; }
}
"@
$r2 = New-Object W2+RECT
[W2]::DwmGetWindowAttribute($h, 9, [ref]$r2, 16) | Out-Null
$w = $r2.Right - $r2.Left; $hh = $r2.Bottom - $r2.Top
$bmp = New-Object System.Drawing.Bitmap($w, $hh)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($r2.Left, $r2.Top, 0, 0, (New-Object System.Drawing.Size($w, $hh)))
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Output ("scrolled+saved {0}" -f $Out)
