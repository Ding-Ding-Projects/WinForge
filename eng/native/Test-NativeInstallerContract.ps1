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

function Require-Regex {
    param(
        [Parameter(Mandatory)][string]$Content,
        [Parameter(Mandatory)][string]$Pattern,
        [Parameter(Mandatory)][string]$Label
    )

    if (-not [System.Text.RegularExpressions.Regex]::IsMatch(
            $Content,
            $Pattern,
            [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)) {
        throw ('Native installer contract is missing ' + $Label + ': ' + $Pattern)
    }
}

function Reject-Regex {
    param(
        [Parameter(Mandatory)][string]$Content,
        [Parameter(Mandatory)][string]$Pattern,
        [Parameter(Mandatory)][string]$Label
    )

    if ([System.Text.RegularExpressions.Regex]::IsMatch(
            $Content,
            $Pattern,
            [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)) {
        throw ('Native installer contract rejects ' + $Label + ': ' + $Pattern)
    }
}

function Get-WorkflowStep {
    param(
        [Parameter(Mandatory)][string]$Content,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Label
    )

    $escapedName = [System.Text.RegularExpressions.Regex]::Escape($Name)
    $pattern = '(?ms)^[ ]{6}- name: ' + $escapedName + '[^\r\n]*\r?\n(?<body>.*?)(?=^[ ]{6}- name:|\z)'
    $match = [System.Text.RegularExpressions.Regex]::Match(
        $Content,
        $pattern,
        [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
    if (-not $match.Success) {
        throw "Native installer contract could not resolve $Label workflow step: $Name"
    }
    return $match.Groups['body'].Value
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

$nativeWorkflowPath = Join-Path $root '.github\workflows\native-release.yml'
$managedWorkflowPath = Join-Path $root '.github\workflows\release.yml'
$siteDataWorkflowPath = Join-Path $root '.github\workflows\site-data.yml'
Require-File -Path $nativeWorkflowPath -Label 'Native release workflow'
Require-File -Path $managedWorkflowPath -Label 'Managed release workflow'
Require-File -Path $siteDataWorkflowPath -Label 'Site-data workflow'
$nativeWorkflow = [System.IO.File]::ReadAllText($nativeWorkflowPath, [System.Text.Encoding]::UTF8).Replace("`r`n", "`n").Replace("`r", "`n")
$managedWorkflow = [System.IO.File]::ReadAllText($managedWorkflowPath, [System.Text.Encoding]::UTF8).Replace("`r`n", "`n").Replace("`r", "`n")
$siteDataWorkflow = [System.IO.File]::ReadAllText($siteDataWorkflowPath, [System.Text.Encoding]::UTF8).Replace("`r`n", "`n").Replace("`r", "`n")

Require-Regex -Content $nativeWorkflow -Pattern '(?m)^on:\r?\n  push:\r?\n  pull_request:\r?\n    branches: \[ main \]\r?\n  workflow_dispatch:[ \t]*$' -Label 'unfiltered every-push native trigger'
Require-Regex -Content $nativeWorkflow -Pattern '(?m)^      source_sha:\r?\n        description:.*\r?\n        required: false\r?\n        default: ''''\r?\n        type: string[ \t]*$' -Label 'optional exact source_sha dispatch input'

$resolveSourceStep = Get-WorkflowStep -Content $nativeWorkflow -Name 'Resolve immutable source commit' -Label 'native source resolver'
Require-Regex -Content $resolveSourceStep -Pattern '(?m)^        id: source[ \t]*$' -Label 'native source output step ID'
Require-Regex -Content $resolveSourceStep -Pattern '(?m)^          EVENT_SOURCE_SHA: \$\{\{ github\.sha \}\}[ \t]*$' -Label 'direct-push event SHA wiring'
Require-Regex -Content $resolveSourceStep -Pattern '(?m)^          REQUESTED_SOURCE_SHA: \$\{\{ inputs\.source_sha \}\}[ \t]*$' -Label 'dispatched source SHA wiring'
Require-Regex -Content $resolveSourceStep -Pattern '(?m)^          "sha=\$expectedSha" \| Out-File -FilePath \$env:GITHUB_OUTPUT -Append -Encoding utf8[ \t]*$' -Label 'resolved source SHA output'

$checkoutStep = Get-WorkflowStep -Content $nativeWorkflow -Name 'Checkout' -Label 'native checkout'
Require-Regex -Content $checkoutStep -Pattern '(?m)^          fetch-depth: 0[ \t]*$' -Label 'full native source history checkout'
Require-Regex -Content $checkoutStep -Pattern '(?m)^          ref: \$\{\{ steps\.source\.outputs\.sha \}\}[ \t]*$' -Label 'exact native source checkout'

$verifySourceStep = Get-WorkflowStep -Content $nativeWorkflow -Name 'Verify immutable source commit' -Label 'native source verifier'
Require-Regex -Content $verifySourceStep -Pattern '(?m)^          \$actualSha = \(git rev-parse HEAD\)\.Trim\(\)\.ToLowerInvariant\(\)[ \t]*$' -Label 'checked-out SHA verification'
Require-Regex -Content $verifySourceStep -Pattern '(?m)^          git cat-file -e "\$expectedSha\^\{commit\}"[ \t]*$' -Label 'source commit object verification'
Require-Regex -Content $verifySourceStep -Pattern '(?m)^            git fetch --no-tags origin ''\+refs/heads/main:refs/remotes/origin/main''[ \t]*$' -Label 'origin/main reachability fetch'
Require-Regex -Content $verifySourceStep -Pattern '(?m)^            git merge-base --is-ancestor \$expectedSha refs/remotes/origin/main[ \t]*$' -Label 'dispatched source main-ancestor verification'

$nativeReleaseStep = Get-WorkflowStep -Content $nativeWorkflow -Name 'Create native GitHub release' -Label 'native release creation'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^        if: \(github\.event_name == ''push'' && github\.ref_type == ''branch''\) \|\| \(github\.event_name == ''push'' && startsWith\(github\.ref, ''refs/tags/native-v''\)\) \|\| \(github\.event_name == ''workflow_dispatch'' && inputs\.publish_release == true\)[ \t]*$' -Label 'branch-push/native-tag/manual release condition'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^          EXPECTED_SOURCE_SHA: \$\{\{ steps\.source\.outputs\.sha \}\}[ \t]*$' -Label 'native release expected SHA environment'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^          \$usePushedNativeTag = \$env:EVENT_NAME -eq ''push'' -and[ \t]*$' -Label 'native-tag event discrimination'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^            \(\[string\]\$env:EVENT_REF_NAME\)\.StartsWith\(''native-v'', \[StringComparison\]::Ordinal\)[ \t]*$' -Label 'native-only pushed tag acceptance'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^          gh release create \$tag\b[^\r\n]*--target "\$expectedSha"[^\r\n]*--prerelease[ \t]*$' -Label 'native release command immutable target'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^          \$refJson = \(& gh api "repos/\$env:GITHUB_REPOSITORY/git/ref/tags/\$tag" \| Out-String\)[ \t]*$' -Label 'native created-tag lookup'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^          for \(\$depth = 0; \$object\.type -eq ''tag''; \$depth\+\+\) \{[ \t]*$' -Label 'native annotated-tag dereference'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^          if \(\$actualSha -ne \$expectedSha\) \{[ \t]*$' -Label 'native exact tag provenance assertion'

$managedReleaseStep = Get-WorkflowStep -Content $managedWorkflow -Name 'Create GitHub Release' -Label 'managed release creation'
Require-Regex -Content $managedReleaseStep -Pattern '(?m)^          EXPECTED_SOURCE_SHA: \$\{\{ github\.sha \}\}[ \t]*$' -Label 'managed release expected SHA environment'
Require-Regex -Content $managedReleaseStep -Pattern '(?m)^          gh release create \$ver\b[^\r\n]*--target "\$expectedSha"[^\r\n]*--title[ \t]*' -Label 'managed release command immutable target'
Require-Regex -Content $managedReleaseStep -Pattern '(?m)^          \$refJson = \(& gh api "repos/\$env:GITHUB_REPOSITORY/git/ref/tags/\$ver" \| Out-String\)[ \t]*$' -Label 'managed created-tag lookup'
Require-Regex -Content $managedReleaseStep -Pattern '(?m)^          for \(\$depth = 0; \$object\.type -eq ''tag''; \$depth\+\+\) \{[ \t]*$' -Label 'managed annotated-tag dereference'
Require-Regex -Content $managedReleaseStep -Pattern '(?m)^          if \(\$actualSha -ne \$expectedSha\) \{[ \t]*$' -Label 'managed exact tag provenance assertion'
Reject-Regex -Content $managedWorkflow -Pattern '(?m)^\s+- ''\.github/workflows/release\.yml''[ \t]*$' -Label 'managed workflow self-ignore'

$siteDataCommitStep = Get-WorkflowStep -Content $siteDataWorkflow -Name 'Commit refreshed data if it changed' -Label 'site-data commit and dispatch'
Require-Regex -Content $siteDataCommitStep -Pattern '(?ms)^.*?^          git pull --rebase origin main[ \t]*\r?\n.*?^          \$pushedSha = \(git rev-parse HEAD\)\.Trim\(\)\.ToLowerInvariant\(\)[ \t]*\r?\n.*?^          git push origin HEAD:main[ \t]*\r?\n.*?^          gh workflow run native-release\.yml --ref main -f publish_release=true -f source_sha=\$pushedSha[ \t]*$' -Label 'exact post-rebase site-data SHA dispatch order'

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
