[CmdletBinding()]
param(
    [Parameter()]
    [string]$ManifestPath = 'artifacts\smoke\baseline\manifest.json',

    [Parameter()]
    [string]$OutputDirectory,

    [Parameter()]
    [int]$WaitMs = 5000,

    [Parameter()]
    [int]$RetryWaitMs = 15000,

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

function Invoke-DriverLaunchAttempt {
    param(
        [Parameter(Mandatory = $true)][string]$DriverPath,
        [Parameter(Mandatory = $true)][string]$Alias,
        [Parameter(Mandatory = $true)][int]$AttemptWaitMs
    )

    $previousErrorActionPreference = $ErrorActionPreference
    try {
        # A failed child route must become ledger evidence, not abort an entire
        # batch through PowerShell's native-command error promotion.
        $ErrorActionPreference = 'Continue'
        $output = @(& powershell -NoProfile -ExecutionPolicy Bypass -File $DriverPath -Page $Alias -NoCapture -WaitMs $AttemptWaitMs 2>&1)
        $exitCode = $LASTEXITCODE
    }
    catch {
        $output = @($_)
        $exitCode = 1
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    return [pscustomobject]@{
        waitMs = $AttemptWaitMs
        exitCode = $exitCode
        output = @($output)
    }
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
if ($WaitMs -lt 1) {
    throw 'WaitMs must be at least 1.'
}
if ($RetryWaitMs -lt 0) {
    throw 'RetryWaitMs cannot be negative.'
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
                expectedSurface = $_.expectedSurface
                launchDisposition = $_.launchDisposition
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

    $attempts = @(
        Invoke-DriverLaunchAttempt -DriverPath $driver -Alias $item.alias -AttemptWaitMs $WaitMs
    )
    if ($attempts[0].exitCode -ne 0 -and $RetryWaitMs -gt $WaitMs) {
        $attempts += Invoke-DriverLaunchAttempt -DriverPath $driver -Alias $item.alias -AttemptWaitMs $RetryWaitMs
    }

    $finalAttempt = $attempts[$attempts.Count - 1]
    $retried = $attempts.Count -gt 1
    $text = @(
        for ($attemptIndex = 0; $attemptIndex -lt $attempts.Count; $attemptIndex++) {
            $attempt = $attempts[$attemptIndex]
            "===== attempt $($attemptIndex + 1) / $($attempts.Count); wait=$($attempt.waitMs) ms; exit=$($attempt.exitCode) ====="
            ($attempt.output | Out-String).TrimEnd()
        }
    ) -join [Environment]::NewLine
    $text | Set-Content -LiteralPath $logPath -Encoding UTF8

    $evidencePrefix = if ($item.kind -eq 'shell-dialog') {
        "launch-only shell dialog route; expected=$($item.expectedSurface)"
    }
    else {
        'launch-only driver'
    }

    $results += [pscustomobject]@{
        id = $item.id
        kind = $item.kind
        alias = $item.alias
        expectedSurface = $item.expectedSurface
        launchDisposition = $item.launchDisposition
        source = $item.source
        staticRouting = $item.staticRouting
        startedUtc = $startedUtc
        waitMs = $finalAttempt.waitMs
        initialWaitMs = $attempts[0].waitMs
        retryWaitMs = if ($retried) { $finalAttempt.waitMs } else { $null }
        attemptCount = $attempts.Count
        initialExitCode = $attempts[0].exitCode
        exitCode = $finalAttempt.exitCode
        status = if ($finalAttempt.exitCode -eq 0) { 'launch-pass' } else { 'failed' }
        evidence = if ($retried) {
            "$evidencePrefix; initial exit=$($attempts[0].exitCode) at $($attempts[0].waitMs)ms; retry exit=$($finalAttempt.exitCode) at $($finalAttempt.waitMs)ms; log=$([System.IO.Path]::GetFileName($logPath))"
        }
        else {
            "$evidencePrefix; log=$([System.IO.Path]::GetFileName($logPath))"
        }
        log = [System.IO.Path]::GetFileName($logPath)
    }
}
Write-Progress -Activity 'WinForge route launch smoke' -Completed

foreach ($item in $skipped) {
    $results += [pscustomobject]@{
        id = $item.id
        kind = $item.kind
        alias = $null
        expectedSurface = $item.expectedSurface
        launchDisposition = $item.launchDisposition
        source = $item.source
        staticRouting = $item.staticRouting
        startedUtc = $null
        waitMs = 0
        initialWaitMs = 0
        retryWaitMs = $null
        attemptCount = 0
        initialExitCode = $null
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
$passedAfterRetry = @($results | Where-Object { $_.status -eq 'launch-pass' -and $_.attemptCount -gt 1 }).Count
$failed = @($results | Where-Object { $_.status -eq 'failed' }).Count
$notLaunchable = @($results | Where-Object { $_.status -eq 'not-launchable' }).Count
$summary = @(
    '# WinForge launch-only smoke batch',
    '',
    "Manifest: $manifestFile",
    "Batch: launchable routes $StartIndex through $($StartIndex + $batch.Count - 1)",
    "Initial wait per route: $WaitMs ms",
    "Retry wait after a nonzero route exit: $(if ($RetryWaitMs -gt $WaitMs) { "$RetryWaitMs ms" } else { 'disabled' })",
    '',
    '## Results',
    '',
    "| Launch pass | $passed |",
    "| Launch pass after retry | $passedAfterRetry |",
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
