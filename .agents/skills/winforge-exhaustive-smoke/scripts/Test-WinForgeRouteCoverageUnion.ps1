[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ManifestPath,

    [Parameter(Mandatory = $true)]
    [string[]]$NumericRanges,

    [Parameter()]
    [int]$SpecialRouteIndex = -1,

    [Parameter()]
    [string]$SpecialRouteId
)

$ErrorActionPreference = 'Stop'

$manifestFile = (Resolve-Path -LiteralPath $ManifestPath).Path
$manifest = Get-Content -LiteralPath $manifestFile -Raw | ConvertFrom-Json
$routes = @($manifest.routes)
if ($routes.Count -eq 0) { throw "Manifest has no routes: $manifestFile" }

if ($SpecialRouteIndex -ge $routes.Count) {
    throw "SpecialRouteIndex $SpecialRouteIndex is outside the $($routes.Count)-route manifest."
}
if ($SpecialRouteIndex -lt -1) {
    throw 'SpecialRouteIndex must be -1 or a valid route-array index.'
}
if ($SpecialRouteIndex -ge 0 -and -not [string]::IsNullOrWhiteSpace($SpecialRouteId)) {
    if ($routes[$SpecialRouteIndex].id -ne $SpecialRouteId) {
        throw "Expected special route '$SpecialRouteId' at index $SpecialRouteIndex; found '$($routes[$SpecialRouteIndex].id)'."
    }
}

$coverage = @{}
$parsedRanges = [System.Collections.Generic.List[object]]::new()
foreach ($rangeText in (($NumericRanges -join ',').Split(',', [System.StringSplitOptions]::RemoveEmptyEntries) | ForEach-Object { $_.Trim() })) {
    if ($rangeText -notmatch '^(?<start>\d+)\s*-\s*(?<end>\d+)$') {
        throw "Invalid numeric range '$rangeText'. Use inclusive start-end form such as 0-24."
    }
    $start = [int]$Matches.start
    $end = [int]$Matches.end
    if ($start -gt $end) { throw "Range '$rangeText' starts after it ends." }
    if ($start -lt 0 -or $end -ge $routes.Count) {
        throw "Range '$rangeText' is outside manifest indices 0-$($routes.Count - 1)."
    }
    for ($index = $start; $index -le $end; $index++) {
        if ($coverage.ContainsKey($index)) {
            throw "Route index $index is covered by both '$($coverage[$index])' and '$rangeText'."
        }
        $coverage[$index] = $rangeText
    }
    $parsedRanges.Add([pscustomobject]@{ Start = $start; End = $end; Range = "$start-$end" })
}

$expected = 0..($routes.Count - 1) | Where-Object { $_ -ne $SpecialRouteIndex }
$missing = @($expected | Where-Object { -not $coverage.ContainsKey($_) })
$unexpected = @($coverage.Keys | Where-Object { $_ -eq $SpecialRouteIndex })
if ($missing.Count -gt 0 -or $unexpected.Count -gt 0) {
    throw "Route coverage union is incomplete. Missing: $($missing -join ', '); special/invalid coverage: $($unexpected -join ', ')."
}

[pscustomobject]@{
    Manifest = $manifestFile
    RouteCount = $routes.Count
    NumericRouteCount = $expected.Count
    CoveredNumericRouteCount = $coverage.Count
    RangeCount = $parsedRanges.Count
    SpecialRouteIndex = $SpecialRouteIndex
    SpecialRouteId = if ($SpecialRouteIndex -ge 0) { $routes[$SpecialRouteIndex].id } else { $null }
    SpecialRouteKind = if ($SpecialRouteIndex -ge 0) { $routes[$SpecialRouteIndex].kind } else { $null }
}

Write-Output 'Inclusive numeric ranges:'
$parsedRanges | Sort-Object Start | Format-Table -AutoSize
