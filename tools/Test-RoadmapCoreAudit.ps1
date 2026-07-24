[CmdletBinding()]
param(
    [Parameter()]
    [string]$RepoRoot = ''
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
    $RepoRoot = (Resolve-Path (Join-Path $scriptDirectory '..')).Path
}

function Get-SectionBounds {
    param(
        [Parameter(Mandatory = $true)][AllowEmptyString()][string[]]$Lines,
        [Parameter(Mandatory = $true)][string]$HeadingPrefix,
        [Parameter(Mandatory = $true)][string]$NextHeadingPrefix
    )

    $start = -1
    for ($i = 0; $i -lt $Lines.Count; $i++) {
        if ($Lines[$i].StartsWith($HeadingPrefix, [StringComparison]::Ordinal)) {
            $start = $i
            break
        }
    }
    if ($start -lt 0) {
        throw "Missing required heading prefix: $HeadingPrefix"
    }

    $end = $Lines.Count
    for ($i = $start + 1; $i -lt $Lines.Count; $i++) {
        if ($Lines[$i].StartsWith($NextHeadingPrefix, [StringComparison]::Ordinal)) {
            $end = $i
            break
        }
    }

    return [pscustomobject]@{ Start = $start; End = $end }
}

function Find-RelativeLine {
    param(
        [Parameter(Mandatory = $true)][AllowEmptyString()][string[]]$Lines,
        [Parameter(Mandatory = $true)][string]$StartsWith
    )

    for ($i = 0; $i -lt $Lines.Count; $i++) {
        if ($Lines[$i].StartsWith($StartsWith, [StringComparison]::Ordinal)) {
            return $i
        }
    }
    return -1
}

$specs = @(
    [pscustomobject]@{ Name = 'Windows 11'; RoadmapPrefix = '### Windows 11  ('; AuditPrefix = '## Windows 11 '; Total = 13; Shipped = 10 },
    [pscustomobject]@{ Name = 'ViveTool'; RoadmapPrefix = '### ViveTool '; AuditPrefix = '## ViveTool '; Total = 15; Shipped = 15 },
    [pscustomobject]@{ Name = 'Media'; RoadmapPrefix = '### Media '; AuditPrefix = '## Media '; Total = 15; Shipped = 4 },
    [pscustomobject]@{ Name = 'Maintenance'; RoadmapPrefix = '### Maintenance '; AuditPrefix = '## Maintenance '; Total = 15; Shipped = 10 },
    [pscustomobject]@{ Name = 'Dev & Terminal'; RoadmapPrefix = '### Dev & Terminal '; AuditPrefix = '## Dev & Terminal '; Total = 15; Shipped = 9 },
    [pscustomobject]@{ Name = 'Home Assistant'; RoadmapPrefix = '### Home Assistant '; AuditPrefix = '## Home Assistant '; Total = 14; Shipped = 13 },
    [pscustomobject]@{ Name = 'Archives'; RoadmapPrefix = '### Archives '; AuditPrefix = '## Archives '; Total = 14; Shipped = 10 },
    [pscustomobject]@{ Name = 'Browser Control'; RoadmapPrefix = '### Browser Control '; AuditPrefix = '## Browser Control '; Total = 14; Shipped = 14 }
)

$roadmapPath = Join-Path $RepoRoot 'docs\ROADMAP.md'
$auditPath = Join-Path $RepoRoot 'docs\audits\roadmap-core-capability-audit-2026-07-24.md'
$indexPath = Join-Path $RepoRoot 'docs\audits\README.md'
$wikiPath = Join-Path $RepoRoot 'docs\wiki\Roadmap-Core-Capability-Audit.md'
$pagesPath = Join-Path $RepoRoot 'design\content\wiki\Roadmap-Core-Capability-Audit.md'
$mediaCatalogPath = Join-Path $RepoRoot 'Catalog\MediaOperations.cs'
$browserCorePath = Join-Path $RepoRoot 'Services\BrowserControlCore.cs'
$browserServicePath = Join-Path $RepoRoot 'Services\BrowserControlService.cs'
$browserPanelPath = Join-Path $RepoRoot 'Controls\BrowserControlPanel.xaml'
$browserPanelCodePath = Join-Path $RepoRoot 'Controls\BrowserControlPanel.xaml.cs'
$browserTestPath = Join-Path $RepoRoot 'tests\BrowserControl.Tests\Program.cs'
$browserDocsPath = Join-Path $RepoRoot 'docs\features\browser-control\browser-workbench.md'

