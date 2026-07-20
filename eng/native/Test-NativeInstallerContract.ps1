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
$pagesWorkflowPath = Join-Path $root '.github\workflows\pages.yml'
Require-File -Path $nativeWorkflowPath -Label 'Native release workflow'
Require-File -Path $siteDataWorkflowPath -Label 'Site-data workflow'
Require-File -Path $pagesWorkflowPath -Label 'GitHub Pages workflow'
$nativeWorkflow = [System.IO.File]::ReadAllText($nativeWorkflowPath, [System.Text.Encoding]::UTF8).Replace("`r`n", "`n").Replace("`r", "`n")
$siteDataWorkflow = [System.IO.File]::ReadAllText($siteDataWorkflowPath, [System.Text.Encoding]::UTF8).Replace("`r`n", "`n").Replace("`r", "`n")
$pagesWorkflow = [System.IO.File]::ReadAllText($pagesWorkflowPath, [System.Text.Encoding]::UTF8).Replace("`r`n", "`n").Replace("`r", "`n")

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

$trustedNativeDirectCreatePattern = '(?im)^[ \t]*\$createOutput[ \t]*=[ \t]*\(&[ \t]+gh[ \t]+@releaseArgs[ \t]+2>&1[ \t]*\|[ \t]*Out-String\)[ \t]*$'
$trustedNativeLatestEditPattern = '(?ms)^[ \t]*Invoke-GhWithRetry[ \t]+-Description[ \t]+"native Latest promotion for \$tag"[ \t]+`\r?\n[ \t]+-Arguments[ \t]+@\(''release'', ''edit'', \$tag, ''--latest'', ''--prerelease=false'', ''--draft=false''\)[ \t]*\|[ \t]*Out-Null[ \t]*$'
$trustedNativeAssetUploadPattern = '(?ms)^[ \t]*Invoke-GhWithRetry[ \t]+-Description[ \t]+"native release asset reconciliation for \$missingAssetName"[ \t]+`\r?\n[ \t]+-Arguments[ \t]+@\(''release'', ''upload'', \$tag, \$missingAssetPath, ''--clobber''\)[ \t]*\|[ \t]*Out-Null[ \t]*$'
$trustedNativeHelperMutationPattern = '(?ms)^[ \t]*Invoke-GhWithRetry[^\r\n]*\r?\n[ \t]+-Arguments[ \t]+@\(''release'', ''(?:edit|upload)''[^\r\n]*\)[^\r\n]*$'

