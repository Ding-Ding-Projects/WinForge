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
    [pscustomobject]@{ Name = 'Media'; RoadmapPrefix = '### Media '; AuditPrefix = '## Media '; Total = 15; Shipped = 15 },
    [pscustomobject]@{ Name = 'Maintenance'; RoadmapPrefix = '### Maintenance '; AuditPrefix = '## Maintenance '; Total = 15; Shipped = 10 },
    [pscustomobject]@{ Name = 'Dev & Terminal'; RoadmapPrefix = '### Dev & Terminal '; AuditPrefix = '## Dev & Terminal '; Total = 15; Shipped = 15 },
    [pscustomobject]@{ Name = 'Home Assistant'; RoadmapPrefix = '### Home Assistant '; AuditPrefix = '## Home Assistant '; Total = 14; Shipped = 14 },
    [pscustomobject]@{ Name = 'Archives'; RoadmapPrefix = '### Archives '; AuditPrefix = '## Archives '; Total = 14; Shipped = 14 },
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
$mediaCorePath = Join-Path $RepoRoot 'Services\MediaWorkflowCore.cs'
$mediaPagePath = Join-Path $RepoRoot 'Pages\MediaModule.xaml'
$mediaPageCodePath = Join-Path $RepoRoot 'Pages\MediaModule.xaml.cs'
$mediaTestPath = Join-Path $RepoRoot 'tests\MediaWorkflowCore.Tests\Program.cs'
$mediaGuidePath = Join-Path $RepoRoot 'docs\features\media-capture\media-studio-workflows.md'
$roadmapWorkflowTestPath = Join-Path $RepoRoot 'tests\RoadmapWorkflowCore.Tests\Program.cs'
$developerCorePath = Join-Path $RepoRoot 'Services\DeveloperWorkflowCore.cs'
$developerServicePath = Join-Path $RepoRoot 'Services\DeveloperWorkflowService.cs'
$developerPanelPath = Join-Path $RepoRoot 'Controls\DeveloperWorkflowPanel.xaml'
$developerPanelCodePath = Join-Path $RepoRoot 'Controls\DeveloperWorkflowPanel.xaml.cs'
$developerGuidePath = Join-Path $RepoRoot 'docs\features\developer-terminal\developer-workflow-workbench.md'
$archiveCorePath = Join-Path $RepoRoot 'Services\ArchiveWorkflowCore.cs'
$archiveServicePath = Join-Path $RepoRoot 'Services\ArchiveWorkflowService.cs'
$archivePagePath = Join-Path $RepoRoot 'Pages\ArchivesModule.xaml'
$archivePageCodePath = Join-Path $RepoRoot 'Pages\ArchivesModule.xaml.cs'
$archiveGuidePath = Join-Path $RepoRoot 'docs\features\archives\safe-create-and-delete.md'
$homeAssistantGatePath = Join-Path $RepoRoot 'Services\HomeAssistantRestartGate.cs'
$homeAssistantPageCodePath = Join-Path $RepoRoot 'Pages\HomeAssistantModule.xaml.cs'
$homeAssistantGuidePath = Join-Path $RepoRoot 'docs\features\home-assistant\validated-restart.md'

