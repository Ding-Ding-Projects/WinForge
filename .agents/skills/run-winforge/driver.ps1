<#
  run-winforge driver — build / launch / screenshot canonical managed WinForge.

  WinForge is a .NET (net11.0-windows) WinUI 3 desktop app. It cannot run framework-dependent
  here (no matching desktop runtime installed -> it shows a "You must install .NET" dialog), so
  this driver runs a SELF-CONTAINED publish and launches THAT exe. The app exposes deep-links
  (`WinForge.exe --page <alias>`) so any registered module page can be opened directly, and we
  capture the live WinUI visual tree to a PNG. Every launch is created on a unique, non-input
  Win32 desktop with CreateDesktop/CreateProcess; the driver never switches to that desktop, so
  neither WinForge nor a helper window can take focus from the user. The app-owned DEBUG capture
  is preferred so an overlapping desktop window can never leak into evidence; HWND-targeted
  PrintWindow on the owned off-screen desktop is a validated fallback.

  Usage (run from the repo root):
    powershell -ExecutionPolicy Bypass -File .agents/skills/run-winforge/driver.ps1 -Page reactor -Out shot.png
#>
param(
  [ValidateNotNullOrEmpty()]
  [ValidateLength(1, 128)]
  [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._:-]*$')]
  [string]$Page = "dashboard",          # deep-link key (see MainWindow.ApplyStartPage), e.g. reactor, monitor, search:Cake
  [ValidateNotNullOrEmpty()]
  [ValidateLength(1, 1024)]
  [ValidatePattern('\S')]
  [string]$Out  = "winforge-shot.png",  # output PNG path
  [switch]$Publish,                      # force a fresh self-contained publish
  [ValidateRange(1000, 120000)]
  [int]$WaitMs  = 12000,                 # ms to wait for the window to render before capturing
  [switch]$NoCapture                     # verify a dedicated launched window without foregrounding or screenshot capture
)
$ErrorActionPreference = "Stop"
# repo root = three levels up from .agents/skills/run-winforge/
$root = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path
$managedPublishRoot = Join-Path $root "artifacts\run-winforge\publish"
$managedExe = Join-Path $managedPublishRoot "WinForge.exe"
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
    # Publish to the driver's ignored artifact directory instead of the SDK's default
    # publish folder. A human-owned WinForge process may legitimately keep the latter
    # loaded; the headless capture path must never close it just to refresh its build.
    & $dotnetExe publish (Join-Path $root "WinForge.csproj") -c Debug -r win-x64 --self-contained true `
        -p:Platform=x64 -p:WindowsAppSDKSelfContained=true -o $managedPublishRoot -v quiet
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }
    if (-not (Test-Path -LiteralPath $exe)) { throw "publish did not produce $exe" }
  }

Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

public static class WfCap {
  private const uint DESKTOP_READOBJECTS = 0x0001;
  private const uint DESKTOP_CREATEWINDOW = 0x0002;
  private const uint DESKTOP_ENUMERATE = 0x0040;
  private const uint DESKTOP_WRITEOBJECTS = 0x0080;
  private const uint CREATE_NEW_PROCESS_GROUP = 0x00000200;
  private const uint CREATE_NO_WINDOW = 0x08000000;
  private const uint STARTF_USESHOWWINDOW = 0x00000001;
  private const short SW_SHOWNOACTIVATE = 4;

  [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int a, out RECT r, int s);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll", SetLastError=true)] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
  [DllImport("user32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
  private static extern IntPtr CreateDesktop(
    string desktop, string device, IntPtr devmode, uint flags, uint desiredAccess, IntPtr securityAttributes);
  [DllImport("user32.dll", SetLastError=true)]
  public static extern bool CloseDesktop(IntPtr desktop);
  [DllImport("user32.dll", SetLastError=true)]
  private static extern bool EnumDesktopWindows(IntPtr desktop, EnumDesktopWindowsProc callback, IntPtr lParam);
  [DllImport("user32.dll")]
  private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
  [DllImport("user32.dll")]
  private static extern bool IsWindowVisible(IntPtr window);
  [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
  private static extern bool CreateProcess(
    string applicationName,
    StringBuilder commandLine,
    IntPtr processAttributes,
    IntPtr threadAttributes,
    bool inheritHandles,
    uint creationFlags,
    IntPtr environment,
    string currentDirectory,
    ref STARTUPINFO startupInfo,
    out PROCESS_INFORMATION processInformation);
  [DllImport("kernel32.dll", SetLastError=true)]
  private static extern bool CloseHandle(IntPtr handle);
  [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
  public static extern bool MoveFileEx(string existingFile, string newFile, uint flags);

  private delegate bool EnumDesktopWindowsProc(IntPtr window, IntPtr lParam);

  public struct RECT { public int Left, Top, Right, Bottom; }

  [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
  private struct STARTUPINFO {
    public int cb;
    public string lpReserved;
    public string lpDesktop;
    public string lpTitle;
    public uint dwX;
    public uint dwY;
    public uint dwXSize;
    public uint dwYSize;
    public uint dwXCountChars;
    public uint dwYCountChars;
    public uint dwFillAttribute;
    public uint dwFlags;
    public short wShowWindow;
    public short cbReserved2;
    public IntPtr lpReserved2;
    public IntPtr hStdInput;
    public IntPtr hStdOutput;
    public IntPtr hStdError;
  }

  [StructLayout(LayoutKind.Sequential)]
  private struct PROCESS_INFORMATION {
    public IntPtr hProcess;
    public IntPtr hThread;
    public uint dwProcessId;
    public uint dwThreadId;
  }

  public static IntPtr CreateOffscreenDesktop(string name) {
    uint access = DESKTOP_READOBJECTS | DESKTOP_CREATEWINDOW | DESKTOP_ENUMERATE | DESKTOP_WRITEOBJECTS;
    IntPtr desktop = CreateDesktop(name, null, IntPtr.Zero, 0, access, IntPtr.Zero);
    if (desktop == IntPtr.Zero)
      throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateDesktop failed");
    return desktop;
  }

  public static Process LaunchOnDesktop(string executable, string currentDirectory, string desktopName, string page) {
    var startup = new STARTUPINFO();
    startup.cb = Marshal.SizeOf(typeof(STARTUPINFO));
    startup.lpDesktop = desktopName;
    startup.dwFlags = STARTF_USESHOWWINDOW;
    startup.wShowWindow = SW_SHOWNOACTIVATE;

    // Page is constrained by the PowerShell parameter validator. Supplying lpApplicationName
    // separately prevents command-line path ambiguity while argv[0] remains conventional.
    var commandLine = new StringBuilder("\"" + executable + "\" --page \"" + page + "\"");
    PROCESS_INFORMATION created;
    if (!CreateProcess(
      executable,
      commandLine,
      IntPtr.Zero,
      IntPtr.Zero,
      false,
      CREATE_NEW_PROCESS_GROUP | CREATE_NO_WINDOW,
      IntPtr.Zero,
      currentDirectory,
      ref startup,
      out created))
      throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateProcess on the off-screen desktop failed");

    try {
      return Process.GetProcessById((int)created.dwProcessId);
    }
    finally {
      if (created.hThread != IntPtr.Zero) CloseHandle(created.hThread);
      if (created.hProcess != IntPtr.Zero) CloseHandle(created.hProcess);
    }
  }

  public static IntPtr FindOwnedTopLevelWindow(IntPtr desktop, uint processId) {
    IntPtr best = IntPtr.Zero;
    long bestArea = 0;
    bool enumerated = EnumDesktopWindows(desktop, delegate(IntPtr window, IntPtr ignored) {
      uint owner;
      GetWindowThreadProcessId(window, out owner);
      if (owner != processId || !IsWindowVisible(window))
        return true;

      RECT rect;
      if (!GetWindowRect(window, out rect))
        return true;
      long width = Math.Max(0, (long)rect.Right - rect.Left);
      long height = Math.Max(0, (long)rect.Bottom - rect.Top);
      long area = width * height;
      if (area > bestArea) {
        best = window;
        bestArea = area;
      }
      return true;
    }, IntPtr.Zero);

    if (!enumerated)
      throw new Win32Exception(Marshal.GetLastWin32Error(), "EnumDesktopWindows failed");
    return best;
  }
}
"@

$launchedProcess = $null
$ownedProcessHandle = $null
$ownedWindowHandle = [IntPtr]::Zero
$offscreenDesktopHandle = [IntPtr]::Zero
$offscreenDesktopName = $null
$automationDataRoot = $null
$inProcessCapture = $null
$finalCaptureTemp = $null
function Test-WfCapture([string]$Path) {
  if (-not (Test-Path -LiteralPath $Path)) { return $false }
  $candidate = $null
  try {
    $candidate = [System.Drawing.Bitmap]::FromFile($Path)
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
  catch { return $false }
  finally { if ($candidate) { $candidate.Dispose() } }
}

function Remove-WfCaptureFile([string]$Path) {
  if ([string]::IsNullOrWhiteSpace($Path)) { return $true }
  for ($attempt = 0; $attempt -lt 3 -and (Test-Path -LiteralPath $Path); $attempt++) {
    Remove-Item -LiteralPath $Path -Force -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $Path) { Start-Sleep -Milliseconds 100 }
  }
  return -not (Test-Path -LiteralPath $Path)
}

function New-WfAutomationDataRoot {
  $tempParent = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
  $leaf = "WinForge.Driver.$PID.$([Guid]::NewGuid().ToString('N'))"
  $candidate = Join-Path $tempParent $leaf
  [System.IO.Directory]::CreateDirectory($candidate) | Out-Null
  return [System.IO.Path]::GetFullPath($candidate)
}

function Remove-WfAutomationDataRoot([string]$Path) {
  if ([string]::IsNullOrWhiteSpace($Path)) { return $true }

  try {
    $full = [System.IO.Path]::GetFullPath($Path)
    $tempParent = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd(
      [System.IO.Path]::DirectorySeparatorChar,
      [System.IO.Path]::AltDirectorySeparatorChar)
    $parent = [System.IO.Path]::GetDirectoryName($full).TrimEnd(
      [System.IO.Path]::DirectorySeparatorChar,
      [System.IO.Path]::AltDirectorySeparatorChar)
    $leaf = [System.IO.Path]::GetFileName($full)
    if (-not [string]::Equals($parent, $tempParent, [StringComparison]::OrdinalIgnoreCase) -or
        $leaf -notmatch '^WinForge\.Driver\.\d+\.[0-9a-f]{32}$') {
      Write-Warning "Refused to remove an automation data path that is not the exact driver-owned temp directory."
      return $false
    }
    if (-not (Test-Path -LiteralPath $full)) { return $true }

    $item = Get-Item -LiteralPath $full -Force
    if (-not $item.PSIsContainer -or ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint)) {
      Write-Warning "Refused recursive cleanup because the driver-owned automation path is not a normal directory."
      return $false
    }

    for ($attempt = 0; $attempt -lt 3 -and (Test-Path -LiteralPath $full); $attempt++) {
      Remove-Item -LiteralPath $full -Recurse -Force -ErrorAction SilentlyContinue
      if (Test-Path -LiteralPath $full) { Start-Sleep -Milliseconds 150 }
    }
    return -not (Test-Path -LiteralPath $full)
  }
  catch {
    Write-Warning "Could not validate or remove the driver-owned automation data directory: $($_.Exception.Message)"
    return $false
  }
}

function Publish-WfCapture([string]$SourcePath, [string]$DestinationPath) {
  if (-not (Test-WfCapture $SourcePath)) {
    throw "capture promotion rejected an invalid source image."
  }

  $sourceFull = [System.IO.Path]::GetFullPath($SourcePath)
  $destinationFull = [System.IO.Path]::GetFullPath($DestinationPath)
  $sourceDirectory = [System.IO.Path]::GetDirectoryName($sourceFull)
  $destinationDirectory = [System.IO.Path]::GetDirectoryName($destinationFull)
  if (-not [string]::Equals($sourceDirectory, $destinationDirectory, [StringComparison]::OrdinalIgnoreCase)) {
    throw "capture promotion requires a same-directory temporary image."
  }

  # MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH. The requested evidence path
  # changes only after a fully written, validated PNG is ready in the same directory.
  if (-not [WfCap]::MoveFileEx($sourceFull, $destinationFull, 0x9)) {
    $moveError = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
    throw "atomic capture promotion failed with Win32 error $moveError."
  }

  if (-not (Test-WfCapture $destinationFull)) {
    if (-not (Remove-WfCaptureFile $destinationFull)) {
      Write-Warning "Could not remove an invalid atomically promoted capture."
    }
    throw "atomic capture promotion did not produce a valid image."
  }
}

try {
  if (-not $NoCapture) {
    $outFull = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Out)
    if ($outFull.Length -gt 1024 -or [System.IO.Path]::GetExtension($outFull) -ine ".png") {
      throw "capture output must be a local .png path no longer than 1024 characters."
    }
    if ($outFull.StartsWith("\\", [StringComparison]::Ordinal)) {
      throw "capture output must stay on a local drive; UNC paths are not accepted."
    }
    $outRoot = [System.IO.Path]::GetPathRoot($outFull)
    $outDriveType = if ($outRoot) { (New-Object System.IO.DriveInfo($outRoot)).DriveType } else { $null }
    if (-not $outRoot -or $outDriveType -notin @([System.IO.DriveType]::Fixed, [System.IO.DriveType]::Removable)) {
      throw "capture output must stay on a fixed or removable local drive."
    }
    $outDirectory = Split-Path -Parent $outFull
    if ($outDirectory) { New-Item -ItemType Directory -Path $outDirectory -Force | Out-Null }
    if (Test-Path -LiteralPath $outFull) {
      $existingOutput = Get-Item -LiteralPath $outFull -Force
      if ($existingOutput.PSIsContainer) { throw "capture output '$outFull' is a directory." }
      # Once a capture attempt begins, an older image at the requested path must not
      # survive a failed run and be mistaken for current evidence.
      Remove-Item -LiteralPath $outFull -Force
    }
    $inProcessCapture = "$outFull.winui-$([Guid]::NewGuid().ToString('N')).png"
  }

  # The desktop is never made the input desktop and this thread is never attached to it.
  # WinForge may activate normally inside its own desktop, but that activation cannot move
  # keyboard focus or place a window over the user's interactive session.
  $offscreenDesktopName = "WinForge.Driver.$PID.$([Guid]::NewGuid().ToString('N'))"
  $offscreenDesktopHandle = [WfCap]::CreateOffscreenDesktop($offscreenDesktopName)
  $automationDataRoot = New-WfAutomationDataRoot

  $oldCapturePath = [Environment]::GetEnvironmentVariable("WINFORGE_CAPTURE_PATH", "Process")
  $oldCaptureDelay = [Environment]::GetEnvironmentVariable("WINFORGE_CAPTURE_DELAY_MS", "Process")
  $oldAutomationDataRoot = [Environment]::GetEnvironmentVariable("WINFORGE_AUTOMATION_DATA_ROOT", "Process")
  try {
    if ($NoCapture) {
      [Environment]::SetEnvironmentVariable("WINFORGE_CAPTURE_PATH", $null, "Process")
      [Environment]::SetEnvironmentVariable("WINFORGE_CAPTURE_DELAY_MS", $null, "Process")
    }
    else {
      [Environment]::SetEnvironmentVariable("WINFORGE_CAPTURE_PATH", $inProcessCapture, "Process")
      [Environment]::SetEnvironmentVariable(
        "WINFORGE_CAPTURE_DELAY_MS",
        [Math]::Max(3000, [Math]::Min(10000, [int]($WaitMs / 3))).ToString(),
        "Process")
    }
    [Environment]::SetEnvironmentVariable("WINFORGE_AUTOMATION_DATA_ROOT", $automationDataRoot, "Process")
    $launchedProcess = [WfCap]::LaunchOnDesktop($exe, $root, $offscreenDesktopName, $Page)
  }
  finally {
    [Environment]::SetEnvironmentVariable("WINFORGE_CAPTURE_PATH", $oldCapturePath, "Process")
    [Environment]::SetEnvironmentVariable("WINFORGE_CAPTURE_DELAY_MS", $oldCaptureDelay, "Process")
    [Environment]::SetEnvironmentVariable("WINFORGE_AUTOMATION_DATA_ROOT", $oldAutomationDataRoot, "Process")
  }

  # Open and retain the native process handle immediately. Cleanup therefore remains
  # bound to this launch even if the numeric PID is recycled later.
  $ownedProcessHandle = $launchedProcess.SafeHandle
  Start-Sleep -Milliseconds $WaitMs

  $p = $null
  try {
    $launchedProcess.Refresh()
    if (-not $launchedProcess.HasExited) {
      $ownedWindowHandle = [WfCap]::FindOwnedTopLevelWindow(
        $offscreenDesktopHandle,
        [uint32]$launchedProcess.Id)
    }
    if (-not $launchedProcess.HasExited -and $ownedWindowHandle -ne [IntPtr]::Zero) {
      $p = $launchedProcess
    }
  }
  catch { }
  if (-not $p) {
    throw "no owned WinForge window appeared on the isolated desktop for page '$Page'; raise -WaitMs or run the relevant managed smoke test. The driver will not fall back to the interactive desktop."
  }
  if ($NoCapture) {
    Write-Host ("OK off-screen launch-only page='{0}' (pid {1})" -f $Page, $p.Id)
    return
  }

  # Prefer the live app-owned visual tree. CopyFromScreen is intentionally never used:
  # a foreground-denied or overlapped window can otherwise capture an unrelated app.
  if (Test-WfCapture $inProcessCapture) {
    Publish-WfCapture $inProcessCapture $outFull
    $liveCapture = [System.Drawing.Image]::FromFile($outFull)
    try { $w = $liveCapture.Width; $hgt = $liveCapture.Height }
    finally { $liveCapture.Dispose() }
    Write-Host "Used the live in-process WinUI visual-tree capture."
    Write-Host ("OK off-screen page='{0}' -> {1} ({2}x{3})" -f $Page, $outFull, $w, $hgt)
    return
  }

  $h = $ownedWindowHandle
  $r = New-Object WfCap+RECT
  if ([WfCap]::DwmGetWindowAttribute($h, 9, [ref]$r, 16) -ne 0) { [WfCap]::GetWindowRect($h, [ref]$r) | Out-Null }
  $w = $r.Right - $r.Left; $hgt = $r.Bottom - $r.Top
  if ($w -le 0 -or $hgt -le 0) { throw "bad window rect $w x $hgt" }
  $finalCaptureTemp = "$outFull.driver-$([Guid]::NewGuid().ToString('N')).partial.png"
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
    $bmp.Save($finalCaptureTemp, [System.Drawing.Imaging.ImageFormat]::Png)
  }
  catch {
    $desktopCaptureError = $_.Exception.Message
  }
  finally {
    $g.Dispose()
    $bmp.Dispose()
  }

  if (-not $desktopCaptureError) {
    try {
      Publish-WfCapture $finalCaptureTemp $outFull
      $finalCaptureTemp = $null
    }
    catch {
      $desktopCaptureError = $_.Exception.Message
    }
  }

  if ($desktopCaptureError) {
    if (-not (Test-WfCapture $inProcessCapture)) {
      throw "$desktopCaptureError The in-process WinUI capture did not produce a valid frame."
    }
    Publish-WfCapture $inProcessCapture $outFull
    $fallback = [System.Drawing.Image]::FromFile($outFull)
    try { $w = $fallback.Width; $hgt = $fallback.Height }
    finally { $fallback.Dispose() }
    Write-Host "PrintWindow unavailable; used the late live in-process WinUI visual-tree capture."
  }
  Write-Host ("OK off-screen page='{0}' -> {1} ({2}x{3})" -f $Page, $outFull, $w, $hgt)
}
finally {
  if ($launchedProcess) {
    # Keep the original Process handle: resolving only by PID after a long capture can
    # terminate an unrelated process if Windows has already reused the number.
    try {
      $launchedProcess.Refresh()
      if (-not $launchedProcess.HasExited) {
        $launchedProcess.Kill()
        if (-not $launchedProcess.WaitForExit(5000)) {
          Write-Warning "The owned WinForge process did not exit within five seconds of cleanup."
        }
      }
    }
    catch { Write-Warning "Could not complete owned-process cleanup: $($_.Exception.Message)" }
    finally { $launchedProcess.Dispose() }
  }
  if ($offscreenDesktopHandle -ne [IntPtr]::Zero) {
    if (-not [WfCap]::CloseDesktop($offscreenDesktopHandle)) {
      $closeDesktopError = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
      Write-Warning "Could not close the owned off-screen desktop handle (Win32 error $closeDesktopError)."
    }
    $offscreenDesktopHandle = [IntPtr]::Zero
  }
  if (-not (Remove-WfCaptureFile $inProcessCapture)) {
    Write-Warning "Could not remove the unique in-process capture temporary file '$inProcessCapture'."
  }
  if (-not (Remove-WfCaptureFile $finalCaptureTemp)) {
    Write-Warning "Could not remove the unique final-promotion temporary file '$finalCaptureTemp'."
  }
  if (-not (Remove-WfAutomationDataRoot $automationDataRoot)) {
    Write-Warning "Could not remove the exact driver-owned automation data directory '$automationDataRoot'."
  }
}
