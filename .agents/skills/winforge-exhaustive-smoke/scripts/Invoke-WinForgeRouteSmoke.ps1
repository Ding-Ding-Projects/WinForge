[CmdletBinding()]
param(
    [Parameter()]
    [string]$ManifestPath = 'artifacts\smoke\baseline\manifest.json',

    [Parameter()]
    [string]$OutputDirectory,

    [Parameter()]
    [int]$WaitMs = 2500,

    [Parameter()]
    [int]$StartIndex = 0,

    [Parameter()]
    [int]$Count = 25,

    [string[]]$RouteId
)

$ErrorActionPreference = 'Stop'

function Resolve-FromRepo {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Root
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    }

    return Join-Path $Root $Path
}

function Select-CanonicalAlias {
    param([Parameter(Mandatory = $true)]$Route)

    $aliases = @($Route.aliases | Where-Object { $_ } | Sort-Object -Unique)
    if ($aliases.Count -eq 0) {
        return $null
    }

    $preferred = if ($Route.id -like 'module.*') {
        $Route.id.Substring('module.'.Length)
    }
    else {
        $Route.id
    }

    $exact = @($aliases | Where-Object { $_ -eq $preferred } | Select-Object -First 1)
    if ($exact.Count -gt 0) {
        return $exact[0]
    }

    return $aliases[0]
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$manifestFile = Resolve-FromRepo -Path $ManifestPath -Root $repoRoot
if (-not (Test-Path -LiteralPath $manifestFile -PathType Leaf)) {
    throw "Manifest was not found: $manifestFile"
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $OutputDirectory = Join-Path $repoRoot "artifacts\smoke\launch-batches\$stamp"
}
else {
    $OutputDirectory = Resolve-FromRepo -Path $OutputDirectory -Root $repoRoot
}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$driver = Join-Path $repoRoot '.agents\skills\run-winforge\driver.ps1'
if (-not (Test-Path -LiteralPath $driver -PathType Leaf)) {
    throw "run-winforge driver was not found: $driver"
}

$manifest = Get-Content -LiteralPath $manifestFile -Raw | ConvertFrom-Json
$candidates = @(
    $manifest.routes |
        Sort-Object id |
        ForEach-Object {
            $alias = Select-CanonicalAlias -Route $_
            [pscustomobject]@{
                id = $_.id
                kind = $_.kind
                alias = $alias
                source = @($_.source) -join ';'
                staticRouting = @($_.routing) -join ';'
            }
        }
)

$requestedIds = @()
if ($RouteId -and $RouteId.Count -gt 0) {
    $requestedIds = @(
        $RouteId |
            ForEach-Object { $_ -split ',' } |
            ForEach-Object { $_.Trim() } |
            Where-Object { $_ }
    )
    $candidates = @($candidates | Where-Object { $requestedIds -contains $_.id })
}

$skipped = @($candidates | Where-Object { [string]::IsNullOrWhiteSpace($_.alias) })
$runnable = @($candidates | Where-Object { -not [string]::IsNullOrWhiteSpace($_.alias) })
if ($StartIndex -lt 0 -or $StartIndex -ge $runnable.Count) {
    throw "StartIndex $StartIndex is outside the $($runnable.Count) launchable routes."
}
if ($Count -lt 1) {
    $Count = $runnable.Count - $StartIndex
}

$batch = @($runnable | Select-Object -Skip $StartIndex -First $Count)
$results = @()
for ($index = 0; $index -lt $batch.Count; $index++) {
    $item = $batch[$index]
    $safeId = $item.id -replace '[^A-Za-z0-9._-]', '_'
    $logPath = Join-Path $OutputDirectory "$safeId.log"
    $startedUtc = (Get-Date).ToUniversalTime().ToString('o')
    Write-Progress -Activity 'WinForge route launch smoke' -Status "$($index + 1)/$($batch.Count): $($item.id)" -PercentComplete ((($index + 1) / $batch.Count) * 100)

    $output = @(& powershell -NoProfile -ExecutionPolicy Bypass -File $driver -Page $item.alias -NoCapture -WaitMs $WaitMs 2>&1)
    $exitCode = $LASTEXITCODE
    $text = ($output | Out-String).TrimEnd()
    $text | Set-Content -LiteralPath $logPath -Encoding UTF8

    $results += [pscustomobject]@{
        id = $item.id
        kind = $item.kind
        alias = $item.alias
        source = $item.source
        staticRouting = $item.staticRouting
        startedUtc = $startedUtc
        waitMs = $WaitMs
        exitCode = $exitCode
        status = if ($exitCode -eq 0) { 'launch-pass' } else { 'failed' }
        evidence = "launch-only driver; log=$([System.IO.Path]::GetFileName($logPath))"
        log = [System.IO.Path]::GetFileName($logPath)
    }
}
Write-Progress -Activity 'WinForge route launch smoke' -Completed

foreach ($item in $skipped) {
    $results += [pscustomobject]@{
        id = $item.id
        kind = $item.kind
        alias = $null
        source = $item.source
        staticRouting = $item.staticRouting
        startedUtc = $null
        waitMs = 0
        exitCode = $null
        status = 'not-launchable'
        evidence = 'No ApplyStartPage alias was discovered; inspect routing manually.'
        log = $null
    }
}

$resultsPath = Join-Path $OutputDirectory 'results.json'
$csvPath = Join-Path $OutputDirectory 'results.csv'
$summaryPath = Join-Path $OutputDirectory 'summary.md'
$results | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $resultsPath -Encoding UTF8
$results | Export-Csv -LiteralPath $csvPath -NoTypeInformation -Encoding UTF8

$passed = @($results | Where-Object { $_.status -eq 'launch-pass' }).Count
$failed = @($results | Where-Object { $_.status -eq 'failed' }).Count
$notLaunchable = @($results | Where-Object { $_.status -eq 'not-launchable' }).Count
$summary = @(
    '# WinForge launch-only smoke batch',
    '',
    "Manifest: $manifestFile",
    "Batch: launchable routes $StartIndex through $($StartIndex + $batch.Count - 1)",
    "Wait per route: $WaitMs ms",
    '',
    '## Results',
    '',
    "| Launch pass | $passed |",
    "| Failed | $failed |",
    "| Not launchable from a discovered alias | $notLaunchable |",
    '',
    'This is launch-only evidence. It intentionally does not claim visual or behavioral verification.'
)
if ($failed -gt 0) {
    $summary += @('', '## Failures', '')
    foreach ($failure in ($results | Where-Object { $_.status -eq 'failed' } | Sort-Object id)) {
        $summary += "- $($failure.id) via $($failure.alias): $($failure.log)"
    }
}
if ($notLaunchable -gt 0) {
    $summary += @('', '## Manual routing review', '')
    foreach ($item in ($results | Where-Object { $_.status -eq 'not-launchable' } | Sort-Object id)) {
        $summary += "- $($item.id)"
    }
}
$summary | Set-Content -LiteralPath $summaryPath -Encoding UTF8

Write-Host "Launch-only smoke batch complete:"
Write-Host "  $resultsPath"
Write-Host "  $csvPath"
Write-Host "  $summaryPath"
Write-Host "Launch pass: $passed; failed: $failed; not launchable: $notLaunchable"
if ($failed -gt 0) {
    exit 1
}
