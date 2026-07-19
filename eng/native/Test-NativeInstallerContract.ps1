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

function Get-WorkflowJob {
    param(
        [Parameter(Mandatory)][string]$Content,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Label
    )

    $escapedName = [System.Text.RegularExpressions.Regex]::Escape($Name)
    $pattern = '(?ms)^[ ]{2}' + $escapedName + ':\r?\n(?<body>.*?)(?=^[ ]{2}[A-Za-z0-9_-]+:\r?\n|\z)'
    $match = [System.Text.RegularExpressions.Regex]::Match(
        $Content,
        $pattern,
        [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
    if (-not $match.Success) {
        throw "Native installer contract could not resolve $Label workflow job: $Name"
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

function Require-NativePeFile {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Label,
        [switch]$RequireAmd64
    )

    Require-PeFile -Path $Path -Label $Label
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -lt 0x100) {
        throw "$Label is too small to contain a valid native PE image: $Path"
    }

    $peOffset = [System.BitConverter]::ToInt32($bytes, 0x3C)
    if ($peOffset -lt 0 -or $peOffset + 24 -ge $bytes.Length -or
        $bytes[$peOffset] -ne 0x50 -or $bytes[$peOffset + 1] -ne 0x45 -or
        $bytes[$peOffset + 2] -ne 0 -or $bytes[$peOffset + 3] -ne 0) {
        throw "$Label has an invalid PE header: $Path"
    }

    $machine = [System.BitConverter]::ToUInt16($bytes, $peOffset + 4)
    if ($RequireAmd64 -and $machine -ne 0x8664) {
        throw ("$Label must be AMD64, but its PE machine is 0x{0:X4}: $Path" -f $machine)
    }

    $optionalHeaderOffset = $peOffset + 24
    $optionalHeaderSize = [System.BitConverter]::ToUInt16($bytes, $peOffset + 20)
    $optionalMagic = [System.BitConverter]::ToUInt16($bytes, $optionalHeaderOffset)
    if ($RequireAmd64 -and $optionalMagic -ne 0x20B) {
        throw ("$Label must use PE32+, but its optional-header magic is 0x{0:X4}: $Path" -f $optionalMagic)
    }

    # IMAGE_DIRECTORY_ENTRY_COM_DESCRIPTOR is data-directory index 14. A pure
    # native binary has a zero RVA and size for this CLR header directory.
    $dataDirectoryOffset = if ($optionalMagic -eq 0x20B) {
        $optionalHeaderOffset + 112
    }
    elseif ($optionalMagic -eq 0x10B) {
        $optionalHeaderOffset + 96
    }
    else {
        throw ("$Label has unsupported optional-header magic 0x{0:X4}: $Path" -f $optionalMagic)
    }
    $comDescriptorOffset = $dataDirectoryOffset + (14 * 8)
    if ($comDescriptorOffset + 8 -gt $optionalHeaderOffset + $optionalHeaderSize -or
        $comDescriptorOffset + 8 -gt $bytes.Length) {
        throw "$Label has a truncated PE data-directory table: $Path"
    }
    $clrRva = [System.BitConverter]::ToUInt32($bytes, $comDescriptorOffset)
    $clrSize = [System.BitConverter]::ToUInt32($bytes, $comDescriptorOffset + 4)
    if ($clrRva -ne 0 -or $clrSize -ne 0) {
        throw "$Label contains a CLR header and is not the pure native C++ app: $Path"
    }

    # A framework-dependent or self-contained .NET apphost is itself a native PE
    # shim and therefore has no CLR header. Reject its embedded host markers too;
    # the genuine C++/WinRT executable neither hosts CoreCLR nor delegates to the
    # managed WinForge.dll oracle.
    $asciiImage = [System.Text.Encoding]::ASCII.GetString($bytes)
    $utf16Image = [System.Text.Encoding]::Unicode.GetString($bytes)
    foreach ($marker in @('hostfxr', 'coreclr', 'WinForge.dll')) {
        if ($asciiImage.IndexOf($marker, [System.StringComparison]::OrdinalIgnoreCase) -ge 0 -or
            $utf16Image.IndexOf($marker, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw "$Label contains forbidden .NET apphost marker '$marker': $Path"
        }
    }
}

function Require-NativeAmd64PeFile {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Label
    )

    Require-NativePeFile -Path $Path -Label $Label -RequireAmd64
}

function Assert-NativeReleasePayload {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Label
    )

    $payloadRoot = [System.IO.Path]::GetFullPath($Path)
    $blockedExtensions = @('.pdb', '.ilk', '.lib', '.exp')
    $blockedNames = @(
        'WinForge.dll',
        'WinForgeLauncher.exe',
        'WinForgeUpdater.exe',
        'coreclr.dll',
        'hostfxr.dll',
        'hostpolicy.dll'
    )
    $blocked = @(Get-ChildItem -LiteralPath $payloadRoot -Recurse -File | Where-Object {
        $relative = $_.FullName.Substring($payloadRoot.TrimEnd('\').Length).TrimStart('\', '/')
        $segments = @($relative -split '[\\/]')
        $_.Extension.ToLowerInvariant() -in $blockedExtensions -or
            $_.Name -in $blockedNames -or
            $_.Name.EndsWith('.deps.json', [System.StringComparison]::OrdinalIgnoreCase) -or
            $_.Name.EndsWith('.runtimeconfig.json', [System.StringComparison]::OrdinalIgnoreCase) -or
            $segments -contains 'updater-runtime'
    })
    if ($blocked.Count -gt 0) {
        $names = ($blocked | ForEach-Object { $_.FullName }) -join '; '
        throw "$Label contains managed/debug/link payload that must not ship: $names"
    }

    $shippedPeFiles = @(Get-ChildItem -LiteralPath $payloadRoot -Recurse -File | Where-Object {
        $_.Extension -in @('.exe', '.dll')
    })
    if ($shippedPeFiles.Count -eq 0) {
        throw "$Label contains no shipped PE executables or libraries: $payloadRoot"
    }
    foreach ($peFile in $shippedPeFiles) {
        Require-NativePeFile -Path $peFile.FullName -Label "$Label PE '$($peFile.FullName)'"
    }
    Require-NativeAmd64PeFile -Path (Join-Path $payloadRoot 'WinForge.exe') -Label "$Label WinForge executable"
}

$root = [System.IO.Path]::GetFullPath($RepoRoot)
$issPath = Join-Path $root 'installer\WinForge.Native.iss'
Require-File -Path $issPath -Label 'Native Inno Setup script'
$iss = [System.IO.File]::ReadAllText($issPath, [System.Text.Encoding]::UTF8)

$nativeWorkflowPath = Join-Path $root '.github\workflows\native-release.yml'
$siteDataWorkflowPath = Join-Path $root '.github\workflows\site-data.yml'
Require-File -Path $nativeWorkflowPath -Label 'Native release workflow'
Require-File -Path $siteDataWorkflowPath -Label 'Site-data workflow'
$nativeWorkflow = [System.IO.File]::ReadAllText($nativeWorkflowPath, [System.Text.Encoding]::UTF8).Replace("`r`n", "`n").Replace("`r", "`n")
$siteDataWorkflow = [System.IO.File]::ReadAllText($siteDataWorkflowPath, [System.Text.Encoding]::UTF8).Replace("`r`n", "`n").Replace("`r", "`n")

$legacyManagedReleasePath = Join-Path $root '.github\workflows\release.yml'
if (Test-Path -LiteralPath $legacyManagedReleasePath) {
    throw "Managed release workflow must remain removed so only the native C++ app can publish releases: $legacyManagedReleasePath"
}

function Get-PublisherScanContent {
    param([Parameter(Mandatory)][string]$Content)

    # Release commands documented in full-line comments are evidence, not live
    # automation. Strip the comment forms used by the executable source types
    # below before scanning; inline commands remain deliberately fail-closed.
    $withoutBlockComments = [System.Text.RegularExpressions.Regex]::Replace(
        $Content,
        '(?ms)^[ \t]*(?:<#.*?#>|/\*.*?\*/)[ \t]*(?:\r?\n|\z)',
        '')
    $executableLines = @([System.Text.RegularExpressions.Regex]::Split($withoutBlockComments, '\r?\n') | Where-Object {
        $_ -notmatch '(?i)^[ \t]*(?:#|//|REM(?:[ \t]|$)|::)'
    })
    return $executableLines -join "`n"
}

$releasePublisherPattern = @'
(?imx)
(?:
    \bgh(?:\.exe)?\s+release\s+(?:create|new|upload|edit)\b
  | &\s*gh(?:\.exe)?\s+@[A-Za-z0-9_]*release[A-Za-z0-9_]*\b
  | ^[ \t]*(?:-[ \t]+)?uses:[ \t]*(?:
        softprops/action-gh-release
      | ncipollo/release-action
      | actions/(?:create-release|upload-release-asset)
      | marvinpinto/action-automatic-releases
      | svenstaro/upload-release-action
      | AButler/upload-release-assets
      | release-drafter/release-drafter
      | Roang-zero1/github-upload-release-artifacts-action
      | xresloader/upload-to-github-release
      | elgohr/Github-Release-Action
      | meeDamian/github-release
      | Shopify/upload-to-release
      | csexton/release-asset-action
    )@
  | \b(?:octokit|github)(?:\.rest)?\.repos\.(?:createRelease|updateRelease|uploadReleaseAsset)\b
  | \b(?:New|Set)-GitHubRelease\b
  | (?s:\bmutation\b.{0,2000}?\b(?:createRelease|updateRelease)\s*\()
  | \bgh(?:\.exe)?\s+api\b
      (?=[^\r\n]{0,1200}(?:
          (?:(?:https?://)?(?:api|uploads)\.github\.com/)?repos/[^\s"'`]{1,300}/releases(?:\b|/)
        | \b(?:release)?upload(?:_|-)?url\b
      ))
      (?=[^\r\n]{0,1200}(?:
          --method(?:=|[ \t]+)(?:POST|PATCH|PUT)\b
        | (?:^|[ \t])(?:-[fF]|--field|--raw-field|--input)(?:=|[ \t]+)
      ))
      [^\r\n]*
  | \bcurl(?:\.exe)?\b
      (?=[^\r\n]{0,1200}(?:
          (?:(?:https?://)?(?:api|uploads)\.github\.com/)?repos/[^\s"'`]{1,300}/releases(?:\b|/)
        | \b(?:release)?upload(?:_|-)?url\b
      ))
      (?=[^\r\n]{0,1200}(?:
          --request(?:=|[ \t]+)(?:POST|PATCH|PUT)\b
        | -X[ \t]*(?:POST|PATCH|PUT)\b
        | --data(?:-ascii|-binary|-raw|-urlencode)?(?:=|[ \t]+)
        | -d(?:=|[ \t]+)
        | --form(?:=|[ \t]+)
        | -F(?:=|[ \t]+)
        | --upload-file(?:=|[ \t]+)
        | -T(?:=|[ \t]+)
      ))
      [^\r\n]*
  | \b(?:Invoke-RestMethod|Invoke-WebRequest|irm|iwr)\b
      (?=[^\r\n]{0,1200}(?:
          (?:(?:https?://)?(?:api|uploads)\.github\.com/)?repos/[^\s"'`]{1,300}/releases(?:\b|/)
        | \b(?:release)?upload(?:_|-)?url\b
      ))
      (?=[^\r\n]{0,1200}-Method(?:[ :=]|[ \t]+)(?:POST|PATCH|PUT)\b)
      [^\r\n]*
  | \b(?:requests|httpx|axios)\.(?:post|patch|put)\s*\(
      [^\r\n]{0,1200}(?:
          (?:(?:https?://)?(?:api|uploads)\.github\.com/)?repos/[^\s"'`]{1,300}/releases(?:\b|/)
        | \b(?:release)?upload(?:_|-)?url\b
      )
  | \bfetch\s*\(
      (?=[^\r\n]{0,1200}(?:
          (?:(?:https?://)?(?:api|uploads)\.github\.com/)?repos/[^\s"'`]{1,300}/releases(?:\b|/)
        | \b(?:release)?upload(?:_|-)?url\b
      ))
      (?=[^\r\n]{0,1200}\bmethod\s*:[ \t]*["'](?:POST|PATCH|PUT)["'])
      [^\r\n]*
)
'@

$publisherDenyFixtures = [ordered]@{
    'gh release create' = 'gh release create v1.2.3'
    'gh release new' = 'gh.exe release new v1.2.3'
    'gh release upload' = 'gh release upload v1.2.3 Managed-WinForge.zip'
    'gh.exe release upload' = 'gh.exe release upload v1.2.3 Managed-WinForge.zip --clobber'
    'gh release edit latest true' = 'gh release edit v1.2.3 --latest'
    'gh release edit latest false' = 'gh.exe release edit v1.2.3 --latest=false'
    'gh release edit metadata' = 'gh release edit v1.2.3 --title Managed-WinForge'
    'gh REST release create' = 'gh api repos/example/project/releases -f tag_name=v1.2.3'
    'gh REST release asset upload' = 'gh api --method POST repos/example/project/releases/42/assets?name=Managed.zip --hostname uploads.github.com --input Managed.zip'
    'gh REST upload_url asset upload' = 'gh api --method POST $upload_url --input Managed.zip'
    'curl REST release create' = 'curl.exe -X POST https://api.github.com/repos/example/project/releases --data-binary @release.json'
    'curl REST release asset upload' = 'curl --data-binary @Managed.zip https://uploads.github.com/repos/example/project/releases/42/assets?name=Managed.zip'
    'curl REST upload_url asset upload' = 'curl --request POST --data-binary @Managed.zip $UPLOAD_URL'
    'Invoke-RestMethod release create' = 'Invoke-RestMethod -Method Post -Uri https://api.github.com/repos/example/project/releases -Body $json'
    'Invoke-WebRequest upload_url asset upload' = 'Invoke-WebRequest -Uri $release.upload_url -Method Put -InFile Managed.zip'
    'GraphQL createRelease mutation' = 'gh api graphql -f query=''mutation { createRelease(input: $input) { release { id } } }'''
    'GraphQL updateRelease mutation' = 'gh api graphql -f query=''mutation { updateRelease(input: $input) { release { id } } }'''
    'Octokit release create' = 'octokit.rest.repos.createRelease(args)'
    'Octokit release update' = 'github.rest.repos.updateRelease(args)'
    'Octokit release asset upload' = 'octokit.repos.uploadReleaseAsset(args)'
    'PowerShell GitHub release update' = 'Set-GitHubRelease -Owner example -Repository project -Tag v1.2.3'
    'Python REST release asset upload' = 'requests.post(upload_url, data=asset)'
    'JavaScript REST release asset upload' = 'axios.put(releaseUploadUrl, asset)'
    'fetch REST release asset upload' = 'fetch("https://uploads.github.com/repos/example/project/releases/42/assets?name=Managed.zip", { method: "POST", body: asset })'
    'softprops release action' = 'uses: softprops/action-gh-release@v2'
    'ncipollo release action' = 'uses: ncipollo/release-action@v1'
    'official create-release action' = 'uses: actions/create-release@v1'
    'official upload-release-asset action' = 'uses: actions/upload-release-asset@v1'
    'automatic release action' = 'uses: marvinpinto/action-automatic-releases@latest'
    'svenstaro upload release action' = 'uses: svenstaro/upload-release-action@v2'
    'AButler upload release action' = 'uses: AButler/upload-release-assets@v3.0'
    'release drafter action' = 'uses: release-drafter/release-drafter@v6'
    'Roang upload release action' = 'uses: Roang-zero1/github-upload-release-artifacts-action@v3'
    'xresloader upload release action' = 'uses: xresloader/upload-to-github-release@v1'
    'elgohr GitHub release action' = 'uses: elgohr/Github-Release-Action@v5'
    'meeDamian GitHub release action' = 'uses: meeDamian/github-release@v2'
    'Shopify upload-to-release action' = 'uses: Shopify/upload-to-release@v2'
    'csexton release asset action' = 'uses: csexton/release-asset-action@v3'
}
foreach ($fixture in $publisherDenyFixtures.GetEnumerator()) {
    $fixtureContent = Get-PublisherScanContent -Content $fixture.Value
    if (-not [System.Text.RegularExpressions.Regex]::IsMatch(
            $fixtureContent,
            $releasePublisherPattern,
            [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)) {
        throw "Release-publisher deny pattern missed '$($fixture.Key)' self-test fixture: $($fixture.Value)"
    }
}

$trustedPublisherFixture = '& gh @releaseArgs'
$trustedPublisherFixtureMatches = [System.Text.RegularExpressions.Regex]::Matches(
    (Get-PublisherScanContent -Content $trustedPublisherFixture),
    $releasePublisherPattern,
    [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
if ($trustedPublisherFixtureMatches.Count -ne 1 -or
    $trustedPublisherFixtureMatches[0].Value -notmatch '(?i)^&\s*gh\s+@releaseArgs$') {
    throw "Release-publisher pattern no longer recognizes the one reviewed native publisher: $trustedPublisherFixture"
}

$publisherAllowFixtures = [ordered]@{
    'gh release view' = 'gh release view v1.2.3'
    'gh release download' = 'gh release download v1.2.3'
    'gh REST release read' = 'gh api repos/example/project/releases/latest'
    'curl REST release read' = 'curl.exe https://api.github.com/repos/example/project/releases/latest'
    'Invoke-RestMethod release read' = 'Invoke-RestMethod -Method Get -Uri https://api.github.com/repos/example/project/releases/latest'
    'GraphQL release query' = 'gh api graphql -f query=''query { repository(owner: "example", name: "project") { releases(first: 1) { nodes { id } } } }'''
    'Python REST release read' = 'requests.get("https://api.github.com/repos/example/project/releases/latest")'
    'JavaScript REST release read' = 'fetch("https://api.github.com/repos/example/project/releases/latest")'
    'workflow artifact upload' = 'uses: actions/upload-artifact@v4'
    'workflow artifact download' = 'uses: actions/download-artifact@v4'
    'YAML or shell full-line comment' = '# gh release upload v1.2.3 Managed.zip'
    'JavaScript full-line comment' = '// github.rest.repos.updateRelease(args)'
    'PowerShell block comment' = "<#`ngh release edit v1.2.3 --latest`n#>"
    'C-style block comment' = "/*`nuses: softprops/action-gh-release@v2`n*/"
    'batch REM comment' = 'REM gh release create v1.2.3'
    'batch label comment' = ':: gh release create v1.2.3'
}
foreach ($fixture in $publisherAllowFixtures.GetEnumerator()) {
    $fixtureContent = Get-PublisherScanContent -Content $fixture.Value
    if ([System.Text.RegularExpressions.Regex]::IsMatch(
            $fixtureContent,
            $releasePublisherPattern,
            [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)) {
        throw "Release-publisher deny pattern rejected safe '$($fixture.Key)' self-test fixture: $($fixture.Value)"
    }
}

$workflowRoot = Join-Path $root '.github\workflows'
foreach ($workflowFile in @(Get-ChildItem -LiteralPath $workflowRoot -File | Where-Object { $_.Extension -in @('.yml', '.yaml') })) {
    $workflowContent = [System.IO.File]::ReadAllText($workflowFile.FullName, [System.Text.Encoding]::UTF8)
    $workflowScanContent = Get-PublisherScanContent -Content $workflowContent
    $publisherMatches = [System.Text.RegularExpressions.Regex]::Matches(
        $workflowScanContent,
        $releasePublisherPattern,
        [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
    if ($workflowFile.FullName -eq $nativeWorkflowPath) {
        if ($publisherMatches.Count -ne 1 -or $publisherMatches[0].Value -notmatch '(?i)^&\s*gh\s+@releaseArgs$') {
            throw "Native workflow must contain exactly one reviewed '& gh @releaseArgs' publisher; found $($publisherMatches.Count)."
        }
    }
    elseif ($publisherMatches.Count -ne 0) {
        throw "Only native-release.yml may publish a GitHub release; rejected $($publisherMatches.Count) publisher(s) in $($workflowFile.Name)."
    }
}

# Release-capable commands hidden in a workflow-called first-party script are
# equally dangerous. Scan executable automation sources while excluding vendored,
# generated, build-output, and documentation trees. The contract file itself is
# excluded because it necessarily contains the deny-pattern literals.
$scriptExtensions = @('.ps1', '.psm1', '.sh', '.bash', '.cmd', '.bat', '.py', '.js', '.mjs', '.cjs')
$excludedAutomationSegments = '\\(?:\.git|\.vs|ThirdParty|bin|obj|node_modules|packages|Generated(?:[ _-]Files)?|TestResults|dist|out(?:-[^\\]+)?|design|docs|artifacts)\\'
foreach ($scriptFile in @(Get-ChildItem -LiteralPath $root -Recurse -File -ErrorAction SilentlyContinue | Where-Object {
    $_.Extension -in $scriptExtensions -and
        $_.FullName -ne $PSCommandPath -and
        $_.FullName -notmatch $excludedAutomationSegments
})) {
    $scriptContent = [System.IO.File]::ReadAllText($scriptFile.FullName, [System.Text.Encoding]::UTF8)
    $scriptScanContent = Get-PublisherScanContent -Content $scriptContent
    Reject-Regex -Content $scriptScanContent -Pattern $releasePublisherPattern -Label "release publisher hidden in first-party script $($scriptFile.FullName)"
}

Require-Regex -Content $nativeWorkflow -Pattern '(?m)^on:\r?\n  push:\r?\n  pull_request:\r?\n    branches: \[ main \]\r?\n  workflow_dispatch:[ \t]*$' -Label 'unfiltered every-push native trigger'
Require-Regex -Content $nativeWorkflow -Pattern '(?m)^      source_sha:\r?\n        description:.*\r?\n        required: false\r?\n        default: ''''\r?\n        type: string[ \t]*$' -Label 'optional exact source_sha dispatch input'
Require-Regex -Content $nativeWorkflow -Pattern '(?m)^permissions:\r?\n  contents: read[ \t]*$' -Label 'read-only workflow default token'

$nativeBuildJob = Get-WorkflowJob -Content $nativeWorkflow -Name 'build-test-package' -Label 'untrusted native build/test/package'
Require-Regex -Content $nativeBuildJob -Pattern '(?m)^    permissions:\r?\n      contents: read[ \t]*$' -Label 'read-only native build job token'
Require-Regex -Content $nativeBuildJob -Pattern '(?m)^    outputs:\r?\n      source_sha: \$\{\{ steps\.source\.outputs\.sha \}\}\r?\n      version: \$\{\{ steps\.runtime\.outputs\.version \}\}[ \t]*$' -Label 'exact source SHA and version job handoff'
Reject-Regex -Content (Get-PublisherScanContent -Content $nativeBuildJob) -Pattern $releasePublisherPattern -Label 'release publisher in untrusted native build job'

$nativeReleaseJob = Get-WorkflowJob -Content $nativeWorkflow -Name 'release-native' -Label 'trusted native release'
Require-Regex -Content $nativeReleaseJob -Pattern '(?m)^    needs: build-test-package[ \t]*$' -Label 'trusted release dependency on successful package job'
Require-Regex -Content $nativeReleaseJob -Pattern '(?m)^    if: \(github\.event_name == ''push'' && github\.ref_type == ''branch''\) \|\| \(github\.event_name == ''push'' && startsWith\(github\.ref, ''refs/tags/native-v''\)\) \|\| \(github\.event_name == ''workflow_dispatch'' && inputs\.publish_release == true\)[ \t]*$' -Label 'trusted branch-push/native-tag/manual release condition'
Require-Regex -Content $nativeReleaseJob -Pattern '(?m)^    permissions:\r?\n      contents: write[ \t]*$' -Label 'write token isolated to trusted release job'
$trustedPublisherMatches = [System.Text.RegularExpressions.Regex]::Matches(
    (Get-PublisherScanContent -Content $nativeReleaseJob),
    $releasePublisherPattern,
    [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
if ($trustedPublisherMatches.Count -ne 1) {
    throw "Trusted native release job must contain exactly one publisher; found $($trustedPublisherMatches.Count)."
}

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

$nativeRuntimeStep = Get-WorkflowStep -Content $nativeWorkflow -Name 'Stage native runtime' -Label 'native runtime staging'
Require-Regex -Content $nativeRuntimeStep -Pattern '(?m)^          \$blockedExtensions = @\(''\.pdb'', ''\.ilk'', ''\.lib'', ''\.exp''\)[ \t]*$' -Label 'native build/link artifact denylist'
Require-Regex -Content $nativeRuntimeStep -Pattern '(?m)^            ''WinForge\.dll'',[ \t]*$' -Label 'managed oracle assembly denylist'
Require-Regex -Content $nativeRuntimeStep -Pattern '(?m)^            ''WinForgeLauncher\.exe'',[ \t]*$' -Label 'managed launcher denylist'
Require-Regex -Content $nativeRuntimeStep -Pattern '(?m)^            ''WinForgeUpdater\.exe'',[ \t]*$' -Label 'managed updater denylist'
Require-Regex -Content $nativeRuntimeStep -Pattern '(?m)^            ''coreclr\.dll'',[ \t]*$' -Label 'CLR runtime denylist'
Require-Regex -Content $nativeRuntimeStep -Pattern '(?m)^              \$segments -contains ''updater-runtime''[ \t]*$' -Label 'managed updater directory denylist'
Require-Regex -Content $nativeRuntimeStep -Pattern '(?m)^          Copy-Item -LiteralPath ''THIRD-PARTY-NOTICES\.txt'' -Destination \$staging -Force[ \t]*$' -Label 'staged third-party notices'
Require-Regex -Content $nativeRuntimeStep -Pattern '(?m)^          "dir=\$staging" \| Out-File -FilePath \$env:GITHUB_OUTPUT -Append -Encoding utf8[ \t]*$' -Label 'clean staged runtime output'

$nativeCheckoutReleaseStep = Get-WorkflowStep -Content $nativeWorkflow -Name 'Checkout exact native release source' -Label 'trusted exact-source checkout'
Require-Regex -Content $nativeCheckoutReleaseStep -Pattern '(?m)^          fetch-depth: 0[ \t]*$' -Label 'trusted release full-history checkout'
Require-Regex -Content $nativeCheckoutReleaseStep -Pattern '(?m)^          ref: \$\{\{ needs\.build-test-package\.outputs\.source_sha \}\}[ \t]*$' -Label 'trusted release exact-source checkout'

$nativeVerifyReleaseSourceStep = Get-WorkflowStep -Content $nativeWorkflow -Name 'Verify exact native release source' -Label 'trusted exact-source verification'
Require-Regex -Content $nativeVerifyReleaseSourceStep -Pattern '(?m)^          EXPECTED_SOURCE_SHA: \$\{\{ needs\.build-test-package\.outputs\.source_sha \}\}[ \t]*$' -Label 'trusted release source SHA handoff'
Require-Regex -Content $nativeVerifyReleaseSourceStep -Pattern '(?m)^          \$actualSha = \(git rev-parse HEAD\)\.Trim\(\)\.ToLowerInvariant\(\)[ \t]*$' -Label 'trusted release checkout SHA verification'

$nativeReleaseStep = Get-WorkflowStep -Content $nativeWorkflow -Name 'Create sole native GitHub release' -Label 'sole native release creation'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^          EXPECTED_SOURCE_SHA: \$\{\{ needs\.build-test-package\.outputs\.source_sha \}\}[ \t]*$' -Label 'native release expected SHA environment'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^          NATIVE_VERSION: \$\{\{ needs\.build-test-package\.outputs\.version \}\}[ \t]*$' -Label 'native release version environment'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^          \$usePushedNativeTag = \$env:EVENT_NAME -eq ''push'' -and[ \t]*$' -Label 'native-tag event discrimination'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^            \(\[string\]\$env:EVENT_REF_NAME\)\.StartsWith\(''native-v'', \[StringComparison\]::Ordinal\)[ \t]*$' -Label 'native-only pushed tag acceptance'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^          \$portableAsset = "release-assets/WinForge-native-x64-\$env:NATIVE_VERSION\.zip"[ \t]*$' -Label 'exact native portable release asset'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^          \$installerAsset = ''release-assets/WinForge-Native-Setup\.exe''[ \t]*$' -Label 'exact native installer release asset'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^          git fetch --no-tags origin ''\+refs/heads/main:refs/remotes/origin/main''[ \t]*$' -Label 'fresh origin/main release-tip fetch'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^          \$currentMainSha = \(git rev-parse refs/remotes/origin/main\)\.Trim\(\)\.ToLowerInvariant\(\)[ \t]*$' -Label 'fresh origin/main tip resolution'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^          \$isMainChannel = \$env:EVENT_REF_TYPE -eq ''branch'' -and[ \t]*$' -Label 'main release channel discrimination'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^          \$isCurrentMainTip = \$isMainChannel -and \$expectedSha -eq \$currentMainSha[ \t]*$' -Label 'exact current-main-tip release gate'
Require-Regex -Content $nativeReleaseStep -Pattern '(?ms)^          if \(\$isCurrentMainTip\) \{\r?\n            \$releaseArgs \+= ''--latest''\r?\n          \}\r?\n          elseif \(\$isMainChannel\) \{\r?\n            \$releaseArgs \+= ''--latest=false''\r?\n          \}\r?\n          else \{\r?\n            \$releaseArgs \+= ''--prerelease''\r?\n          \}[ \t]*$' -Label 'current-main latest, older-main non-latest, and branch-prerelease modes'
Require-Regex -Content $nativeReleaseStep -Pattern '(?ms)^          \$releaseArgs = @\(\r?\n            ''release'', ''create'', \$tag,\r?\n            \$portableAsset,\r?\n            \$installerAsset,\r?\n            ''--target'', \$expectedSha,\r?\n            ''--title'', \$title,\r?\n            ''--notes'', \$notes\r?\n          \)[ \t]*$' -Label 'exact native release assets and immutable target arguments'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^          & gh @releaseArgs[ \t]*$' -Label 'sole native release publisher invocation'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^          \$refJson = \(& gh api "repos/\$env:GITHUB_REPOSITORY/git/ref/tags/\$tag" \| Out-String\)[ \t]*$' -Label 'native created-tag lookup'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^          for \(\$depth = 0; \$object\.type -eq ''tag''; \$depth\+\+\) \{[ \t]*$' -Label 'native annotated-tag dereference'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^          if \(\$actualSha -ne \$expectedSha\) \{[ \t]*$' -Label 'native exact tag provenance assertion'

$nativeAssetStageStep = Get-WorkflowStep -Content $nativeWorkflow -Name 'Stage exact native release assets' -Label 'exact native asset staging'
Require-Regex -Content $nativeAssetStageStep -Pattern '(?m)^          if \(\$actualNames\.Count -ne 2 -or \(Compare-Object \$expectedNames \$actualNames\)\) \{[ \t]*$' -Label 'exact two-file package handoff assertion'

$nativeUploadStep = Get-WorkflowStep -Content $nativeWorkflow -Name 'Upload exact native release assets' -Label 'native artifact upload'
Require-Regex -Content $nativeUploadStep -Pattern '(?m)^          name: WinForge-native-release-\$\{\{ steps\.runtime\.outputs\.version \}\}[ \t]*$' -Label 'native artifact bundle name'
Require-Regex -Content $nativeUploadStep -Pattern '(?m)^          path: \|\r?\n            release-assets/WinForge-native-x64-\$\{\{ steps\.runtime\.outputs\.version \}\}\.zip\r?\n            release-assets/WinForge-Native-Setup\.exe\r?\n          if-no-files-found: error[ \t]*$' -Label 'exact setup and portable artifact upload'

$nativeDownloadStep = Get-WorkflowStep -Content $nativeWorkflow -Name 'Download exact native release assets' -Label 'trusted native artifact download'
Require-Regex -Content $nativeDownloadStep -Pattern '(?m)^          name: WinForge-native-release-\$\{\{ needs\.build-test-package\.outputs\.version \}\}[ \t]*$' -Label 'trusted versioned artifact download'
Require-Regex -Content $nativeDownloadStep -Pattern '(?m)^          path: release-assets[ \t]*$' -Label 'trusted release asset download path'

$nativeHandoffStep = Get-WorkflowStep -Content $nativeWorkflow -Name 'Verify exact native release handoff' -Label 'trusted native artifact handoff verification'
Require-Regex -Content $nativeHandoffStep -Pattern '(?m)^          if \(\$actualNames\.Count -ne 2 -or \(Compare-Object \$expectedNames \$actualNames\)\) \{[ \t]*$' -Label 'trusted exact two-file release handoff assertion'

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
    'Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Excludes: "*.pdb,*.ilk,*.lib,*.exp,THIRD-PARTY-NOTICES.txt"',
    'Source: "..\THIRD-PARTY-NOTICES.txt"; DestDir: "{app}"',
    'Filename: "{app}\{#MyAppExe}"'
)

foreach ($literal in $requiredLiterals) {
    Require-Literal -Content $iss -Literal $literal -Label 'required installer directive'
}

$explicitNoticeSources = [System.Text.RegularExpressions.Regex]::Matches(
    $iss,
    '(?m)^Source: "\.\.\\THIRD-PARTY-NOTICES\.txt"; DestDir: "\{app\}"; Flags: ignoreversion[ \t]*\r?$',
    [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
if ($explicitNoticeSources.Count -ne 1) {
    throw "Native installer must contain exactly one explicit third-party-notices source; found $($explicitNoticeSources.Count)."
}

if ($iss -match '(?im)^\s*PrivilegesRequired\s*=\s*admin\s*$') {
    throw 'Native installer contract must remain per-user and must not request administrator privileges.'
}

if ($PublishDir) {
    $publish = [System.IO.Path]::GetFullPath($PublishDir)
    if (-not (Test-Path -LiteralPath $publish -PathType Container)) {
        throw "Native publish directory is missing: $publish"
    }
    Require-File -Path (Join-Path $publish 'THIRD-PARTY-NOTICES.txt') -Label 'Native publish third-party notices'
    Assert-NativeReleasePayload -Path $publish -Label 'Native publish payload'
}

if ($InstallerPath) {
    $installer = [System.IO.Path]::GetFullPath($InstallerPath)
    if ([System.IO.Path]::GetFileName($installer) -ne 'WinForge-Native-Setup.exe') {
        throw "Native installer has an unexpected filename: $installer"
    }
    Require-NativePeFile -Path $installer -Label 'Native installer'
}

if ($InstallDir) {
    $install = [System.IO.Path]::GetFullPath($InstallDir)
    if (-not (Test-Path -LiteralPath $install -PathType Container)) {
        throw "Native install directory is missing: $install"
    }

    Require-NativeAmd64PeFile -Path (Join-Path $install 'WinForge.exe') -Label 'Installed native executable'
    Require-PeFile -Path (Join-Path $install 'unins000.exe') -Label 'Installed native uninstaller'
    Require-File -Path (Join-Path $install 'THIRD-PARTY-NOTICES.txt') -Label 'Installed third-party notices'
    Assert-NativeReleasePayload -Path $install -Label 'Installed native payload'
}

Write-Output 'Native installer contract: PASS'