function Assert-TrustedNativeReleaseMutationSurface {
    param(
        [Parameter(Mandatory)][string]$Content,
        [Parameter(Mandatory)][string]$Label
    )

    # Fixtures can inherit the checkout's CRLF while workflow text is normalized
    # at read time. Keep the release-mutation assertions line-ending invariant.
    $Content = $Content.Replace("`r`n", "`n").Replace("`r", "`n")

    $publisherMatches = [System.Text.RegularExpressions.Regex]::Matches(
        (Get-PublisherScanContent -Content $Content),
        $releasePublisherPattern,
        [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
    if ($publisherMatches.Count -ne 1 -or
        $publisherMatches[0].Value -notmatch '(?i)^&\s*gh\s+@releaseArgs$') {
        throw "$Label must expose exactly one direct native release-create command; found $($publisherMatches.Count)."
    }

    $createMatches = [System.Text.RegularExpressions.Regex]::Matches(
        $Content,
        $trustedNativeDirectCreatePattern,
        [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
    if ($createMatches.Count -ne 1) {
        throw "$Label must perform exactly one non-retried native release-create attempt; found $($createMatches.Count)."
    }

    $helperMutations = [System.Text.RegularExpressions.Regex]::Matches(
        $Content,
        $trustedNativeHelperMutationPattern,
        [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
    if ($helperMutations.Count -ne 2) {
        throw "$Label must contain exactly the reviewed native Latest-edit and missing-asset upload helper mutations; found $($helperMutations.Count)."
    }
    foreach ($expectedPattern in @($trustedNativeLatestEditPattern, $trustedNativeAssetUploadPattern)) {
        if (-not [System.Text.RegularExpressions.Regex]::IsMatch(
                $Content,
                $expectedPattern,
                [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)) {
            throw "$Label is missing a reviewed idempotent native release mutation."
        }
    }

    Reject-Regex -Content $Content -Pattern '(?ms)Invoke-GhWithRetry[^\r\n]*\r?\n[ \t]+-Arguments[ \t]+@\(''release'', ''create''' -Label "$Label blind release-create retry"
    Require-Regex -Content $Content -Pattern '(?m)^\s*\$existingRelease = Get-ReleaseByTagWithRetry -Tag \$tag -WaitForVisibility[ \t]*$' -Label "$Label ambiguous-create visibility reconciliation"
}

$trustedPublisherFixture = @'
$existingRelease = Get-ReleaseByTagWithRetry -Tag $tag
if ($null -eq $existingRelease) {
  $createOutput = (& gh @releaseArgs 2>&1 | Out-String)
  if ($LASTEXITCODE -ne 0) {
    $existingRelease = Get-ReleaseByTagWithRetry -Tag $tag -WaitForVisibility
  }
}
Invoke-GhWithRetry -Description "native Latest promotion for $tag" `
  -Arguments @('release', 'edit', $tag, '--latest', '--prerelease=false', '--draft=false') | Out-Null
Invoke-GhWithRetry -Description "native release asset reconciliation for $missingAssetName" `
  -Arguments @('release', 'upload', $tag, $missingAssetPath, '--clobber') | Out-Null
'@
Assert-TrustedNativeReleaseMutationSurface -Content $trustedPublisherFixture -Label 'Trusted publisher self-test fixture'

$publisherAllowFixtures = [ordered]@{
    'gh release view' = 'gh release view v1.2.3'
    'gh release download' = 'gh release download v1.2.3'
    'gh REST release read' = 'gh api repos/example/project/releases/latest'
    'curl REST release read' = 'curl.exe https://api.github.com/repos/example/project/releases/latest'
    'Invoke-RestMethod release read' = 'Invoke-RestMethod -Method Get -Uri https://api.github.com/repos/example/project/releases/latest'
    'GraphQL release query' = 'gh api graphql -f query=''query { repository(owner: "example", name: "project") { releases(first: 1) { nodes { id } } } }'''
    'Python REST release read' = 'requests.get("https://api.github.com/repos/example/project/releases/latest")'
    'JavaScript REST release read' = 'fetch("https://api.github.com/repos/example/project/releases/latest")'
    'workflow artifact upload' = 'uses: actions/upload-artifact@v7'
    'workflow artifact download' = 'uses: actions/download-artifact@v8'
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

$releaseModeFixtures = @(
    [pscustomobject]@{ Name = 'current main'; IsMain = $true; IsCurrentMain = $true; CreateMode = '--latest=false'; EditLatest = $true; Prerelease = $false; MustDifferFromLatest = $false }
    [pscustomobject]@{ Name = 'older main'; IsMain = $true; IsCurrentMain = $false; CreateMode = '--latest=false'; EditLatest = $false; Prerelease = $false; MustDifferFromLatest = $true }
    [pscustomobject]@{ Name = 'branch'; IsMain = $false; IsCurrentMain = $false; CreateMode = '--prerelease'; EditLatest = $false; Prerelease = $true; MustDifferFromLatest = $true }
    [pscustomobject]@{ Name = 'native tag'; IsMain = $false; IsCurrentMain = $false; CreateMode = '--prerelease'; EditLatest = $false; Prerelease = $true; MustDifferFromLatest = $true }
)
foreach ($fixture in $releaseModeFixtures) {
    $createMode = if ($fixture.IsMain) { '--latest=false' } else { '--prerelease' }
    $editLatest = [bool]$fixture.IsCurrentMain
    $prerelease = -not [bool]$fixture.IsMain
    $mustDifferFromLatest = -not [bool]$fixture.IsCurrentMain
    if ($createMode -cne $fixture.CreateMode -or
        $editLatest -ne $fixture.EditLatest -or
        $prerelease -ne $fixture.Prerelease -or
        $mustDifferFromLatest -ne $fixture.MustDifferFromLatest) {
        throw "Release-mode self-test failed for $($fixture.Name)."
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
        Assert-TrustedNativeReleaseMutationSurface -Content $workflowContent -Label 'Native workflow'
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

Require-Regex -Content $nativeWorkflow -Pattern '(?m)^on:\r?\n  push:\r?\n  pull_request:\r?\n    branches: \[main\]\r?\n  workflow_dispatch:[ \t]*$' -Label 'unfiltered every-push native trigger'
Require-Regex -Content $nativeWorkflow -Pattern '(?m)^      source_sha:\r?\n        description:.*\r?\n        required: false\r?\n        default: ""\r?\n        type: string[ \t]*$' -Label 'optional exact source_sha dispatch input'
Require-Regex -Content $nativeWorkflow -Pattern '(?m)^      release_version:\r?\n        description:.*\r?\n        required: false\r?\n        default: ""\r?\n        type: string[ \t]*$' -Label 'optional immutable release_version dispatch input'
Require-Regex -Content $nativeWorkflow -Pattern '(?m)^permissions:\r?\n  contents: read[ \t]*$' -Label 'read-only workflow default token'

$requiredNativeActionCounts = [ordered]@{
    'actions/checkout@v7' = 2
    'microsoft/setup-msbuild@v3' = 1
    'actions/upload-artifact@v7' = 1
    'actions/download-artifact@v8' = 1
}
foreach ($action in $requiredNativeActionCounts.GetEnumerator()) {
    $actionPattern = '(?im)^[ \t]*uses:[ \t]*' + [System.Text.RegularExpressions.Regex]::Escape($action.Key) + '[ \t]*$'
    $actionMatches = [System.Text.RegularExpressions.Regex]::Matches(
        $nativeWorkflow,
        $actionPattern,
        [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
    if ($actionMatches.Count -ne $action.Value) {
        throw "Native workflow must use $($action.Key) exactly $($action.Value) time(s); found $($actionMatches.Count)."
    }
}
$obsoleteNativeActionPattern = '(?im)^[ \t]*uses:[ \t]*(?:actions/checkout@v[1-6]|actions/upload-artifact@v[1-6]|actions/download-artifact@v[1-7]|microsoft/setup-msbuild@v[12])[ \t]*$'
foreach ($fixture in @(
    'uses: actions/checkout@v4',
    'uses: actions/upload-artifact@v4',
    'uses: actions/download-artifact@v4',
    'uses: microsoft/setup-msbuild@v2'
)) {
    if (-not [System.Text.RegularExpressions.Regex]::IsMatch(
            $fixture,
            $obsoleteNativeActionPattern,
            [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)) {
        throw "Obsolete-action deny pattern missed Node20 self-test fixture: $fixture"
    }
}
foreach ($fixture in $requiredNativeActionCounts.Keys) {
    if ([System.Text.RegularExpressions.Regex]::IsMatch(
            "uses: $fixture",
            $obsoleteNativeActionPattern,
            [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)) {
        throw "Obsolete-action deny pattern rejected required Node24 self-test fixture: $fixture"
    }
}
Reject-Regex -Content $nativeWorkflow -Pattern $obsoleteNativeActionPattern -Label 'obsolete/Node20 action major in native workflow'

$nativeBuildJob = Get-WorkflowJob -Content $nativeWorkflow -Name 'build-test-package' -Label 'untrusted native build/test/package'
Require-Regex -Content $nativeBuildJob -Pattern '(?m)^    permissions:\r?\n      contents: read[ \t]*$' -Label 'read-only native build job token'
Require-Regex -Content $nativeBuildJob -Pattern '(?m)^    outputs:\r?\n      source_sha: \$\{\{ steps\.source\.outputs\.sha \}\}\r?\n      version: \$\{\{ steps\.runtime\.outputs\.version \}\}[ \t]*$' -Label 'exact source SHA and version job handoff'
Reject-Regex -Content (Get-PublisherScanContent -Content $nativeBuildJob) -Pattern $releasePublisherPattern -Label 'release publisher in untrusted native build job'

$nativeReleaseJob = Get-WorkflowJob -Content $nativeWorkflow -Name 'release-native' -Label 'trusted native release'
Require-Regex -Content $nativeReleaseJob -Pattern '(?m)^    needs: build-test-package[ \t]*$' -Label 'trusted release dependency on successful package job'
Require-Regex -Content $nativeReleaseJob -Pattern '(?m)^    if: \$\{\{ needs\.build-test-package\.result == ''success'' && \(github\.event_name == ''push'' \|\| \(github\.event_name == ''workflow_dispatch'' && inputs\.publish_release == true\)\) \}\}[ \t]*$' -Label 'test-gated every-push/manual release condition'
Require-Regex -Content $nativeReleaseJob -Pattern '(?m)^    permissions:\r?\n      contents: write[ \t]*$' -Label 'write token isolated to trusted release job'
Assert-TrustedNativeReleaseMutationSurface -Content $nativeReleaseJob -Label 'Trusted native release job'

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
Require-Regex -Content $nativeRuntimeStep -Pattern '(?m)^          EVENT_NAME: \$\{\{ github\.event_name \}\}[ \t]*$' -Label 'runtime event-name wiring for native tags'
Require-Regex -Content $nativeRuntimeStep -Pattern '(?m)^          EVENT_REF_NAME: \$\{\{ github\.ref_name \}\}[ \t]*$' -Label 'runtime ref-name wiring for native tags'
Require-Regex -Content $nativeRuntimeStep -Pattern '(?m)^          EVENT_REF_TYPE: \$\{\{ github\.ref_type \}\}[ \t]*$' -Label 'runtime ref-type wiring for native tags'
Require-Regex -Content $nativeRuntimeStep -Pattern '(?m)^          REQUESTED_RELEASE_VERSION: \$\{\{ inputs\.release_version \}\}[ \t]*$' -Label 'runtime requested-version wiring'
Require-Regex -Content $nativeRuntimeStep -Pattern '(?m)^          \$blockedExtensions = @\(''\.pdb'', ''\.ilk'', ''\.lib'', ''\.exp''\)[ \t]*$' -Label 'native build/link artifact denylist'
Require-Regex -Content $nativeRuntimeStep -Pattern '(?m)^            ''WinForge\.dll'',[ \t]*$' -Label 'managed oracle assembly denylist'
Require-Regex -Content $nativeRuntimeStep -Pattern '(?m)^            ''WinForgeLauncher\.exe'',[ \t]*$' -Label 'managed launcher denylist'
Require-Regex -Content $nativeRuntimeStep -Pattern '(?m)^            ''WinForgeUpdater\.exe'',[ \t]*$' -Label 'managed updater denylist'
Require-Regex -Content $nativeRuntimeStep -Pattern '(?m)^            ''coreclr\.dll'',[ \t]*$' -Label 'CLR runtime denylist'
Require-Regex -Content $nativeRuntimeStep -Pattern '(?m)^              \$segments -contains ''updater-runtime''[ \t]*$' -Label 'managed updater directory denylist'
Require-Regex -Content $nativeRuntimeStep -Pattern '(?m)^          Copy-Item -LiteralPath ''THIRD-PARTY-NOTICES\.txt'' -Destination \$staging -Force[ \t]*$' -Label 'staged third-party notices'
Require-Regex -Content $nativeRuntimeStep -Pattern '(?m)^          "dir=\$staging" \| Out-File -FilePath \$env:GITHUB_OUTPUT -Append -Encoding utf8[ \t]*$' -Label 'clean staged runtime output'
Require-Regex -Content $nativeRuntimeStep -Pattern '(?m)^          \$pushedNativeTagVersion = if \([ \t]*$' -Label 'pushed-native-tag version derivation'
Require-Regex -Content $nativeRuntimeStep -Pattern '(?m)^            \(\[string\]\$env:EVENT_REF_NAME\)\.StartsWith\(''native-v'', \[StringComparison\]::Ordinal\)[ \t]*$' -Label 'runtime native-tag prefix gate'
Require-Regex -Content $nativeRuntimeStep -Pattern '(?m)^            \(\[string\]\$env:EVENT_REF_NAME\)\.Substring\(''native-v''\.Length\)[ \t]*$' -Label 'runtime native-tag version extraction'
Require-Regex -Content $nativeRuntimeStep -Pattern '(?m)^          if \(\$requestedVersion -and \$pushedNativeTagVersion -and \$requestedVersion -cne \$pushedNativeTagVersion\) \{[ \t]*$' -Label 'reject conflicting pushed-tag and requested versions'
Require-Regex -Content $nativeRuntimeStep -Pattern '(?m)^          elseif \(\$pushedNativeTagVersion\) \{[ \t]*$' -Label 'pushed-native-tag version precedence'
Require-Regex -Content $nativeRuntimeStep -Pattern '(?m)^          if \(\$version -notmatch ''\^\[1-9\]\[0-9\]\*\\\.\[0-9\]\+\\\.\[0-9\]\+\$''\) \{[ \t]*$' -Label 'numeric semantic native version validation'

$nativeCheckoutReleaseStep = Get-WorkflowStep -Content $nativeWorkflow -Name 'Checkout exact native release source' -Label 'trusted exact-source checkout'
Require-Regex -Content $nativeCheckoutReleaseStep -Pattern '(?m)^          fetch-depth: 0[ \t]*$' -Label 'trusted release full-history checkout'
Require-Regex -Content $nativeCheckoutReleaseStep -Pattern '(?m)^          ref: \$\{\{ needs\.build-test-package\.outputs\.source_sha \}\}[ \t]*$' -Label 'trusted release exact-source checkout'

$nativeVerifyReleaseSourceStep = Get-WorkflowStep -Content $nativeWorkflow -Name 'Verify exact native release source' -Label 'trusted exact-source verification'
Require-Regex -Content $nativeVerifyReleaseSourceStep -Pattern '(?m)^          EXPECTED_SOURCE_SHA: \$\{\{ needs\.build-test-package\.outputs\.source_sha \}\}[ \t]*$' -Label 'trusted release source SHA handoff'
Require-Regex -Content $nativeVerifyReleaseSourceStep -Pattern '(?m)^          \$actualSha = \(git rev-parse HEAD\)\.Trim\(\)\.ToLowerInvariant\(\)[ \t]*$' -Label 'trusted release checkout SHA verification'

$nativeReleaseStep = Get-WorkflowStep -Content $nativeWorkflow -Name 'Create sole native GitHub release' -Label 'sole native release creation'
$releaseRunMatch = [System.Text.RegularExpressions.Regex]::Match(
    $nativeReleaseStep,
    '(?ms)^        run: \|\r?\n(?<script>.*)\z',
    [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
if (-not $releaseRunMatch.Success) {
    throw 'Native installer contract could not extract the trusted native release PowerShell body.'
}
$releaseRunLines = @([System.Text.RegularExpressions.Regex]::Split($releaseRunMatch.Groups['script'].Value, '\r?\n') | ForEach-Object {
    if ($_.StartsWith('          ', [System.StringComparison]::Ordinal)) { $_.Substring(10) } else { $_ }
})
$releaseRunScript = ($releaseRunLines -join "`n").Replace('${{ github.run_number }}', '12345')
$releaseRunTokens = $null
$releaseRunParseErrors = $null
$releaseRunAst = [System.Management.Automation.Language.Parser]::ParseInput(
    $releaseRunScript,
    [ref]$releaseRunTokens,
    [ref]$releaseRunParseErrors)
if ($releaseRunParseErrors.Count -ne 0) {
    $messages = @($releaseRunParseErrors | ForEach-Object { $_.Message }) -join '; '
    throw "Trusted native release PowerShell does not parse: $messages"
}
$latestValidatorAsts = @($releaseRunAst.FindAll({
    param($node)
    $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
        $node.Name -eq 'Get-NativeLatestPostconditionErrors'
}, $true))
if ($latestValidatorAsts.Count -ne 1) {
    throw "Trusted native release must define exactly one native Latest validator; found $($latestValidatorAsts.Count)."
}
Invoke-Expression $latestValidatorAsts[0].Extent.Text

$latestFixtureSha = 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'
$latestFixtureTag = 'native-v1.2.3'
$latestFixtureAssets = @(
    [pscustomobject]@{ name = 'WinForge-Native-Setup.exe' },
    [pscustomobject]@{ name = 'WinForge-native-x64-1.2.3.zip' }
)
$latestPostconditionFixtures = @(
    [pscustomobject]@{ Name = 'valid stable native Latest'; ShouldPass = $true; Release = [pscustomobject]@{ tag_name = $latestFixtureTag; target_commitish = $latestFixtureSha; draft = $false; prerelease = $false; assets = $latestFixtureAssets } }
    [pscustomobject]@{ Name = 'managed Latest'; ShouldPass = $false; Release = [pscustomobject]@{ tag_name = 'v1.0.256'; target_commitish = $latestFixtureSha; draft = $false; prerelease = $false; assets = $latestFixtureAssets } }
    [pscustomobject]@{ Name = 'wrong Latest target SHA'; ShouldPass = $false; Release = [pscustomobject]@{ tag_name = $latestFixtureTag; target_commitish = ('b' * 40); draft = $false; prerelease = $false; assets = $latestFixtureAssets } }
    [pscustomobject]@{ Name = 'wrong Latest assets'; ShouldPass = $false; Release = [pscustomobject]@{ tag_name = $latestFixtureTag; target_commitish = $latestFixtureSha; draft = $false; prerelease = $false; assets = @([pscustomobject]@{ name = 'WinForge-Setup.exe' }) } }
    [pscustomobject]@{ Name = 'prerelease Latest'; ShouldPass = $false; Release = [pscustomobject]@{ tag_name = $latestFixtureTag; target_commitish = $latestFixtureSha; draft = $false; prerelease = $true; assets = $latestFixtureAssets } }
    [pscustomobject]@{ Name = 'draft Latest'; ShouldPass = $false; Release = [pscustomobject]@{ tag_name = $latestFixtureTag; target_commitish = $latestFixtureSha; draft = $true; prerelease = $false; assets = $latestFixtureAssets } }
)
foreach ($fixture in $latestPostconditionFixtures) {
    $fixtureErrors = @(Get-NativeLatestPostconditionErrors -Release $fixture.Release -ExpectedSha $latestFixtureSha)
    $passed = $fixtureErrors.Count -eq 0
    if ($passed -ne $fixture.ShouldPass) {
        throw "Native Latest postcondition self-test failed for $($fixture.Name): $($fixtureErrors -join '; ')"
    }
}

Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^          EXPECTED_SOURCE_SHA: \$\{\{ needs\.build-test-package\.outputs\.source_sha \}\}[ \t]*$' -Label 'native release expected SHA environment'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^          NATIVE_VERSION: \$\{\{ needs\.build-test-package\.outputs\.version \}\}[ \t]*$' -Label 'native release version environment'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^          \$usePushedNativeTag = \$env:EVENT_NAME -eq ''push'' -and[ \t]*$' -Label 'native-tag event discrimination'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^            \(\[string\]\$env:EVENT_REF_NAME\)\.StartsWith\(''native-v'', \[StringComparison\]::Ordinal\)[ \t]*$' -Label 'native-only pushed tag acceptance'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^          \$portableAsset = "release-assets/WinForge-native-x64-\$env:NATIVE_VERSION\.zip"[ \t]*$' -Label 'exact native portable release asset'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^          \$installerAsset = ''release-assets/WinForge-Native-Setup\.exe''[ \t]*$' -Label 'exact native installer release asset'
Require-Regex -Content $nativeReleaseStep -Pattern '(?ms)^          \$expectedAssetNames = @\(\r?\n            \[System\.IO\.Path\]::GetFileName\(\$installerAsset\),\r?\n            \[System\.IO\.Path\]::GetFileName\(\$portableAsset\)\r?\n          \) \| Sort-Object[ \t]*$' -Label 'exact native release API asset-name expectation'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^            if \(\[string\]\$Release\.tag_name -cne \$ExpectedTag\) \{[ \t]*$' -Label 'release API exact-tag postcondition'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^            if \(\(\[string\]\$Release\.target_commitish\)\.Trim\(\)\.ToLowerInvariant\(\) -cne \$ExpectedSha\) \{[ \t]*$' -Label 'release API exact-target-SHA postcondition'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^            if \(\[bool\]\$Release\.draft\) \{[ \t]*$' -Label 'release API non-draft postcondition'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^            if \(\[bool\]\$Release\.prerelease -ne \$ExpectedPrerelease\) \{[ \t]*$' -Label 'release API exact-prerelease-mode postcondition'
Require-Regex -Content $nativeReleaseStep -Pattern '(?ms)^            \$actualAssets = @\(\$Release\.assets \| ForEach-Object \{ \[string\]\$_\.name \} \| Sort-Object\)\r?\n            if \(\$actualAssets\.Count -ne \$ExpectedAssets\.Count -or\r?\n                \(\$actualAssets\.Count -eq \$ExpectedAssets\.Count -and\r?\n                  \(Compare-Object -ReferenceObject \$ExpectedAssets -DifferenceObject \$actualAssets -CaseSensitive\)\)\) \{[ \t]*$' -Label 'release API exact two-asset postcondition'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^          git fetch --no-tags origin ''\+refs/heads/main:refs/remotes/origin/main''[ \t]*$' -Label 'fresh origin/main release-tip fetch'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^          \$currentMainSha = \(git rev-parse refs/remotes/origin/main\)\.Trim\(\)\.ToLowerInvariant\(\)[ \t]*$' -Label 'fresh origin/main tip resolution'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^          \$isMainChannel = \$env:EVENT_REF_TYPE -eq ''branch'' -and[ \t]*$' -Label 'main release channel discrimination'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^          \$isCurrentMainTip = \$isMainChannel -and \$expectedSha -eq \$currentMainSha[ \t]*$' -Label 'exact current-main-tip release gate'
Require-Regex -Content $nativeReleaseStep -Pattern '(?ms)^          if \(\$isMainChannel\) \{\r?\n            \$releaseArgs \+= ''--latest=false''\r?\n          \}\r?\n          else \{\r?\n            \$releaseArgs \+= ''--prerelease''\r?\n          \}[ \t]*$' -Label 'stable main creation without implicit Latest and branch-prerelease modes'
Require-Regex -Content $nativeReleaseStep -Pattern '(?ms)^          \$releaseArgs = @\(\r?\n            ''release'', ''create'', \$tag,\r?\n            \$portableAsset,\r?\n            \$installerAsset,\r?\n            ''--target'', \$expectedSha,\r?\n            ''--title'', \$title,\r?\n            ''--notes'', \$notes\r?\n          \)[ \t]*$' -Label 'exact native release assets and immutable target arguments'
Require-Regex -Content $nativeReleaseStep -Pattern $trustedNativeDirectCreatePattern -Label 'sole non-retried native release publisher invocation'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^              \[switch\]\$WaitForVisibility[ \t]*$' -Label 'ambiguous-create delayed-visibility mode'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^                if \(-not \$WaitForVisibility\) \{ return \$null \}[ \t]*$' -Label 'ordinary absent-release lookup behavior'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^                Write-Warning "Native release ''\$Tag'' is not visible yet on attempt \$attempt/\$\{Attempts\}\."[ \t]*$' -Label 'ambiguous-create visibility retry warning'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^              \$existingRelease = Get-ReleaseByTagWithRetry -Tag \$tag -WaitForVisibility[ \t]*$' -Label 'ambiguous-create exact-tag visibility reconciliation'
Require-Regex -Content $nativeReleaseStep -Pattern $trustedNativeLatestEditPattern -Label 'fresh-tip-gated stable current-main Latest mutation'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^          \$refJson = Invoke-GhWithRetry -Description "native release tag lookup for \$tag" `[ \t]*$' -Label 'retrying native created-tag lookup'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^          for \(\$depth = 0; \$object\.type -eq ''tag''; \$depth\+\+\) \{[ \t]*$' -Label 'native annotated-tag dereference'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^          if \(\$actualSha -ne \$expectedSha\) \{[ \t]*$' -Label 'native exact tag provenance assertion'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^          \$releaseJson = Invoke-GhWithRetry -Description "native release readback for \$tag" `[ \t]*$' -Label 'retrying created native release API readback'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^          \$expectedPrerelease = -not \$isMainChannel[ \t]*$' -Label 'branch prerelease and main stable postcondition mode'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^          if \(\$releaseErrors\.Count -ne 0\) \{[ \t]*$' -Label 'created native release fail-closed postcondition'
Require-Regex -Content $nativeReleaseStep -Pattern $trustedNativeAssetUploadPattern -Label 'idempotent missing-native-asset reconciliation'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^          function Get-NativeLatestPostconditionErrors \{[ \t]*$' -Label 'native Latest invariant validator'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^              ''\^native-v\(\?<version>\[0-9\]\+\\\.\[0-9\]\+\\\.\[0-9\]\+\)\$'',[ \t]*$' -Label 'versioned native Latest tag requirement'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^            if \(\[bool\]\$Release\.draft\) \{[ \t]*$' -Label 'Latest non-draft requirement'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^            if \(\[bool\]\$Release\.prerelease\) \{[ \t]*$' -Label 'Latest stable non-prerelease requirement'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^                ''WinForge-Native-Setup\.exe'',[ \t]*$' -Label 'Latest exact native installer asset requirement'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^                "WinForge-native-x64-\$\(\$nativeTag\.Groups\[''version''\]\.Value\)\.zip"[ \t]*$' -Label 'Latest version-matched native portable asset requirement'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^          \$latestObservationAttempts = 12[ \t]*$' -Label 'bounded Latest observation count'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^              \$latestJson = Invoke-GhWithRetry -Description "native Latest readback for \$tag" `[ \t]*$' -Label 'retrying Latest endpoint readback'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^          if \(-not \$latestVerified\) \{[ \t]*$' -Label 'Latest postcondition failure gate'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^            \$finalMainSha = \(git rev-parse refs/remotes/origin/main\)\.Trim\(\)\.ToLowerInvariant\(\)[ \t]*$' -Label 'final fresh main resolution'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^            if \(\$finalMainSha -ne \$expectedSha\) \{[ \t]*$' -Label 'newer-main ownership handoff after Latest race'
Require-Regex -Content $nativeReleaseStep -Pattern '(?m)^              exit 0[ \t]*$' -Label 'newer-main non-Latest success exit'
Reject-Regex -Content $nativeReleaseStep -Pattern '(?i)\$(?:candidate|recovery)|candidateDiscovery|recoveryCandidate|candidateTag' -Label 'obsolete cross-commit Latest recovery path'

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
Require-Regex -Content $siteDataWorkflow -Pattern '(?m)^        uses: actions/checkout@v7[ \t]*$' -Label 'Node 24 site-data checkout action'
Require-Regex -Content $siteDataWorkflow -Pattern '(?m)^        uses: actions/setup-dotnet@v6[ \t]*$' -Label 'Node 24 site-data setup-dotnet action'
Require-Regex -Content $pagesWorkflow -Pattern '(?m)^        uses: actions/checkout@v7[ \t]*$' -Label 'Node 24 Pages checkout action'
Require-Regex -Content $pagesWorkflow -Pattern '(?m)^        uses: actions/configure-pages@v6[ \t]*$' -Label 'Node 24 Pages configure action'
Require-Regex -Content $pagesWorkflow -Pattern '(?m)^        uses: actions/upload-pages-artifact@v5[ \t]*$' -Label 'Node 24 Pages artifact action'
Require-Regex -Content $pagesWorkflow -Pattern '(?m)^        uses: actions/deploy-pages@v5[ \t]*$' -Label 'Node 24 Pages deploy action'
Require-Regex -Content $siteDataWorkflow -Pattern '(?ms)^concurrency:\r?\n  group: site-data\r?\n.*?^  cancel-in-progress: false[ \t]*$' -Label 'non-canceling site-data delivery queue'
Reject-Regex -Content $siteDataCommitStep -Pattern '(?m)^          if \(-not \(git status --porcelain design/winforge-data\.js\)\) \{[ \t]*$' -Label 'pre-normalization site-data status truthiness gate'
Require-Regex -Content $siteDataCommitStep -Pattern '(?ms)^          git add -- design/winforge-data\.js[ \t]*\r?\n          if \(\$LASTEXITCODE -ne 0\) \{ throw ''site-data staging failed'' \}[ \t]*\r?\n          git diff --cached --quiet -- design/winforge-data\.js[ \t]*\r?\n          \$stagedDiffExitCode = \$LASTEXITCODE[ \t]*\r?\n          if \(\$stagedDiffExitCode -eq 0\) \{\r?\n            Write-Host "No change to design/winforge-data\.js\."\r?\n            exit 0\r?\n          \}\r?\n          if \(\$stagedDiffExitCode -ne 1\) \{\r?\n            throw "site-data staged diff check failed with exit code \$stagedDiffExitCode"\r?\n          \}[ \t]*$' -Label 'post-normalization staged site-data three-way diff gate'
Require-Regex -Content $siteDataCommitStep -Pattern '(?ms)^.*?^          git pull --rebase origin main[ \t]*\r?\n.*?^          \$pushedSha = \(git rev-parse HEAD\)\.Trim\(\)\.ToLowerInvariant\(\)[ \t]*\r?\n.*?^          git push origin HEAD:main[ \t]*$' -Label 'exact post-rebase site-data SHA push order'
Require-Regex -Content $siteDataCommitStep -Pattern '(?m)^          \$releaseVersion = "1\.0\.\$env:GITHUB_RUN_ID"[ \t]*$' -Label 'stable site-data release version'
Require-Regex -Content $siteDataCommitStep -Pattern '(?ms)^          \$dispatchArgs = @\(\r?\n            ''workflow'', ''run'', ''native-release\.yml'',\r?\n            ''--ref'', ''main'',\r?\n            ''-f'', ''publish_release=true'',\r?\n            ''-f'', "source_sha=\$pushedSha",\r?\n            ''-f'', "release_version=\$releaseVersion"\r?\n          \)[ \t]*$' -Label 'exact site-data native release dispatch arguments'
Require-Regex -Content $siteDataCommitStep -Pattern '(?m)^          for \(\$attempt = 1; \$attempt -le 5; \$attempt\+\+\) \{[ \t]*$' -Label 'bounded site-data native-release dispatch retry'
Require-Regex -Content $siteDataCommitStep -Pattern '(?m)^            \$dispatchOutput = \(& gh @dispatchArgs 2>&1 \| Out-String\)[ \t]*$' -Label 'site-data dispatch command'
Require-Regex -Content $siteDataCommitStep -Pattern '(?m)^            Write-Warning "Native release dispatch attempt \$attempt/5 failed: \$lastDispatchFailure"[ \t]*$' -Label 'site-data dispatch retry diagnostic'
Require-Regex -Content $siteDataCommitStep -Pattern '(?m)^            throw "native release dispatch failed after 5 attempts: \$lastDispatchFailure"[ \t]*$' -Label 'site-data dispatch fails closed'

$siteDataStagedDiffFixtures = @(
    [pscustomobject]@{ Name = 'normalized no-op exits cleanly'; ExitCode = 0; Outcome = 'skip' }
    [pscustomobject]@{ Name = 'real staged data delta commits'; ExitCode = 1; Outcome = 'commit' }
    [pscustomobject]@{ Name = 'staged diff command error fails closed'; ExitCode = 2; Outcome = 'fail' }
)
foreach ($fixture in $siteDataStagedDiffFixtures) {
    $outcome = if ($fixture.ExitCode -eq 0) { 'skip' } elseif ($fixture.ExitCode -eq 1) { 'commit' } else { 'fail' }
    if ($outcome -cne $fixture.Outcome) {
        throw "Site-data staged diff self-test failed for $($fixture.Name): expected $($fixture.Outcome), got $outcome"
    }
}

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
