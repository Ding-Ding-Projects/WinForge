param(
    [switch]$NoExplorerRestart
)

$ErrorActionPreference = "Stop"

$packages = Get-AppxPackage -Name "CodingMachineEdge.DewEncryption.ContextMenu" -ErrorAction SilentlyContinue
foreach ($package in $packages) {
    Remove-AppxPackage -Package $package.PackageFullName
}

$configKey = "HKCU:\Software\DewEncryption\ContextMenu"
if (Test-Path $configKey) {
    Remove-Item -Path $configKey -Recurse -Force
}

if (-not $NoExplorerRestart) {
    Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
    Start-Process explorer.exe
}

Write-Host "Removed Windows 11 Explorer context menu package: Dew Encryption"
