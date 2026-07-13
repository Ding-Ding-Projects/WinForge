[CmdletBinding()]
param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [string]$ManifestPath,
    [string]$OutputPath,
    [switch]$InitializeLedger,
    [string]$LedgerPath,
    [switch]$ForceLedger
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repo = (Resolve-Path -LiteralPath $RepoRoot).Path
if (-not $OutputPath) {
    $OutputPath = Join-Path $repo 'src\WinForge.App\Resources\modules.json'
}
if (-not $LedgerPath) {
    $LedgerPath = Join-Path $repo 'docs\cpp-port-parity.json'
}

if (-not $ManifestPath) {
    $inventoryDirectory = Join-Path $repo 'artifacts\native-cpp-inventory'
    $extractor = Join-Path $repo '.agents\skills\winforge-exhaustive-smoke\scripts\New-WinForgeSmokeInventory.ps1'
    if (-not (Test-Path -LiteralPath $extractor)) {
        throw "Smoke inventory extractor is missing: $extractor"
    }

    & powershell -ExecutionPolicy Bypass -File $extractor -RepoRoot $repo -OutputDirectory $inventoryDirectory -Force
    if ($LASTEXITCODE -ne 0) {
        throw "Smoke inventory extraction failed with exit code $LASTEXITCODE."
    }
    $ManifestPath = Join-Path $inventoryDirectory 'manifest.json'
}

$manifestFile = (Resolve-Path -LiteralPath $ManifestPath).Path
$manifest = Get-Content -Raw -LiteralPath $manifestFile | ConvertFrom-Json
$registryPath = Join-Path $repo 'Services\ModuleRegistry.cs'
$categoriesPath = Join-Path $repo 'Catalog\Categories.cs'
$registryPattern = '^\s*new\(\)\s*\{\s*Tag\s*=\s*"(?<tag>(?:\\.|[^"])*)",\s*En\s*=\s*"(?<en>(?:\\.|[^"])*)",\s*Zh\s*=\s*"(?<zh>(?:\\.|[^"])*)",\s*Glyph\s*=\s*(?<glyph>.*?),\s*Keywords\s*=\s*"(?<keywords>(?:\\.|[^"])*)"\s*\},?\s*$'

function ConvertFrom-CSharpLiteral {
    param([Parameter(Mandatory)][AllowEmptyString()][string]$Value)
    return [System.Text.RegularExpressions.Regex]::Unescape($Value)
}

function Resolve-Glyph {
    param([Parameter(Mandatory)][string]$Expression)

    $literal = [regex]::Match($Expression, '^"(?<value>(?:\\.|[^"])*)"$')
    if ($literal.Success) {
        return ConvertFrom-CSharpLiteral $literal.Groups['value'].Value
    }

    $codePoint = [regex]::Match($Expression, '0x(?<hex>[0-9A-Fa-f]+)')
    if ($codePoint.Success) {
        return [char][Convert]::ToInt32($codePoint.Groups['hex'].Value, 16)
    }

    throw "Unsupported glyph expression: $Expression"
}

function ConvertTo-StringArray {
    param($Value)

    if ($null -eq $Value) {
        return @()
    }
    if ($Value -is [string]) {
        return @($Value)
    }
    return @($Value | ForEach-Object { [string]$_ })
}

function Set-Utf8NoBomContent {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][AllowEmptyString()][string]$Value
    )

    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Value, $encoding)
}

$registry = @{}
foreach ($line in Get-Content -LiteralPath $registryPath -Encoding utf8) {
    $match = [regex]::Match($line, $registryPattern)
    if (-not $match.Success) {
        continue
    }

    $tag = ConvertFrom-CSharpLiteral $match.Groups['tag'].Value
    $routeId = $tag
    if ($registry.ContainsKey($routeId)) {
        throw "Duplicate ModuleRegistry route id: $routeId"
    }

    $registry[$routeId] = [ordered]@{
        tag = $tag
        en = ConvertFrom-CSharpLiteral $match.Groups['en'].Value
        zh = ConvertFrom-CSharpLiteral $match.Groups['zh'].Value
        glyph = Resolve-Glyph $match.Groups['glyph'].Value
        keywords = ConvertFrom-CSharpLiteral $match.Groups['keywords'].Value
    }
}

