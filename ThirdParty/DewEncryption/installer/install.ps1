param(
    [string]$InstallRoot = "$env:LOCALAPPDATA\DewEncryption",
    [string]$RepoUrl = "https://github.com/codingmachineedge/dew-encryption.git",
    [switch]$SkipWinget,
    [switch]$NoContextMenu
)

$ErrorActionPreference = "Stop"

function Test-Command($Name) {
    return [bool](Get-Command $Name -ErrorAction SilentlyContinue)
}

function Install-WingetPackage($Id) {
    if ($SkipWinget) {
        return
    }
    if (-not (Test-Command winget)) {
        throw "winget is not available. Install Python 3.10+, Git, and 7-Zip manually, then rerun with -SkipWinget."
    }
    winget install -e --id $Id --silent --accept-source-agreements --accept-package-agreements
}

if (-not (Test-Command python)) {
    Install-WingetPackage "Python.Python.3.12"
}

if (-not (Test-Command git)) {
    Install-WingetPackage "Git.Git"
}

if (-not (Test-Command 7z)) {
    Install-WingetPackage "7zip.7zip"
}

if (-not (Test-Command VeraCrypt)) {
    Install-WingetPackage "IDRIX.VeraCrypt"
}

if (-not (Test-Command git)) {
    $env:Path = "$env:ProgramFiles\Git\cmd;$env:Path"
}

if (-not (Test-Command 7z)) {
    $env:Path = "$env:ProgramFiles\7-Zip;$env:Path"
}

if (-not (Test-Command VeraCrypt)) {
    $env:Path = "$env:ProgramFiles\VeraCrypt;$env:Path"
}

if (-not (Test-Command python)) {
    throw "Python is still not available after dependency installation."
}
if (-not (Test-Command git)) {
    throw "Git is still not available after dependency installation."
}
if (-not (Test-Command 7z)) {
    throw "7-Zip is still not available after dependency installation."
}
if (-not (Test-Command VeraCrypt)) {
    Write-Warning "VeraCrypt is not available. Git/7-Zip actions will work, but VeraCrypt encrypt/decrypt needs VeraCrypt installed."
}

New-Item -ItemType Directory -Path $InstallRoot -Force | Out-Null

if (Test-Path (Join-Path $InstallRoot ".git")) {
    git -C $InstallRoot pull --ff-only
} else {
    if ((Get-ChildItem -Path $InstallRoot -Force | Measure-Object).Count -gt 0) {
        throw "InstallRoot is not empty and is not a Git repo: $InstallRoot"
    }
    git clone $RepoUrl $InstallRoot
}

python -m pip install --upgrade pip
python -m pip install -e $InstallRoot

if (-not $NoContextMenu) {
    powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $InstallRoot "installer\install-context-menu.ps1")
}

Write-Host ""
Write-Host "Dew Encryption installed."
Write-Host "Explorer action: right-click a file or folder, then choose 'dew encryption'."
Write-Host "GUI command: dew-encryption-gui"
