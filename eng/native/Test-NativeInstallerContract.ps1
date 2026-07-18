[CmdletBinding()]
param(
    [string]$RepoRoot,
    [string]$PublishDir,
    [string]$InstallerPath,
    [string]$InstallDir
)

$ErrorActionPreference = 'Stop'

# Windows PowerShell 5.1 can evaluate parameter defaults before $PSScriptRoot
# is populated. Resolve the repository only after parameter binding so the
# same contract command works locally and on the Windows 2022 hosted runner.
if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
}
if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    throw 'Could not resolve the repository root from the installer contract script path.'
}

function Require-File {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Label
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Label is missing: $Path"
    }
}

function Require-Literal {
    param(
        [Parameter(Mandatory)][string]$Content,
        [Parameter(Mandatory)][string]$Literal,
        [Parameter(Mandatory)][string]$Label
    )

    if ($Content.IndexOf($Literal, [System.StringComparison]::Ordinal) -lt 0) {
        throw ('Native installer contract is missing ' + $Label + ': ' + $Literal)
    }
}

function Require-PeFile {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Label
    )

    Require-File -Path $Path -Label $Label
    $stream = [System.IO.File]::OpenRead($Path)
    try {
        $first = $stream.ReadByte()
        $second = $stream.ReadByte()
    }
    finally {
        $stream.Dispose()
    }

    if ($first -ne 0x4D -or $second -ne 0x5A) {
        throw "$Label is not a Windows PE executable: $Path"
    }

    if ((Get-Item -LiteralPath $Path).Length -le 0) {
        throw "$Label is empty: $Path"
    }
}

$root = [System.IO.Path]::GetFullPath($RepoRoot)
$issPath = Join-Path $root 'installer\WinForge.Native.iss'
Require-File -Path $issPath -Label 'Native Inno Setup script'
$iss = [System.IO.File]::ReadAllText($issPath, [System.Text.Encoding]::UTF8)

$requiredLiterals = @(
    '#define MyAppName "WinForge Native"',
    'AppId={{B87F4D8B-7F9E-4DB9-9E7A-5C6C8D02C9D0}',
    'PrivilegesRequired=lowest',
    'DefaultDirName={localappdata}\Programs\WinForge-Native',
    'ArchitecturesAllowed=x64compatible',
    'ArchitecturesInstallIn64BitMode=x64compatible',
    'OutputDir=out-native',
    'OutputBaseFilename=WinForge-Native-Setup',
    'Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Excludes: "*.pdb,*.ilk"',
    'Source: "..\THIRD-PARTY-NOTICES.txt"; DestDir: "{app}"',
    'Filename: "{app}\{#MyAppExe}"'
)

foreach ($literal in $requiredLiterals) {
    Require-Literal -Content $iss -Literal $literal -Label 'required installer directive'
}

if ($iss -match '(?im)^\s*PrivilegesRequired\s*=\s*admin\s*$') {
    throw 'Native installer contract must remain per-user and must not request administrator privileges.'
}

if ($PublishDir) {
    $publish = [System.IO.Path]::GetFullPath($PublishDir)
    if (-not (Test-Path -LiteralPath $publish -PathType Container)) {
        throw "Native publish directory is missing: $publish"
    }
    Require-PeFile -Path (Join-Path $publish 'WinForge.exe') -Label 'Native publish executable'
}

if ($InstallerPath) {
    $installer = [System.IO.Path]::GetFullPath($InstallerPath)
    if ([System.IO.Path]::GetFileName($installer) -ne 'WinForge-Native-Setup.exe') {
        throw "Native installer has an unexpected filename: $installer"
    }
    Require-PeFile -Path $installer -Label 'Native installer'
}

if ($InstallDir) {
    $install = [System.IO.Path]::GetFullPath($InstallDir)
    if (-not (Test-Path -LiteralPath $install -PathType Container)) {
        throw "Native install directory is missing: $install"
    }

    Require-PeFile -Path (Join-Path $install 'WinForge.exe') -Label 'Installed native executable'
    Require-PeFile -Path (Join-Path $install 'unins000.exe') -Label 'Installed native uninstaller'
    Require-File -Path (Join-Path $install 'THIRD-PARTY-NOTICES.txt') -Label 'Installed third-party notices'

    $debugArtifacts = @(Get-ChildItem -LiteralPath $install -Recurse -File |
        Where-Object { $_.Extension -in @('.pdb', '.ilk') })
    if ($debugArtifacts.Count -gt 0) {
        $names = ($debugArtifacts | ForEach-Object { $_.FullName }) -join '; '
        throw "Native installer must exclude debug artifacts: $names"
    }
}

Write-Output 'Native installer contract: PASS'
