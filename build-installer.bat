@echo off
setlocal EnableExtensions
cd /d "%~dp0"

set "INCOMING_SILENT=%SILENT%"
set "SILENT=0"
if /I "%~1"=="/s" set "SILENT=1"
if /I "%~1"=="--silent" set "SILENT=1"
if /I "%INCOMING_SILENT%"=="1" set "SILENT=1"
set "VERSION=%~2"

echo === WinForge installer build: Squirrel.Windows ===
set "BUILD_ARGS=-NoProfile -ExecutionPolicy Bypass -File tools\build-winforge.ps1 -Installer"
if not "%VERSION%"=="" set "BUILD_ARGS=%BUILD_ARGS% -Version %VERSION%"
if "%SILENT%"=="1" set "BUILD_ARGS=%BUILD_ARGS% -Silent"
powershell %BUILD_ARGS%
if errorlevel 1 exit /b %errorlevel%

echo.
echo Squirrel output: release-artifacts\Setup.exe
echo Update metadata: release-artifacts\RELEASES
echo Full package: release-artifacts\WinForge-*.nupkg
echo The installer is unsigned and may trigger an unknown-publisher or SmartScreen warning.
exit /b 0
