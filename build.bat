@echo off
setlocal EnableExtensions
cd /d "%~dp0"

set "INCOMING_SILENT=%SILENT%"
set "SILENT=0"
if /I "%~1"=="/s" set "SILENT=1"
if /I "%~1"=="--silent" set "SILENT=1"
if /I "%INCOMING_SILENT%"=="1" set "SILENT=1"
set "VERSION=%~2"

set "PS_ARGS=-NoProfile -ExecutionPolicy Bypass -File tools\build-winforge.ps1"
if not "%VERSION%"=="" set "PS_ARGS=%PS_ARGS% -Version %VERSION%"
if "%SILENT%"=="1" set "PS_ARGS=%PS_ARGS% -Silent"

echo === WinForge build: self-contained runnable x64 application ===
powershell %PS_ARGS%
if errorlevel 1 exit /b %errorlevel%

if "%SILENT%"=="1" exit /b 0
choice /C YN /N /M "Build finished. Run the published app now? [Y/N] "
if errorlevel 2 exit /b 0
start "WinForge" "%CD%\artifacts\winforge\publish\WinForge.exe"
exit /b 0