if ($registry.Count -ne [int]$manifest.counts.registryEntries) {
    throw "Parsed $($registry.Count) registry entries, but the smoke manifest reports $($manifest.counts.registryEntries)."
}

$categories = @{}
$categoriesText = Get-Content -Raw -LiteralPath $categoriesPath -Encoding utf8
$categoryPattern = 'public\s+static\s+readonly\s+AppCategory\s+[A-Za-z0-9_]+\s*=\s*new\(\)\s*\{(?<body>.*?)^\s*\};'
$categoryOptions = [System.Text.RegularExpressions.RegexOptions]::Singleline -bor [System.Text.RegularExpressions.RegexOptions]::Multiline
foreach ($match in [regex]::Matches($categoriesText, $categoryPattern, $categoryOptions)) {
    $body = $match.Groups['body'].Value
    $idMatch = [regex]::Match($body, 'Id\s*=\s*"(?<id>[^"]+)"')
    $nameMatch = [regex]::Match($body, 'Name\s*=\s*new\(\s*"(?<en>(?:\\.|[^"])*)"\s*,\s*"(?<zh>(?:\\.|[^"])*)"\s*\)')
    $glyphMatch = [regex]::Match($body, 'Glyph\s*=\s*"(?<glyph>(?:\\.|[^"])*)"')
    if (-not $idMatch.Success -or -not $nameMatch.Success) {
        continue
    }

    $id = $idMatch.Groups['id'].Value
    $categories[$id] = [ordered]@{
        tag = $id
        en = ConvertFrom-CSharpLiteral $nameMatch.Groups['en'].Value
        zh = ConvertFrom-CSharpLiteral $nameMatch.Groups['zh'].Value
        glyph = if ($glyphMatch.Success) { ConvertFrom-CSharpLiteral $glyphMatch.Groups['glyph'].Value } else { '' }
        keywords = "category $($nameMatch.Groups['en'].Value) $($nameMatch.Groups['zh'].Value)"
    }
}

if ($categories.Count -ne [int]$manifest.counts.categoryRoutes) {
    throw "Parsed $($categories.Count) category entries, but the smoke manifest reports $($manifest.counts.categoryRoutes)."
}

$shellNames = @{
    about = @('About', (ConvertFrom-CSharpLiteral '\u95dc\u65bc'))
    licenses = @('Open-source licences', (ConvertFrom-CSharpLiteral '\u958b\u6e90\u6388\u6b0a'))
    manual = @('Manual', (ConvertFrom-CSharpLiteral '\u4f7f\u7528\u624b\u518a'))
    settings = @('Settings', (ConvertFrom-CSharpLiteral '\u8a2d\u5b9a'))
    'shell.allapps' = @('All Apps', ((ConvertFrom-CSharpLiteral '\u6240\u6709') + ' app'))
}

$routes = [System.Collections.Generic.List[object]]::new()
foreach ($route in $manifest.routes) {
    $routeId = [string]$route.id
    $entry = $registry[$routeId]
    if ($null -eq $entry) {
        if ($categories.ContainsKey($routeId)) {
            $entry = $categories[$routeId]
        }
        elseif (-not $shellNames.ContainsKey($routeId)) {
            throw "Route '$routeId' has no ModuleRegistry entry and no native shell definition."
        }
        else {
            $names = $shellNames[$routeId]
            $entry = [ordered]@{
                tag = $routeId
                en = $names[0]
                zh = $names[1]
                glyph = ''
                keywords = "$($names[0]) $($names[1])"
            }
        }
    }

    $aliases = [System.Collections.Generic.List[string]]::new()
    $aliasCandidates = @()
    $aliasCandidates += ConvertTo-StringArray -Value $route.aliases
    $aliasCandidates += @($routeId, [string]$entry.tag)
    foreach ($candidate in $aliasCandidates) {
        if ([string]::IsNullOrWhiteSpace($candidate)) {
            continue
        }
        $normalized = $candidate.Trim().ToLowerInvariant()
        if (-not $aliases.Contains($normalized)) {
            $aliases.Add($normalized)
        }
    }

    $routes.Add([ordered]@{
        id = $routeId
        tag = [string]$entry.tag
        kind = if ($entry.tag.StartsWith('module.', [StringComparison]::Ordinal)) { 'module' } elseif ($categories.ContainsKey($routeId)) { 'category' } else { 'shell' }
        en = [string]$entry.en
        zh = [string]$entry.zh
        glyph = [string]$entry.glyph
        keywords = [string]$entry.keywords
        aliases = @($aliases)
    })
}

