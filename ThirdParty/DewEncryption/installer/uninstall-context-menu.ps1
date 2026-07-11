$ErrorActionPreference = "Stop"

$keys = @(
    "HKCU:\Software\Classes\*\shell\dew-encryption",
    "HKCU:\Software\Classes\Directory\shell\dew-encryption",
    "HKCU:\Software\Classes\Directory\Background\shell\dew-encryption",
    "HKCU:\Software\Classes\Directory\shell\dew-encryption-watch",
    "HKCU:\Software\Classes\Directory\Background\shell\dew-encryption-watch",
    "HKCU:\Software\Classes\Directory\shell\dew-encryption-manager",
    "HKCU:\Software\Classes\Directory\Background\shell\dew-encryption-manager",
    "HKCU:\Software\Classes\*\shell\dew-encryption-veracrypt-encrypt",
    "HKCU:\Software\Classes\Directory\shell\dew-encryption-veracrypt-encrypt",
    "HKCU:\Software\Classes\.hc\shell\dew-encryption-veracrypt-decrypt",
    "HKCU:\Software\Classes\Directory\Background\shell\dew-encryption-create-elevated-tasks",
    "HKCU:\Software\Classes\Directory\Background\shell\dew-encryption-remove-elevated-tasks",
    "HKCU:\Software\Classes\*\shell\dew-encryption-dew-drive-add",
    "HKCU:\Software\Classes\Directory\shell\dew-encryption-dew-drive-add",
    "HKCU:\Software\Classes\Directory\Background\shell\dew-encryption-dew-drive-sync",
    "HKCU:\Software\Classes\*\shell\dew-encryption-docker-upload",
    "HKCU:\Software\Classes\Directory\Background\shell\dew-encryption-docker-save-here",
    "HKCU:\Software\Classes\Directory\Background\shell\dew-encryption-git-commit-push",
    "HKCU:\Software\Classes\Directory\shell\dew-encryption-git-commit-push"
)

function Get-HkcuSubKeyPath {
    param([string]$Path)
    $prefix = "HKCU:\"
    if (-not $Path.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Only HKCU registry paths are supported: $Path"
    }
    return $Path.Substring($prefix.Length)
}

foreach ($key in $keys) {
    [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKeyTree((Get-HkcuSubKeyPath $key), $false)
}

Write-Host "Removed Explorer right-click menu entry: dew encryption"