foreach ($path in @($roadmapPath, $auditPath, $indexPath, $wikiPath, $pagesPath, $mediaCatalogPath,
        $browserCorePath, $browserServicePath, $browserPanelPath, $browserPanelCodePath, $browserTestPath, $browserDocsPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required audit artifact is missing: $path"
    }
}

$roadmapLines = @(Get-Content -LiteralPath $roadmapPath -Encoding UTF8)
$auditLines = @(Get-Content -LiteralPath $auditPath -Encoding UTF8)
$rows = [System.Collections.Generic.List[object]]::new()
$aggregateTotal = 0
$aggregateShipped = 0

foreach ($spec in $specs) {
    $roadmapBounds = Get-SectionBounds -Lines $roadmapLines -HeadingPrefix $spec.RoadmapPrefix -NextHeadingPrefix '### '
    $roadmapSection = @($roadmapLines[($roadmapBounds.Start + 1)..($roadmapBounds.End - 1)])
    $items = [System.Collections.Generic.List[object]]::new()

    foreach ($line in $roadmapSection) {
        $match = [regex]::Match($line, '^- \[(?<mark>[ xX])\] \*\*(?<title>.+?)\*\*')
        if ($match.Success) {
            $items.Add([pscustomobject]@{
                Checked = $match.Groups['mark'].Value -match '[xX]'
                Title = $match.Groups['title'].Value
            })
        }
    }

    $checked = @($items | Where-Object Checked).Count
    if ($items.Count -ne $spec.Total) {
        throw "$($spec.Name): expected $($spec.Total) items, found $($items.Count)."
    }
    if ($checked -ne $spec.Shipped) {
        throw "$($spec.Name): expected $($spec.Shipped) shipped items, found $checked."
    }
    if (($roadmapSection -join "`n") -notmatch 'roadmap-core-capability-audit-2026-07-24\.md') {
        throw "$($spec.Name): missing categorized audit link."
    }

    $auditBounds = Get-SectionBounds -Lines $auditLines -HeadingPrefix $spec.AuditPrefix -NextHeadingPrefix '## '
    $auditSection = @($auditLines[($auditBounds.Start + 1)..($auditBounds.End - 1)])
    $shippedStart = Find-RelativeLine -Lines $auditSection -StartsWith '### Shipped'
    $gapsStart = Find-RelativeLine -Lines $auditSection -StartsWith '### Remaining gaps'
    if ($shippedStart -lt 0 -or $gapsStart -le $shippedStart) {
        throw "$($spec.Name): shipped/gap evidence headings are missing or out of order."
    }
    if ($auditSection[$shippedStart] -notmatch "Shipped .+ $($spec.Shipped)(?:\D|$)") {
        throw "$($spec.Name): shipped heading does not report $($spec.Shipped)."
    }
    $expectedGaps = $spec.Total - $spec.Shipped
    if ($auditSection[$gapsStart] -notmatch "Remaining gaps .+ $expectedGaps(?:\D|$)") {
        throw "$($spec.Name): gap heading does not report $expectedGaps."
    }

    foreach ($item in $items) {
        $needle = "**$($item.Title)**"
        $found = [System.Collections.Generic.List[int]]::new()
        for ($i = 0; $i -lt $auditSection.Count; $i++) {
            if ($auditSection[$i].IndexOf($needle, [StringComparison]::Ordinal) -ge 0) {
                $found.Add($i)
            }
        }
        if ($found.Count -ne 1) {
            throw "$($spec.Name): expected exactly one evidence row for '$($item.Title)', found $($found.Count)."
        }
        if ($item.Checked -and ($found[0] -le $shippedStart -or $found[0] -ge $gapsStart)) {
            throw "$($spec.Name): checked item '$($item.Title)' is not in the shipped evidence block."
        }
        if (-not $item.Checked -and $found[0] -le $gapsStart) {
            throw "$($spec.Name): unchecked item '$($item.Title)' is not in the remaining-gap block."
        }
    }

    $aggregateTotal += $items.Count
    $aggregateShipped += $checked
    $rows.Add([pscustomobject]@{
        Section = $spec.Name
        Audited = $items.Count
        Shipped = $checked
        Remaining = $items.Count - $checked
    })
}