$catalog = [ordered]@{
    schemaVersion = 1
    generatedAtUtc = [DateTime]::UtcNow.ToString('o')
    sourceCommit = (& git -C $repo rev-parse HEAD).Trim()
    counts = [ordered]@{
        routes = $routes.Count
        registryEntries = $registry.Count
        categories = $categories.Count
        dynamicRouteFamilies = @($manifest.dynamicRoutes).Count
        aliases = [int]$manifest.counts.aliases
    }
    routes = @($routes)
    dynamicRoutes = @($manifest.dynamicRoutes)
}

$outputDirectory = Split-Path -Parent $OutputPath
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
Set-Utf8NoBomContent -Path $OutputPath -Value ($catalog | ConvertTo-Json -Depth 10)

if ($InitializeLedger) {
    if ((Test-Path -LiteralPath $LedgerPath) -and -not $ForceLedger) {
        throw "Parity ledger already exists. Refusing to overwrite evidence: $LedgerPath"
    }

    $ledgerRoutes = foreach ($route in $routes) {
        [ordered]@{
            id = [string]$route.id
            kind = 'route'
            parentId = $null
            source = @('src/WinForge.App/Resources/modules.json')
            risk = if ($route.kind -eq 'shell') { 'safe' } else { 'stateful' }
            # Keep the script itself Windows PowerShell 5.1-safe without a BOM.
            # A literal middle dot would otherwise be decoded through the active ANSI
            # code page before this UTF-8 ledger is emitted.
            expected = "$($route.en) $(ConvertFrom-CSharpLiteral '\u00b7') $($route.zh) is implemented natively in C++, reachable through every registered alias, and behaviorally equivalent to the current WinForge feature."
            status = 'not-started'
            evidence = [ordered]@{
                static = 'not-started'
                build = 'not-started'
                test = 'not-started'
                launch = 'not-started'
                visual = 'not-started'
                behavior = 'not-started'
                sideEffect = 'not-started'
                docs = 'not-started'
            }
            attempts = @()
            notes = 'C++ parity evidence has not yet been recorded.'
        }
    }

    $ledgerDynamicRoutes = foreach ($route in $manifest.dynamicRoutes) {
        [ordered]@{
            id = [string]$route.id
            kind = 'dynamic-route'
            parentId = $null
            source = @($route.source)
            risk = 'safe'
            expected = [string]$route.expectedSurface
            status = 'not-started'
            evidence = [ordered]@{
                static = 'not-started'
                build = 'not-started'
                test = 'not-started'
                launch = 'not-started'
                visual = 'not-started'
                behavior = 'not-started'
                sideEffect = 'not-started'
                docs = 'not-started'
            }
            attempts = @()
            notes = 'C++ parity evidence has not yet been recorded.'
        }
    }

    $ledger = [ordered]@{
        schemaVersion = 1
        objective = 'Full native C++ rewrite of WinForge with every feature ported, smoke-tested, documented, and visually evidenced.'
        generatedAtUtc = [DateTime]::UtcNow.ToString('o')
        baselineCommit = $catalog.sourceCommit
        baselineCounts = $catalog.counts
        routes = @($ledgerRoutes)
        dynamicRoutes = @($ledgerDynamicRoutes)
    }

    $ledgerDirectory = Split-Path -Parent $LedgerPath
    New-Item -ItemType Directory -Force -Path $ledgerDirectory | Out-Null
    Set-Utf8NoBomContent -Path $LedgerPath -Value ($ledger | ConvertTo-Json -Depth 12)
}

Write-Host "Native catalog written: $OutputPath"
Write-Host "Routes: $($routes.Count); registry entries: $($registry.Count)"
if ($InitializeLedger) {
    Write-Host "Parity ledger initialized: $LedgerPath"
}
