param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

python -m pip install --upgrade pip
python -m pip install pyinstaller

Remove-Item -Path .\build, .\dist\app, .\dist\installer -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path .\dist\app | Out-Null

pyinstaller --clean --onefile --icon .\assets\icons\dew-main.ico --name dew-encryption .\dew_encryption\__main__.py
Copy-Item .\dist\dew-encryption.exe .\dist\app\dew-encryption.exe -Force

pyinstaller --clean --onefile --windowed --icon .\assets\icons\dew-main.ico --name dew-encryption-gui .\dew_encryption\gui_main.py
Copy-Item .\dist\dew-encryption-gui.exe .\dist\app\dew-encryption-gui.exe -Force

$iscc = Get-Command iscc -ErrorAction SilentlyContinue
if (-not $iscc) {
    throw "Inno Setup Compiler (iscc) was not found. Install Inno Setup, then rerun this script."
}

& $iscc.Source .\installer\DewEncryption.iss

Write-Host "Installer created at dist\installer\DewEncryptionSetup.exe"
