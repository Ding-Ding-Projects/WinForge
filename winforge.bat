@echo off
setlocal EnableExtensions
cd /d "%~dp0"

rem Backward-compatible developer entry point. The supported packaging path is
rem build-installer.bat, which produces the Squirrel.Windows Setup.exe, RELEASES,
rem full package, and delta packages when a previous release is available.
if /I "%~1"=="installer" (
  shift
  call build-installer.bat %*
  exit /b %errorlevel%
)
if /I "%~1"=="run" (
  call build.bat %2
  exit /b %errorlevel%
)

echo WinForge build helper
echo   winforge installer [version]   Build the unsigned Squirrel.Windows installer
echo   winforge run                   Build and optionally run the published app
echo   build.bat /s                   Silent self-contained app build
echo   build-installer.bat /s         Silent Squirrel.Windows installer build
exit /b 1
