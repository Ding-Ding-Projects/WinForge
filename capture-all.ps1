param([string]$Exe, [string]$Aliases, [string]$OutDir, [int]$WaitMs = 10000)
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Cap {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int n);
  [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
  [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int a, out RECT r, int s);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  public struct RECT { public int Left, Top, Right, Bottom; }
}
"@
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$rows = Get-Content $Aliases
$ok = 0; $fail = 0
foreach ($line in $rows) {
  if (-not $line.Trim()) { continue }
  $alias = ($line -split "`t")[0].Trim()
  if (-not $alias) { continue }
  Get-Process WinForge -ErrorAction SilentlyContinue | Stop-Process -Force
  Start-Sleep -Milliseconds 600
  try { Start-Process -FilePath $Exe -ArgumentList "--page", $alias } catch { Write-Output "LAUNCHFAIL $alias"; $fail++; continue }
  Start-Sleep -Milliseconds $WaitMs
  $p = Get-Process WinForge -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
  if (-not $p) { Write-Output "NOWIN $alias"; $fail++; continue }
  $h = $p.MainWindowHandle
  if ([Cap]::IsIconic($h)) { [Cap]::ShowWindow($h, 9) | Out-Null }
  [Cap]::SetForegroundWindow($h) | Out-Null
  Start-Sleep -Milliseconds 800
  $r = New-Object Cap+RECT
  if ([Cap]::DwmGetWindowAttribute($h, 9, [ref]$r, 16) -ne 0) { [Cap]::GetWindowRect($h, [ref]$r) | Out-Null }
  $w = $r.Right - $r.Left; $hgt = $r.Bottom - $r.Top
  if ($w -le 0 -or $hgt -le 0) { Write-Output "BADRECT $alias"; $fail++; continue }
  try {
    $bmp = New-Object System.Drawing.Bitmap($w, $hgt)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($r.Left, $r.Top, 0, 0, (New-Object System.Drawing.Size($w, $hgt)))
    $out = Join-Path $OutDir ("screenshot-" + $alias + ".png")
    $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    Write-Output ("OK {0} ({1}x{2})" -f $alias, $w, $hgt); $ok++
  } catch { Write-Output "CAPFAIL $alias $_"; $fail++ }
}
Get-Process WinForge -ErrorAction SilentlyContinue | Stop-Process -Force
Write-Output ("DONE ok=$ok fail=$fail")
