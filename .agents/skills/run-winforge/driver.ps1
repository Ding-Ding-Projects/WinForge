<#
  run-winforge driver — build / launch / screenshot canonical managed WinForge.

  WinForge is a .NET (net11.0-windows) WinUI 3 desktop app. It cannot run framework-dependent
  here (no matching desktop runtime installed -> it shows a "You must install .NET" dialog), so
  this driver runs a SELF-CONTAINED publish and launches THAT exe. The app exposes deep-links
  (`WinForge.exe --page <alias>`) so any of its 319 registered module pages can be opened directly, and we
  capture the live WinUI visual tree to a PNG. The app-owned DEBUG capture is preferred so an
  overlapping desktop window can never leak into evidence; HWND-targeted PrintWindow is a
  validated fallback.

  Usage (run from the repo root):
    powershell -ExecutionPolicy Bypass -File .agents/skills/run-winforge/driver.ps1 -Page reactor -Out shot.png
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
$managedExe = Join-Path $root "bin\x64\Debug\net11.0-windows10.0.26100.0\win-x64\publish\WinForge.exe"
$exe = $managedExe
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
      throw "No dotnet SDK was found. Managed WinForge requires a .NET 11 SDK."
    }
    $dotnetExe = $dotnetCommand.Source
  }
  $dotnetVersion = & $dotnetExe --version 2>$null
  $dotnetExitCode = $LASTEXITCODE
  $dotnetVersion = @($dotnetVersion)[0]
  if ($dotnetExitCode -ne 0 -or $dotnetVersion -notmatch '^11\.') {
    throw "Managed WinForge requires a .NET 11 SDK; '$dotnetExe' reported '$dotnetVersion'. Install/select .NET 11 before running the driver."
  }
  Write-Host "Using .NET SDK $dotnetVersion at $dotnetExe"

  if ($Publish -or -not (Test-Path -LiteralPath $exe)) {
    Write-Host "Publishing managed WinForge self-contained (this takes a few minutes)..."
    & $dotnetExe publish (Join-Path $root "WinForge.csproj") -c Debug -r win-x64 --self-contained true `
        -p:Platform=x64 -p:WindowsAppSDKSelfContained=true -v quiet
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }
    if (-not (Test-Path -LiteralPath $exe)) { throw "publish did not produce $exe" }
  }

Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class WfCap {
  [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int a, out RECT r, int s);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll", SetLastError=true)] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
  public struct RECT { public int Left, Top, Right, Bottom; }
}
"@

$launchedProcess = $null
$inProcessCapture = $null
function Test-WfCapture([string]$Path) {
  if (-not (Test-Path -LiteralPath $Path)) { return $false }
  $candidate = [System.Drawing.Bitmap]::FromFile($Path)
  try {
    if ($candidate.Width -lt 100 -or $candidate.Height -lt 100) { return $false }
    $colors = New-Object 'System.Collections.Generic.HashSet[int]'
    $stepX = [Math]::Max(1, [int][Math]::Floor($candidate.Width / 24))
    $stepY = [Math]::Max(1, [int][Math]::Floor($candidate.Height / 24))
    for ($y = 0; $y -lt $candidate.Height; $y += $stepY) {
      for ($x = 0; $x -lt $candidate.Width; $x += $stepX) {
        $colors.Add($candidate.GetPixel($x, $y).ToArgb()) | Out-Null
      }
    }
    return $colors.Count -ge 4
  }
  finally { $candidate.Dispose() }
}

try {
  if (-not $NoCapture) {
    $outFull = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Out)
    $outDirectory = Split-Path -Parent $outFull
    if ($outDirectory) { New-Item -ItemType Directory -Path $outDirectory -Force | Out-Null }
    $inProcessCapture = "$outFull.winui-$([Guid]::NewGuid().ToString('N')).png"
    $oldCapturePath = [Environment]::GetEnvironmentVariable("WINFORGE_CAPTURE_PATH", "Process")
    $oldCaptureDelay = [Environment]::GetEnvironmentVariable("WINFORGE_CAPTURE_DELAY_MS", "Process")
    try {
      [Environment]::SetEnvironmentVariable("WINFORGE_CAPTURE_PATH", $inProcessCapture, "Process")
      [Environment]::SetEnvironmentVariable(
        "WINFORGE_CAPTURE_DELAY_MS",
        [Math]::Max(3000, [Math]::Min(10000, [int]($WaitMs / 3))).ToString(),
        "Process")
      $launchedProcess = Start-Process -FilePath $exe -ArgumentList "--page", $Page -PassThru
    }
    finally {
      [Environment]::SetEnvironmentVariable("WINFORGE_CAPTURE_PATH", $oldCapturePath, "Process")
      [Environment]::SetEnvironmentVariable("WINFORGE_CAPTURE_DELAY_MS", $oldCaptureDelay, "Process")
    }
  }
  else {
    $launchedProcess = Start-Process -FilePath $exe -ArgumentList "--page", $Page -PassThru
  }
  Start-Sleep -Milliseconds $WaitMs

  $p = $null
  try {
    $launchedProcess.Refresh()
    if (-not $launchedProcess.HasExited -and $launchedProcess.MainWindowHandle -ne 0) {
      $p = $launchedProcess
    }
  }
  catch { }
  if (-not $p) {
    throw "no dedicated WinForge window appeared for page '$Page'; another instance may have intercepted the launch. Close only the instance you own, raise -WaitMs, or check %LOCALAPPDATA%\WinForge\crash.log."
  }
  if ($NoCapture) {
    Write-Host ("OK launch-only page='{0}' (pid {1})" -f $Page, $p.Id)
    return
  }

  # Prefer the live app-owned visual tree. CopyFromScreen is intentionally never used:
  # a foreground-denied or overlapped window can otherwise capture an unrelated app.
  if (Test-WfCapture $inProcessCapture) {
    Copy-Item -LiteralPath $inProcessCapture -Destination $outFull -Force
    $liveCapture = [System.Drawing.Image]::FromFile($outFull)
    try { $w = $liveCapture.Width; $hgt = $liveCapture.Height }
    finally { $liveCapture.Dispose() }
    Write-Host "Used the live in-process WinUI visual-tree capture."
    Write-Host ("OK page='{0}' -> {1} ({2}x{3})" -f $Page, $outFull, $w, $hgt)
    return
  }

  $h = $p.MainWindowHandle
  $r = New-Object WfCap+RECT
  if ([WfCap]::DwmGetWindowAttribute($h, 9, [ref]$r, 16) -ne 0) { [WfCap]::GetWindowRect($h, [ref]$r) | Out-Null }
  $w = $r.Right - $r.Left; $hgt = $r.Bottom - $r.Top
  if ($w -le 0 -or $hgt -le 0) { throw "bad window rect $w x $hgt" }
  $bmp = New-Object System.Drawing.Bitmap($w, $hgt)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $desktopCaptureError = $null
  try {
    $dc = $g.GetHdc()
    $printed = $false
    $printError = 0
    try {
      $printed = [WfCap]::PrintWindow($h, $dc, 2) # PW_RENDERFULLCONTENT
      if (-not $printed) { $printError = [Runtime.InteropServices.Marshal]::GetLastWin32Error() }
    }
    finally {
      $g.ReleaseHdc($dc)
    }
    if (-not $printed) { throw "PrintWindow failed with Win32 error $printError." }

    $uniqueColors = New-Object 'System.Collections.Generic.HashSet[int]'
    # Ignore the title bar and frame. PrintWindow can render those correctly while
    # returning a blank WinUI composition surface, which is not valid evidence.
    $left = [Math]::Min($w - 1, [Math]::Max(8, [int]($w / 40)))
    $top = [Math]::Min($hgt - 1, [Math]::Max(56, [int]($hgt / 14)))
    $right = [Math]::Max($left + 1, $w - $left)
    $bottom = [Math]::Max($top + 1, $hgt - $left)
    $stepX = [Math]::Max(1, [int][Math]::Floor(($right - $left) / 24))
    $stepY = [Math]::Max(1, [int][Math]::Floor(($bottom - $top) / 24))
    for ($y = $top; $y -lt $bottom; $y += $stepY) {
      for ($x = $left; $x -lt $right; $x += $stepX) {
        $uniqueColors.Add($bmp.GetPixel($x, $y).ToArgb()) | Out-Null
      }
    }
    if ($uniqueColors.Count -lt 4) {
      throw "PrintWindow produced a blank or near-uniform WinUI client frame."
    }
    $bmp.Save($outFull, [System.Drawing.Imaging.ImageFormat]::Png)
  }
  catch {
    $desktopCaptureError = $_.Exception.Message
  }
  finally {
    $g.Dispose()
    $bmp.Dispose()
  }

  if ($desktopCaptureError) {
    if (-not (Test-WfCapture $inProcessCapture)) {
      throw "$desktopCaptureError The in-process WinUI capture did not produce a valid frame."
    }
    Copy-Item -LiteralPath $inProcessCapture -Destination $outFull -Force
    $fallback = [System.Drawing.Image]::FromFile($outFull)
    try { $w = $fallback.Width; $hgt = $fallback.Height }
    finally { $fallback.Dispose() }
    Write-Host "PrintWindow unavailable; used the late live in-process WinUI visual-tree capture."
  }
  Write-Host ("OK page='{0}' -> {1} ({2}x{3})" -f $Page, $outFull, $w, $hgt)
}
finally {
  if ($launchedProcess) {
    # Keep the original Process handle: resolving only by PID after a long capture can
    # terminate an unrelated process if Windows has already reused the number.
    try {
      $launchedProcess.Refresh()
      if (-not $launchedProcess.HasExited) { $launchedProcess.Kill() }
    }
    catch { }
    finally { $launchedProcess.Dispose() }
  }
  if ($inProcessCapture -and (Test-Path -LiteralPath $inProcessCapture)) {
    Remove-Item -LiteralPath $inProcessCapture -Force -ErrorAction SilentlyContinue
  }
}
