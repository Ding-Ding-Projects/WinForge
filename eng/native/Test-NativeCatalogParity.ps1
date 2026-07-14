[CmdletBinding()]
param(
    [string]$RepoRoot,
    [string]$ManifestPath,
    [string]$CatalogPath,
    [string]$LedgerPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repo = if ($RepoRoot) {
    (Resolve-Path -LiteralPath $RepoRoot).Path
}
else {
    (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
}
if (-not $CatalogPath) {
    $CatalogPath = Join-Path $repo 'src\WinForge.App\Resources\modules.json'
}
if (-not $LedgerPath) {
    $LedgerPath = Join-Path $repo 'docs\cpp-port-parity.json'
}
if (-not $ManifestPath) {
    $inventoryDirectory = Join-Path $repo 'artifacts\native-cpp-parity-check'
    $extractor = Join-Path $repo '.agents\skills\winforge-exhaustive-smoke\scripts\New-WinForgeSmokeInventory.ps1'
    & powershell -ExecutionPolicy Bypass -File $extractor -RepoRoot $repo -OutputDirectory $inventoryDirectory -Force
    if ($LASTEXITCODE -ne 0) {
        throw "Smoke inventory extraction failed with exit code $LASTEXITCODE."
    }
    $ManifestPath = Join-Path $inventoryDirectory 'manifest.json'
}

$manifest = Get-Content -Raw -LiteralPath $ManifestPath -Encoding utf8 | ConvertFrom-Json
$catalog = Get-Content -Raw -LiteralPath $CatalogPath -Encoding utf8 | ConvertFrom-Json
$ledger = Get-Content -Raw -LiteralPath $LedgerPath -Encoding utf8 | ConvertFrom-Json

function Assert-Equal {
    param($Expected, $Actual, [string]$Message)
    if ($Expected -ne $Actual) {
        throw "$Message Expected '$Expected', got '$Actual'."
    }
}

Assert-Equal ([int]$manifest.counts.routes) @($catalog.routes).Count 'Catalog route count differs from the live smoke inventory.'
Assert-Equal ([int]$manifest.counts.routes) @($ledger.routes).Count 'Parity-ledger route count differs from the live smoke inventory.'
Assert-Equal ([int]$manifest.counts.registryEntries) ([int]$catalog.counts.registryEntries) 'Catalog registry count differs from the live smoke inventory.'
Assert-Equal ([int]$manifest.counts.categoryRoutes) ([int]$catalog.counts.categories) 'Catalog category count differs from the live smoke inventory.'
Assert-Equal ([int]$manifest.counts.dynamicRouteFamilies) @($catalog.dynamicRoutes).Count 'Catalog dynamic-route count differs from the live smoke inventory.'
Assert-Equal ([int]$manifest.counts.dynamicRouteFamilies) @($ledger.dynamicRoutes).Count 'Parity-ledger dynamic-route count differs from the live smoke inventory.'

$manifestIds = @($manifest.routes | ForEach-Object { [string]$_.id } | Sort-Object -Unique)
$catalogIds = @($catalog.routes | ForEach-Object { [string]$_.id } | Sort-Object -Unique)
$ledgerIds = @($ledger.routes | ForEach-Object { [string]$_.id } | Sort-Object -Unique)
$manifestDynamicIds = @($manifest.dynamicRoutes | ForEach-Object { [string]$_.id } | Sort-Object -Unique)
$catalogDynamicIds = @($catalog.dynamicRoutes | ForEach-Object { [string]$_.id } | Sort-Object -Unique)
$ledgerDynamicIds = @($ledger.dynamicRoutes | ForEach-Object { [string]$_.id } | Sort-Object -Unique)
Assert-Equal @($manifest.routes).Count $manifestIds.Count 'Live smoke inventory contains duplicate route ids.'
Assert-Equal @($catalog.routes).Count $catalogIds.Count 'Native catalog contains duplicate route ids.'
Assert-Equal @($ledger.routes).Count $ledgerIds.Count 'Parity ledger contains duplicate route ids.'
Assert-Equal @($manifest.dynamicRoutes).Count $manifestDynamicIds.Count 'Live smoke inventory contains duplicate dynamic-route ids.'
Assert-Equal @($catalog.dynamicRoutes).Count $catalogDynamicIds.Count 'Native catalog contains duplicate dynamic-route ids.'
Assert-Equal @($ledger.dynamicRoutes).Count $ledgerDynamicIds.Count 'Parity ledger contains duplicate dynamic-route ids.'

$catalogDiff = Compare-Object $manifestIds $catalogIds
if ($catalogDiff) {
    throw "Native catalog route ids do not match the live inventory: $($catalogDiff | Out-String)"
}
$ledgerDiff = Compare-Object $manifestIds $ledgerIds
if ($ledgerDiff) {
    throw "Parity-ledger route ids do not match the live inventory: $($ledgerDiff | Out-String)"
}
$catalogDynamicDiff = Compare-Object $manifestDynamicIds $catalogDynamicIds
if ($catalogDynamicDiff) {
    throw "Native catalog dynamic-route ids do not match the live inventory: $($catalogDynamicDiff | Out-String)"
}
$ledgerDynamicDiff = Compare-Object $manifestDynamicIds $ledgerDynamicIds
if ($ledgerDynamicDiff) {
    throw "Parity-ledger dynamic-route ids do not match the live inventory: $($ledgerDynamicDiff | Out-String)"
}

$allowedStatuses = @(
    'not-started', 'in-progress', 'static-pass', 'build-pass', 'test-pass',
    'launch-pass', 'visual-pass', 'behavior-pass', 'pass', 'failed', 'blocked',
    'capture-blocked', 'unsafe-without-authorization', 'not-applicable'
)
$requiredDimensions = @('static', 'build', 'test', 'launch', 'visual', 'behavior', 'sideEffect', 'docs')

foreach ($entry in $catalog.routes) {
    if ([string]::IsNullOrWhiteSpace([string]$entry.en) -or [string]::IsNullOrWhiteSpace([string]$entry.zh)) {
        throw "Catalog route '$($entry.id)' is missing bilingual names."
    }
    if (@($entry.aliases).Count -eq 0) {
        throw "Catalog route '$($entry.id)' has no aliases."
    }
    $manifestRoute = @($manifest.routes | Where-Object { [string]$_.id -eq [string]$entry.id })[0]
    $expectedAliases = @(
        @($manifestRoute.aliases) + @([string]$entry.id, [string]$entry.tag) |
            Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) } |
            ForEach-Object { ([string]$_).Trim().ToLowerInvariant() } |
            Sort-Object -Unique
    )
    $actualAliases = @(
        @($entry.aliases) |
            ForEach-Object { ([string]$_).Trim().ToLowerInvariant() } |
            Sort-Object -Unique
    )
    $aliasDiff = Compare-Object $expectedAliases $actualAliases
    if ($aliasDiff) {
        throw "Catalog route '$($entry.id)' aliases differ from the live inventory: $($aliasDiff | Out-String)"
    }
}

