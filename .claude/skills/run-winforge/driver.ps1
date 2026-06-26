<#
  run-winforge driver — build / launch / screenshot the WinForge WinUI 3 desktop app.

  WinForge is a .NET (net11.0-windows) WinUI 3 desktop app. It cannot run framework-dependent
  here (no matching desktop runtime installed -> it shows a "You must install .NET" dialog), so
  this driver runs a SELF-CONTAINED publish and launches THAT exe. The app exposes deep-links
  (`WinForge.exe --page <alias>`) so any of its ~130 module pages can be opened directly, and we
  capture the live window to a PNG via DWM bounds + Graphics.CopyFromScreen (the app is not a
  Start-menu app, so computer-use/desktop-screenshot can't target it — this self-capture is the
  reliable path).

  Usage (run from the repo root):
    powershell -ExecutionPolicy Bypass -File .claude/skills/run-winforge/driver.ps1 -Page reactor -Out shot.png
    powershell -ExecutionPolicy Bypass -File .claude/skills/run-winforge/driver.ps1 -Page dashboard -Out dash.png -Publish
#>
param(
  [string]$Page = "dashboard",          # deep-link alias (see MainWindow.ApplyStartPage), e.g. reactor, monitor, docker
  [string]$Out  = "winforge-shot.png",  # output PNG path
  [switch]$Publish,                      # force a fresh self-contained publish
  [int]$WaitMs  = 12000                  # ms to wait for the window to render before capturing
)
$ErrorActionPreference = "Stop"
# repo root = three levels up from .claude/skills/run-winforge/
$root = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path
$tfm = "net11.0-windows10.0.26100.0"
$exeCandidates = @(
  (Join-Path $root "bin\x64\Debug\$tfm\win-x64\publish\WinForge.exe"),
  (Join-Path $root "bin\Debug\$tfm\win-x64\publish\WinForge.exe"),
  (Join-Path $root "bin\x64\Debug\$tfm\win-x64\WinForge.exe")
)
function Resolve-WinForgeExe {
  foreach ($candidate in $exeCandidates) {
    if (Test-Path $candidate) { return $candidate }
  }
  return $null
}
$exe = Resolve-WinForgeExe

if ($Publish -or -not $exe -or -not (Test-Path $exe)) {
  Write-Host "Publishing self-contained (this takes a few minutes)..."
  & dotnet publish (Join-Path $root "WinForge.csproj") -c Debug -r win-x64 --self-contained true `
      -p:Platform=x64 -p:WindowsAppSDKSelfContained=true -v quiet
  $exe = Resolve-WinForgeExe
  if (-not $exe) { throw "publish did not produce WinForge.exe in any known Debug win-x64 output path" }
}

Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class WfCap {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int n);
  [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
  [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int a, out RECT r, int s);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  public struct RECT { public int Left, Top, Right, Bottom; }
}
"@

Get-Process WinForge -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 800
Start-Process -FilePath $exe -ArgumentList "--page", $Page
Start-Sleep -Milliseconds $WaitMs

$p = Get-Process WinForge -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $p) { throw "no WinForge window appeared for page '$Page' (some pages crash on load; check %LOCALAPPDATA%\WinForge\crash.log)" }
$h = $p.MainWindowHandle
if ([WfCap]::IsIconic($h)) { [WfCap]::ShowWindow($h, 9) | Out-Null }   # SW_RESTORE
[WfCap]::SetForegroundWindow($h) | Out-Null
Start-Sleep -Milliseconds 800
$r = New-Object WfCap+RECT
if ([WfCap]::DwmGetWindowAttribute($h, 9, [ref]$r, 16) -ne 0) { [WfCap]::GetWindowRect($h, [ref]$r) | Out-Null }
$w = $r.Right - $r.Left; $hgt = $r.Bottom - $r.Top
if ($w -le 0 -or $hgt -le 0) { throw "bad window rect $w x $hgt" }
$bmp = New-Object System.Drawing.Bitmap($w, $hgt)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($r.Left, $r.Top, 0, 0, (New-Object System.Drawing.Size($w, $hgt)))
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Host ("OK page='{0}' -> {1} ({2}x{3})" -f $Page, $Out, $w, $hgt)
Get-Process WinForge -ErrorAction SilentlyContinue | Stop-Process -Force
