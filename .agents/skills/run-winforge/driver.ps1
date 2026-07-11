<#
  run-winforge driver — build / launch / screenshot the WinForge WinUI 3 desktop app.

  WinForge is a .NET (net11.0-windows) WinUI 3 desktop app. It cannot run framework-dependent
  here (no matching desktop runtime installed -> it shows a "You must install .NET" dialog), so
  this driver runs a SELF-CONTAINED publish and launches THAT exe. The app exposes deep-links
  (`WinForge.exe --page <alias>`) so any of its 315 registered module pages can be opened directly, and we
  capture the live window to a PNG via DWM bounds + Graphics.CopyFromScreen (the app is not a
  Start-menu app, so computer-use/desktop-screenshot can't target it — this self-capture is the
  reliable path).

  Usage (run from the repo root):
    powershell -ExecutionPolicy Bypass -File .agents/skills/run-winforge/driver.ps1 -Page reactor -Out shot.png
    powershell -ExecutionPolicy Bypass -File .agents/skills/run-winforge/driver.ps1 -Page dashboard -Out dash.png -Publish
#>
param(
  [string]$Page = "dashboard",          # deep-link alias (see MainWindow.ApplyStartPage), e.g. reactor, monitor, docker
  [string]$Out  = "winforge-shot.png",  # output PNG path
  [switch]$Publish,                      # force a fresh self-contained publish
  [int]$WaitMs  = 12000,                 # ms to wait for the window to render before capturing
  [switch]$NoCapture                     # verify a dedicated launched window without foregrounding or screenshot capture
)
$ErrorActionPreference = "Stop"
# repo root = three levels up from .agents/skills/run-winforge/
$root = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path
$exe  = Join-Path $root "bin\x64\Debug\net11.0-windows10.0.26100.0\win-x64\publish\WinForge.exe"

# WinForge targets net11.0. Prefer the user-local .NET 11 SDK when a system
# dotnet installation is older; propagate its root so child build tools resolve
# the matching runtime as well.
$privateDotnetRoot = Join-Path $env:USERPROFILE ".dotnet"
$privateDotnetExe = Join-Path $privateDotnetRoot "dotnet.exe"
$dotnetExe = $null
if (Test-Path -LiteralPath $privateDotnetExe) {
  $privateVersion = & $privateDotnetExe --version 2>$null
  $privateExitCode = $LASTEXITCODE
  $privateVersion = @($privateVersion)[0]
  if ($privateExitCode -eq 0 -and $privateVersion -match '^11\.') {
    $dotnetExe = $privateDotnetExe
    $env:DOTNET_ROOT = $privateDotnetRoot
    if (($env:PATH -split ';') -notcontains $privateDotnetRoot) {
      $env:PATH = "$privateDotnetRoot;$env:PATH"
    }
  }
}
if (-not $dotnetExe) {
  $dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
  if (-not $dotnetCommand) {
    throw "No dotnet SDK was found. WinForge requires a .NET 11 SDK."
  }
  $dotnetExe = $dotnetCommand.Source
}
$dotnetVersion = & $dotnetExe --version 2>$null
$dotnetExitCode = $LASTEXITCODE
$dotnetVersion = @($dotnetVersion)[0]
if ($dotnetExitCode -ne 0 -or $dotnetVersion -notmatch '^11\.') {
  throw "WinForge requires a .NET 11 SDK; '$dotnetExe' reported '$dotnetVersion'. Install/select .NET 11 before running the driver."
}
Write-Host "Using .NET SDK $dotnetVersion at $dotnetExe"

# Never terminate an existing WinForge instance: this repository can be shared
# by multiple agents and a running instance may belong to another task. A
# publish that encounters a lock should fail visibly instead of killing it.

if ($Publish -or -not (Test-Path $exe)) {
  Write-Host "Publishing self-contained (this takes a few minutes)..."
  & $dotnetExe publish (Join-Path $root "WinForge.csproj") -c Debug -r win-x64 --self-contained true `
      -p:Platform=x64 -p:WindowsAppSDKSelfContained=true -v quiet
  if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }
  if (-not (Test-Path $exe)) { throw "publish did not produce $exe" }
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

$launchedProcess = $null
try {
  $launchedProcess = Start-Process -FilePath $exe -ArgumentList "--page", $Page -PassThru
  Start-Sleep -Milliseconds $WaitMs

  $p = Get-Process -Id $launchedProcess.Id -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
  if (-not $p) {
    throw "no dedicated WinForge window appeared for page '$Page'; another instance may have intercepted the launch. Close only the instance you own, raise -WaitMs, or check %LOCALAPPDATA%\WinForge\crash.log."
  }
  if ($NoCapture) {
    Write-Host ("OK launch-only page='{0}' (pid {1})" -f $Page, $p.Id)
    return
  }
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
  try {
    $g.CopyFromScreen($r.Left, $r.Top, 0, 0, (New-Object System.Drawing.Size($w, $hgt)))
    $bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
  }
  finally {
    $g.Dispose()
    $bmp.Dispose()
  }
  Write-Host ("OK page='{0}' -> {1} ({2}x{3})" -f $Page, $Out, $w, $hgt)
}
finally {
  if ($launchedProcess) {
    Get-Process -Id $launchedProcess.Id -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
  }
}
