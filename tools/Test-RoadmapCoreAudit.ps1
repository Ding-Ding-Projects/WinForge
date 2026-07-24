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
    [pscustomobject]@{ Name = 'Browser Control'; RoadmapPrefix = '### Browser Control '; AuditPrefix = '## Browser Control '; Total = 14; Shipped = 3 }
)

$roadmapPath = Join-Path $RepoRoot 'docs\ROADMAP.md'
$auditPath = Join-Path $RepoRoot 'docs\audits\roadmap-core-capability-audit-2026-07-24.md'
$indexPath = Join-Path $RepoRoot 'docs\audits\README.md'
$wikiPath = Join-Path $RepoRoot 'docs\wiki\Roadmap-Core-Capability-Audit.md'
$pagesPath = Join-Path $RepoRoot 'design\content\wiki\Roadmap-Core-Capability-Audit.md'

foreach ($path in @($roadmapPath, $auditPath, $indexPath, $wikiPath, $pagesPath)) {
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

if ($aggregateTotal -ne 115 -or $aggregateShipped -ne 74) {
    throw "Aggregate mismatch: expected 74/115 shipped, found $aggregateShipped/$aggregateTotal."
}

$artifactText = @(
    Get-Content -LiteralPath $indexPath -Raw -Encoding UTF8
    Get-Content -LiteralPath $wikiPath -Raw -Encoding UTF8
    Get-Content -LiteralPath $pagesPath -Raw -Encoding UTF8
) -join "`n"
if ($artifactText -notmatch '74/115' -or $artifactText -notmatch '41') {
    throw 'Audit index/wiki mirrors do not report the 74/115 shipped and 41-gap result.'
}

$rows | Format-Table -AutoSize
Write-Host "Roadmap core audit passed: $aggregateShipped/$aggregateTotal shipped; $($aggregateTotal - $aggregateShipped) factual gaps retained."
