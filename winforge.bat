@echo off
setlocal enableextensions
rem ============================================================================
rem  WinForge dev helper.
rem  Build the Inno Setup installer, or build and run the app -- locally, the
rem  same way CI does it (.github/workflows/release.yml).
rem
rem  Usage:
rem    winforge installer [version]   Publish self-contained x64 (Release) and
rem                                   build installer\out\WinForge-Setup.exe
rem    winforge run                   Build and launch the app (Debug, x64)
rem ============================================================================

rem Always operate from the repo root (this script's folder), whatever the CWD.
cd /d "%~dp0"

rem Point the .NET host at the full install. A stale user-scope DOTNET_ROOT that
rem only contains an older runtime makes the self-contained apphost fail to launch
rem ("You must install or update .NET"); forcing it here keeps "run" reliable.
if exist "%ProgramFiles%\dotnet\dotnet.exe" set "DOTNET_ROOT=%ProgramFiles%\dotnet"

set "CMD=%~1"
if /i "%CMD%"=="installer" goto installer
if /i "%CMD%"=="run" goto run
goto usage

rem ----------------------------------------------------------------- run ------
:run
echo === Building and launching WinForge (Debug, x64) ===
dotnet run --project WinForge.csproj -c Debug -p:Platform=x64
exit /b %errorlevel%

rem ----------------------------------------------------------- installer ------
:installer
set "VERSION=%~2"
if "%VERSION%"=="" set "VERSION=0.0.0-local"

echo === [1/3] Publishing app self-contained x64 (Release) ===
dotnet publish WinForge.csproj -c Release -p:Platform=x64 -r win-x64 --self-contained true -p:WindowsAppSDKSelfContained=true -p:WindowsPackageType=None -p:PublishTrimmed=false -p:PublishReadyToRun=false
if errorlevel 1 ( echo ERROR: app publish failed & exit /b 1 )

rem Resolve the publish folder (matches the installer .iss default; falls back to
rem a search in case the target-framework folder name changes).
set "PUBDIR=%CD%\bin\x64\Release\net11.0-windows10.0.26100.0\win-x64\publish"
if not exist "%PUBDIR%\WinForge.exe" (
  for /f "delims=" %%D in ('dir /b /s /ad "%CD%\bin\x64\Release" 2^>nul ^| findstr /i "\\win-x64\\publish$"') do set "PUBDIR=%%D"
)
if not exist "%PUBDIR%\WinForge.exe" ( echo ERROR: publish folder / WinForge.exe not found & exit /b 1 )
echo     publish = %PUBDIR%

echo === [2/3] Publishing launcher into the publish folder ===
dotnet publish launcher\WinForgeLauncher.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o "%PUBDIR%"
if errorlevel 1 ( echo ERROR: launcher publish failed & exit /b 1 )
if not exist "%PUBDIR%\WinForgeLauncher.exe" ( echo ERROR: WinForgeLauncher.exe not produced & exit /b 1 )

set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" ( echo ERROR: Inno Setup 6 not found at "%ISCC%".  Install it:  choco install innosetup -y & exit /b 1 )

echo === [3/3] Building Inno Setup installer ===
"%ISCC%" "/DMyAppVersion=%VERSION%" "/DMyPublishDir=%PUBDIR%" "installer\WinForge.iss"
if errorlevel 1 ( echo ERROR: ISCC failed & exit /b 1 )
echo.
echo Done. Installer: installer\out\WinForge-Setup.exe
exit /b 0

rem --------------------------------------------------------------- usage ------
:usage
echo WinForge build helper
echo   winforge installer [version]   Publish self-contained x64 + build installer\out\WinForge-Setup.exe
echo   winforge run                   Build and launch the app (Debug, x64)
exit /b 1