if ($aggregateTotal -ne 115 -or $aggregateShipped -ne 85) {
    throw "Aggregate mismatch: expected 85/115 shipped, found $aggregateShipped/$aggregateTotal."
}

$artifactText = @(
    Get-Content -LiteralPath $indexPath -Raw -Encoding UTF8
    Get-Content -LiteralPath $wikiPath -Raw -Encoding UTF8
    Get-Content -LiteralPath $pagesPath -Raw -Encoding UTF8
) -join "`n"
if ($artifactText -notmatch '85/115' -or $artifactText -notmatch '30') {
    throw 'Audit index/wiki mirrors do not report the 85/115 shipped and 30-gap result.'
}

$mediaCatalogText = Get-Content -LiteralPath $mediaCatalogPath -Raw -Encoding UTF8
$mediaEvidenceText = @(
    Get-Content -LiteralPath $roadmapPath -Raw -Encoding UTF8
    Get-Content -LiteralPath $auditPath -Raw -Encoding UTF8
) -join "`n"
if (-not $mediaCatalogText.Contains('fps=15,scale=480:-1:flags=lanczos') -or
    -not $mediaCatalogText.Contains('-c:v libwebp -loop 0')) {
    throw 'Animated WebP source command no longer matches the audited libwebp/fps/scale/loop evidence.'
}
if ($mediaEvidenceText -match 'libwebp_anim|fps=20,scale=600|-q:v\s+70') {
    throw 'Roadmap/audit Media evidence contains the superseded animated-WebP encoder or quality claim.'
}

$browserCoreText = Get-Content -LiteralPath $browserCorePath -Raw -Encoding UTF8
$browserServiceText = Get-Content -LiteralPath $browserServicePath -Raw -Encoding UTF8
$browserPanelText = Get-Content -LiteralPath $browserPanelPath -Raw -Encoding UTF8
$browserPanelCodeText = Get-Content -LiteralPath $browserPanelCodePath -Raw -Encoding UTF8
$browserTestText = Get-Content -LiteralPath $browserTestPath -Raw -Encoding UTF8
foreach ($marker in @(
        'BuildAppModePlan', 'BuildKioskPlan', 'DiscoverProfiles', 'DiscoverPwas',
        'BuildInternalPagePlan', 'ClearProfileCaches', '--proxy-bypass-list=',
        'CreateEphemeralDirectory', '--enable-features=', '--disable-features=',
        '--remote-debugging-address=127.0.0.1', 'BuildWingetPlan')) {
    if (-not $browserCoreText.Contains($marker)) {
        throw "Browser Control core is missing capability marker: $marker"
    }
}
foreach ($marker in @('ShellRunner.RunArguments', 'OwnedSessions', 'TryDeleteEphemeralDirectory')) {
    if (-not $browserServiceText.Contains($marker)) {
        throw "Browser Control executor is missing safety marker: $marker"
    }
}
foreach ($marker in @(
        'BrowserWorkbenchAppMode', 'BrowserWorkbenchKiosk', 'BrowserWorkbenchProfile',
        'BrowserWorkbenchPwa', 'BrowserWorkbenchPolicy', 'BrowserWorkbenchClearCache',
        'BrowserWorkbenchProxy', 'BrowserWorkbenchThrowaway', 'BrowserWorkbenchFeatureNames',
        'BrowserWorkbenchRemoteDebug', 'BrowserWorkbenchWingetInstall', 'BrowserWorkbenchWingetUpgrade')) {
    if (-not $browserPanelText.Contains($marker)) {
        throw "Browser Control panel is missing reachable automation marker: $marker"
    }
}
foreach ($handler in @(
        'AppMode_Click', 'Kiosk_Click', 'Profile_Click', 'LaunchPwa_Click', 'Policy_Click',
        'ClearCache_Click', 'Proxy_Click', 'Throwaway_Click', 'Feature_Click', 'Debug_Click',
        'Install_Click', 'Upgrade_Click')) {
    if (-not $browserPanelCodeText.Contains($handler)) {
        throw "Browser Control panel code is missing handler: $handler"
    }
}
if ($browserTestText -notmatch 'Browser Control contract passed' -or $browserTestText -notmatch 'CacheRequiresClosedBrowser') {
    throw 'Browser Control focused harness no longer covers its completion contract.'
}

$rows | Format-Table -AutoSize
Write-Host "Roadmap core audit passed: $aggregateShipped/$aggregateTotal shipped; $($aggregateTotal - $aggregateShipped) factual gaps retained."
