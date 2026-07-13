<#
  run-winforge driver — build / launch / screenshot either WinForge implementation.

  WinForge is a .NET (net11.0-windows) WinUI 3 desktop app. It cannot run framework-dependent
  here (no matching desktop runtime installed -> it shows a "You must install .NET" dialog), so
  this driver runs a SELF-CONTAINED publish and launches THAT exe. The app exposes deep-links
  (`WinForge.exe --page <alias>`) so any of its 319 registered module pages can be opened directly, and we
  capture the live window to a PNG via DWM bounds + Graphics.CopyFromScreen (the app is not a
  Start-menu app, so computer-use/desktop-screenshot can't target it — this self-capture is the
  reliable path).

  Usage (run from the repo root):
    powershell -ExecutionPolicy Bypass -File .agents/skills/run-winforge/driver.ps1 -Page reactor -Out shot.png
    powershell -ExecutionPolicy Bypass -File .agents/skills/run-winforge/driver.ps1 -Native -Page dashboard -Out native-dash.png -Publish
#>
param(
  [string]$Page = "dashboard",          # deep-link alias (see MainWindow.ApplyStartPage), e.g. reactor, monitor, docker
  [string]$Out  = "winforge-shot.png",  # output PNG path
  [switch]$Native,                       # build and launch the genuine C++/WinRT rewrite
  [switch]$Publish,                      # force a fresh self-contained publish
  [int]$WaitMs  = 12000,                 # ms to wait for the window to render before capturing
  [switch]$NoCapture                     # verify a dedicated launched window without foregrounding or screenshot capture
)
$ErrorActionPreference = "Stop"
# repo root = three levels up from .agents/skills/run-winforge/
$root = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path
$managedExe = Join-Path $root "bin\x64\Debug\net11.0-windows10.0.26100.0\win-x64\publish\WinForge.exe"
$nativeExe = Join-Path $root "src\WinForge.App\bin\x64\Debug\WinForge.exe"

if ($Native) {
  $exe = $nativeExe
  if ($Publish -or -not (Test-Path -LiteralPath $exe)) {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (-not (Test-Path -LiteralPath $vswhere)) {
      throw "Visual Studio Installer's vswhere.exe was not found; native WinForge requires MSVC with C++ UWP tools."
    }

    $installations = @(& $vswhere -products * -all -prerelease -format json | ConvertFrom-Json) |
      Sort-Object -Property installationVersion -Descending
    $nativeToolchain = $null
    foreach ($installation in $installations) {
      $msbuild = Join-Path $installation.installationPath "MSBuild\Current\Bin\amd64\MSBuild.exe"
      if (-not (Test-Path -LiteralPath $msbuild)) { continue }

      $vcRoot = Join-Path $installation.installationPath "MSBuild\Microsoft\VC"
      $toolsets = @(Get-ChildItem -Path $vcRoot -Directory -ErrorAction SilentlyContinue | ForEach-Object {
        Get-ChildItem -Path (Join-Path $_.FullName "Application Type\Windows Store\10.0\Platforms\x64\PlatformToolsets") -Directory -ErrorAction SilentlyContinue
      })
      if ($toolsets.Count -eq 0) { continue }

      $preferred = $toolsets | Where-Object Name -eq "v143" | Select-Object -First 1
      if (-not $preferred) { $preferred = $toolsets | Sort-Object Name -Descending | Select-Object -First 1 }
      $nativeToolchain = [pscustomobject]@{ MSBuild = $msbuild; Toolset = $preferred.Name }
      break
    }
    if (-not $nativeToolchain) {
      throw "No MSVC installation with x64 C++ UWP/WinUI build tools was found."
    }

    Write-Host "Restoring native packages with $($nativeToolchain.MSBuild) ($($nativeToolchain.Toolset))..."
    & $nativeToolchain.MSBuild (Join-Path $root "WinForge.Native.sln") /t:Restore /p:RestorePackagesConfig=true `
      /p:Configuration=Debug /p:Platform=x64 "/p:PlatformToolset=$($nativeToolchain.Toolset)" /m /v:minimal
    if ($LASTEXITCODE -ne 0) { throw "native NuGet restore failed with exit code $LASTEXITCODE" }

    Write-Host "Building the self-contained native C++ app..."
    & $nativeToolchain.MSBuild (Join-Path $root "WinForge.Native.sln") /p:Configuration=Debug /p:Platform=x64 `
      "/p:PlatformToolset=$($nativeToolchain.Toolset)" /m /v:minimal
    if ($LASTEXITCODE -ne 0) { throw "native C++ build failed with exit code $LASTEXITCODE" }
    if (-not (Test-Path -LiteralPath $exe)) { throw "native build did not produce $exe" }
  }
}
else {
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
  [DllImport("user32.dll", SetLastError=true)] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
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
  $usedPrintWindow = $false
  try {
    try {
      $g.CopyFromScreen($r.Left, $r.Top, 0, 0, (New-Object System.Drawing.Size($w, $hgt)))
    }
    catch {
      $screenCaptureError = $_.Exception.Message
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
      if (-not $printed) {
        throw "CopyFromScreen failed: $screenCaptureError. PrintWindow fallback failed with Win32 error $printError."
      }
      $usedPrintWindow = $true
      Write-Host "CopyFromScreen unavailable; captured the window through PrintWindow instead."
    }
    if ($usedPrintWindow) {
      $uniqueColors = New-Object 'System.Collections.Generic.HashSet[int]'
      # Ignore the title bar and frame. PrintWindow can render those correctly
      # while returning a blank WinUI composition surface, which is not valid
      # screenshot evidence.
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
        throw "CopyFromScreen is unavailable and the PrintWindow fallback produced a blank or near-uniform WinUI client frame; graphics capture is unavailable in this desktop session."
      }
    }
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