foreach ($path in @($roadmapPath, $auditPath, $indexPath, $wikiPath, $pagesPath, $mediaCatalogPath,
        $browserCorePath, $browserServicePath, $browserPanelPath, $browserPanelCodePath, $browserTestPath, $browserDocsPath,
        $mediaCorePath, $mediaPagePath, $mediaPageCodePath, $mediaTestPath, $mediaGuidePath,
        $roadmapWorkflowTestPath, $developerCorePath, $developerServicePath, $developerPanelPath, $developerPanelCodePath, $developerGuidePath,
        $archiveCorePath, $archiveServicePath, $archivePagePath, $archivePageCodePath, $archiveGuidePath,
        $homeAssistantGatePath, $homeAssistantPageCodePath, $homeAssistantGuidePath)) {
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

if ($aggregateTotal -ne 115 -or $aggregateShipped -ne 107) {
    throw "Aggregate mismatch: expected 107/115 shipped, found $aggregateShipped/$aggregateTotal."
}

$artifactText = @(
    Get-Content -LiteralPath $indexPath -Raw -Encoding UTF8
    Get-Content -LiteralPath $wikiPath -Raw -Encoding UTF8
    Get-Content -LiteralPath $pagesPath -Raw -Encoding UTF8
) -join "`n"
if ($artifactText -notmatch '107/115' -or $artifactText -notmatch '8 gaps|8-gap|8 remaining|8 項') {
    throw 'Audit index/wiki mirrors do not report the 107/115 shipped and 8-gap result.'
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

$mediaCoreText = Get-Content -LiteralPath $mediaCorePath -Raw -Encoding UTF8
$mediaPageText = Get-Content -LiteralPath $mediaPagePath -Raw -Encoding UTF8
$mediaPageCodeText = Get-Content -LiteralPath $mediaPageCodePath -Raw -Encoding UTF8
$mediaTestText = Get-Content -LiteralPath $mediaTestPath -Raw -Encoding UTF8
$requiredCoreTokens = @(
    'NormalizeLoudnessAsync', 'measured_I=', 'silenceremove=start_periods=1',
    'vidstabdetect=', 'vidstabtransform=', 'cropdetect=round=2', 'BuildConcatListContent',
    'h264_nvenc', 'hevc_nvenc', 'av1_nvenc', 'ComputeTargetVideoBitrateKbps',
    'MediaSubtitleMode', '-show_chapters', 'ConvertPhotoBatchAsync', '-map_metadata:s'
)
foreach ($token in $requiredCoreTokens) {
    if (-not $mediaCoreText.Contains($token)) {
        throw "Media workflow implementation evidence is missing token: $token"
    }
}
$requiredPageControls = @(
    'NormalizeR128Btn', 'TrimSilenceBtn', 'StabilizeBtn', 'AutoCropBtn', 'ChooseConcatBtn',
    'DetectNvencBtn', 'TargetSizeBtn', 'SubtitleRunBtn', 'ReadChaptersBtn',
    'ConvertPhotosBtn', 'StripMetadataBtn'
)
foreach ($control in $requiredPageControls) {
    if (-not $mediaPageText.Contains("x:Name=`"$control`"") -or -not $mediaPageCodeText.Contains("$($control.Replace('Btn', ''))_Click")) {
        throw "Media workflow reachable-control evidence is missing or unwired: $control"
    }
}
if ($mediaTestText -notmatch '17/17 tests passed|tests\.Length' -or
    -not $mediaTestText.Contains('cancellation removes owned workspace and staged files') -or
    -not $mediaTestText.Contains('failure preserves a pre-existing destination')) {
    throw 'Media focused harness no longer preserves its sequencing, cancellation, and staged-output contracts.'
}

$roadmapWorkflowTestText = Get-Content -LiteralPath $roadmapWorkflowTestPath -Raw -Encoding UTF8
$developerEvidenceText = @(
    Get-Content -LiteralPath $developerCorePath -Raw -Encoding UTF8
    Get-Content -LiteralPath $developerServicePath -Raw -Encoding UTF8
    Get-Content -LiteralPath $developerPanelPath -Raw -Encoding UTF8
    Get-Content -LiteralPath $developerPanelCodePath -Raw -Encoding UTF8
) -join "`n"
foreach ($marker in @(
        'InspectPortAsync', 'TerminateReviewedListenersAsync', 'BuildNodeShellPlan',
        'BuildCorepackPreparePlan', 'BuildDefenderMutationScript', 'BuildTcpTuningScript',
        'InspectCachesAsync', 'BuildCacheCleanPlan', 'DeveloperWorkflowCachePnpm',
        'DeveloperWorkflowCachePip', 'DeveloperWorkflowCacheDocker')) {
    if (-not $developerEvidenceText.Contains($marker)) {
        throw "Developer workflow evidence is missing marker: $marker"
    }
}

$archiveEvidenceText = @(
    Get-Content -LiteralPath $archiveCorePath -Raw -Encoding UTF8
    Get-Content -LiteralPath $archiveServicePath -Raw -Encoding UTF8
    Get-Content -LiteralPath $archivePagePath -Raw -Encoding UTF8
    Get-Content -LiteralPath $archivePageCodePath -Raw -Encoding UTF8
) -join "`n"
foreach ($marker in @(
        'ArchiveDeleteMasks', 'ArchiveMoveSourceAfterTest', 'BuildDeleteArguments',
        '-ir!', '-xr!', '-mtc=on', '-mta=on', '-mtm=on', '-ssp',
        'MoveToRecycleBin', 'IntegrityArguments')) {
    if (-not $archiveEvidenceText.Contains($marker)) {
        throw "Archive workflow evidence is missing marker: $marker"
    }
}
if ($archiveEvidenceText.Contains('arguments.Add("-sdel")')) {
    throw 'Archive move workflow must not bypass the separate integrity gate with -sdel.'
}

$homeAssistantEvidenceText = @(
    Get-Content -LiteralPath $homeAssistantGatePath -Raw -Encoding UTF8
    Get-Content -LiteralPath $homeAssistantPageCodePath -Raw -Encoding UTF8
) -join "`n"
foreach ($marker in @('RecordCheck', 'CanRestart', 'FixedTimeEquals', 'CheckConfig', '_restartGate.Consume')) {
    if (-not $homeAssistantEvidenceText.Contains($marker)) {
        throw "Home Assistant restart-gate evidence is missing marker: $marker"
    }
}
if ($roadmapWorkflowTestText -notmatch 'Roadmap workflow contract passed' -or
    -not $roadmapWorkflowTestText.Contains('archive integrity test targets the first split volume') -or
    -not $roadmapWorkflowTestText.Contains('Home Assistant restart consumes validation')) {
    throw 'Roadmap workflow focused harness no longer covers the completion safety contracts.'
}

$rows | Format-Table -AutoSize
Write-Host "Roadmap core audit passed: $aggregateShipped/$aggregateTotal shipped; $($aggregateTotal - $aggregateShipped) factual gaps retained."