$hanLabelCount = @($catalog.routes | Where-Object { [string]$_.zh -match '[\u3400-\u9fff]' }).Count
if ($hanLabelCount -lt 340) {
    throw "Only $hanLabelCount of $(@($catalog.routes).Count) Cantonese labels contain Han characters; check UTF-8 decoding."
}
$mojibakeLabels = @($catalog.routes | Where-Object { [string]$_.zh -cmatch '[\u00c0-\u024f]|\ufffd' })
if ($mojibakeLabels.Count -gt 0) {
    throw "Catalog Cantonese labels contain likely mojibake: $(@($mojibakeLabels.id) -join ', ')"
}

$aliasOwners = @{}
$collisions = @{}
$deepLinkPrecedence = @($catalog.routes | Where-Object { [string]$_.kind -eq 'category' }) +
    @($catalog.routes | Where-Object { [string]$_.kind -ne 'category' })
foreach ($route in $deepLinkPrecedence) {
    foreach ($alias in @($route.aliases)) {
        $key = ([string]$alias).Trim().ToLowerInvariant()
        if ($aliasOwners.ContainsKey($key) -and $aliasOwners[$key] -ne [string]$route.id) {
            $collisions[$key] = [string]$route.id
        }
        $aliasOwners[$key] = [string]$route.id
    }
}
$expectedLegacyCollisions = @{
    apps = 'module.uninstall'
    launcher = 'module.cmdpalette'
    taskbar = 'module.taskbar-tweaker'
    vault = 'module.vault-volumes'
}
Assert-Equal $expectedLegacyCollisions.Count $collisions.Count 'Unexpected number of canonical-category/deep-link alias collisions.'
foreach ($key in $expectedLegacyCollisions.Keys) {
    if (-not $collisions.ContainsKey($key)) {
        throw "Expected legacy deep-link collision '$key' is missing from the native catalog."
    }
    Assert-Equal $expectedLegacyCollisions[$key] $collisions[$key] "Legacy deep-link collision '$key' resolves to the wrong route."
}

foreach ($entry in $ledger.routes) {
    if ($allowedStatuses -notcontains [string]$entry.status) {
        throw "Ledger route '$($entry.id)' has invalid status '$($entry.status)'."
    }
    foreach ($dimension in $requiredDimensions) {
        if ($null -eq $entry.evidence.$dimension) {
            throw "Ledger route '$($entry.id)' is missing evidence dimension '$dimension'."
        }
    }
}

foreach ($entry in $ledger.dynamicRoutes) {
    if ($allowedStatuses -notcontains [string]$entry.status) {
        throw "Dynamic ledger route '$($entry.id)' has invalid status '$($entry.status)'."
    }
    foreach ($dimension in $requiredDimensions) {
        if ($null -eq $entry.evidence.$dimension) {
            throw "Dynamic ledger route '$($entry.id)' is missing evidence dimension '$dimension'."
        }
    }
}

Write-Host 'PASS native catalog / C++ parity ledger'
Write-Host "  Routes: $($catalogIds.Count)"
Write-Host "  Dynamic route families: $($catalogDynamicIds.Count)"
Write-Host "  Registry entries: $($catalog.counts.registryEntries)"
Write-Host "  Categories: $($catalog.counts.categories)"
Write-Host "  Ledger rows: $($ledgerIds.Count)"
