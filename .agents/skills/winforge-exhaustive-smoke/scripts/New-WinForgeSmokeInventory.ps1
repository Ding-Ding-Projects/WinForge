[CmdletBinding()]
param(
    [Parameter()]
    [string]$RepoRoot = (Get-Location).Path,

    [Parameter()]
    [string]$OutputDirectory,

    [switch]$Force
)

$ErrorActionPreference = 'Stop'

function Get-RepoRelativePath {
    param(
        [Parameter(Mandatory = $true)][string]$FullPath,
        [Parameter(Mandatory = $true)][string]$Root
    )

    $normalizedRoot = $Root.TrimEnd('\', '/')
    if ($FullPath.StartsWith($normalizedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $FullPath.Substring($normalizedRoot.Length).TrimStart('\', '/').Replace('\', '/')
    }

    return $FullPath.Replace('\', '/')
}

function Get-TextFile {
    param([Parameter(Mandatory = $true)][string]$Path)
    return [System.IO.File]::ReadAllText($Path)
}

function Get-Matches {
    param(
        [Parameter(Mandatory = $true)][string]$Text,
        [Parameter(Mandatory = $true)][string]$Pattern,
        [System.Text.RegularExpressions.RegexOptions]$Options = [System.Text.RegularExpressions.RegexOptions]::None
    )
    return [System.Text.RegularExpressions.Regex]::Matches($Text, $Pattern, $Options)
}

function Get-FirstGroup {
    param(
        [Parameter(Mandatory = $true)]$Match,
        [Parameter(Mandatory = $true)][string]$Name
    )
    return $Match.Groups[$Name].Value
}

function Get-CSharpMethodBlock {
    param(
        [Parameter(Mandatory = $true)][string]$Text,
        [Parameter(Mandatory = $true)][string]$Signature
    )

    $signatureIndex = $Text.IndexOf($Signature, [System.StringComparison]::Ordinal)
    if ($signatureIndex -lt 0) {
        throw "Could not find method signature '$Signature'."
    }

    $openBrace = $Text.IndexOf('{', $signatureIndex)
    if ($openBrace -lt 0) {
        throw "Could not find opening brace for '$Signature'."
    }

    $depth = 0
    for ($index = $openBrace; $index -lt $Text.Length; $index++) {
        $character = $Text[$index]
        if ($character -eq '{') {
            $depth++
        }
        elseif ($character -eq '}') {
            $depth--
            if ($depth -eq 0) {
                return $Text.Substring($signatureIndex, $index - $signatureIndex + 1)
            }
        }
    }

    throw "Could not find closing brace for '$Signature'."
}

$repo = (Resolve-Path -LiteralPath $RepoRoot).Path
$required = @(
    (Join-Path $repo 'Services\ModuleRegistry.cs'),
    (Join-Path $repo 'Catalog\Categories.cs'),
    (Join-Path $repo 'MainWindow.xaml.cs'),
    (Join-Path $repo 'MainWindow.xaml')
)

foreach ($path in $required) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "WinForge inventory requires '$path'."
    }
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $OutputDirectory = Join-Path $repo "artifacts\smoke\$stamp"
}
elseif (-not [System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory = Join-Path $repo $OutputDirectory
}

if (Test-Path -LiteralPath $OutputDirectory) {
    $existing = Get-ChildItem -LiteralPath $OutputDirectory -Force -ErrorAction SilentlyContinue
    if ($existing -and -not $Force) {
        throw "Output directory '$OutputDirectory' is not empty. Use -Force to reuse it."
    }
}
else {
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
}

$registryPath = Join-Path $repo 'Services\ModuleRegistry.cs'
$categoriesPath = Join-Path $repo 'Catalog\Categories.cs'
$mainCodePath = Join-Path $repo 'MainWindow.xaml.cs'
$mainXamlPath = Join-Path $repo 'MainWindow.xaml'
$registryText = Get-TextFile $registryPath
$categoriesText = Get-TextFile $categoriesPath
$mainCodeText = Get-TextFile $mainCodePath
$mainXamlText = Get-TextFile $mainXamlPath

$registryByTag = @{}
$registryEntries = @()
$registryPattern = 'new\(\)\s*\{\s*Tag\s*=\s*"(?<tag>[^"]+)"\s*,\s*En\s*=\s*"(?<en>[^"]+)"'
foreach ($match in Get-Matches -Text $registryText -Pattern $registryPattern -Options ([System.Text.RegularExpressions.RegexOptions]::Singleline)) {
    $tag = Get-FirstGroup $match 'tag'
    $entry = [pscustomobject]@{
        tag = $tag
        en = Get-FirstGroup $match 'en'
        source = 'Services/ModuleRegistry.cs'
    }
    $registryByTag[$tag] = $entry
    $registryEntries += $entry
}

$categoryById = @{}
$categoryPattern = 'public\s+static\s+readonly\s+AppCategory\s+[A-Za-z0-9_]+\s*=\s*new\(\)\s*\{(?<body>.*?)^\s*\};'
$categoryOptions = [System.Text.RegularExpressions.RegexOptions]::Singleline -bor [System.Text.RegularExpressions.RegexOptions]::Multiline
foreach ($match in Get-Matches -Text $categoriesText -Pattern $categoryPattern -Options $categoryOptions) {
    $body = Get-FirstGroup $match 'body'
    $idMatch = [regex]::Match($body, 'Id\s*=\s*"(?<id>[^"]+)"')
    $nameMatch = [regex]::Match($body, 'Name\s*=\s*new\(\s*"(?<en>(?:\\.|[^"])*)"\s*,\s*"(?<zh>(?:\\.|[^"])*)"\s*\)')
    if (-not $idMatch.Success -or -not $nameMatch.Success) {
        continue
    }

    $id = $idMatch.Groups['id'].Value
    $categoryById[$id] = [pscustomobject]@{
        id = $id
        en = [regex]::Unescape($nameMatch.Groups['en'].Value)
        zh = [regex]::Unescape($nameMatch.Groups['zh'].Value)
        source = 'Catalog/Categories.cs'
    }
}

$typeByTag = @{}
$mapPattern = '^\s*"(?<tag>[^"]+)"\s*=>\s*typeof\((?<type>[A-Za-z0-9_]+)\)'
foreach ($match in Get-Matches -Text $mainCodeText -Pattern $mapPattern -Options ([System.Text.RegularExpressions.RegexOptions]::Multiline)) {
    $typeByTag[(Get-FirstGroup $match 'tag')] = Get-FirstGroup $match 'type'
}

$navTags = @(
    (Get-Matches -Text $mainXamlText -Pattern 'Tag\s*=\s*"(?<tag>[^"]+)"' |
        ForEach-Object { Get-FirstGroup $_ 'tag' } |
        Sort-Object -Unique)
)

$aliasesByTarget = @{}
$unmappedAliases = @()
$pendingAliases = @()
$startPageText = Get-CSharpMethodBlock -Text $mainCodeText -Signature 'private void ApplyStartPage()'
foreach ($line in ($startPageText -split "\r?\n")) {
    $caseMatch = [System.Text.RegularExpressions.Regex]::Match($line, '^\s*case\s+"(?<alias>[^"]+)":')
    if ($caseMatch.Success) {
        $pendingAliases += $caseMatch.Groups['alias'].Value
        continue
    }

    $targetMatch = [System.Text.RegularExpressions.Regex]::Match($line, 'Navigator\.GoToModule\?\.Invoke\("(?<tag>[^"]+)"\)')
    if ($targetMatch.Success) {
        $target = $targetMatch.Groups['tag'].Value
        if (-not $aliasesByTarget.ContainsKey($target)) {
            $aliasesByTarget[$target] = @()
        }
        $aliasesByTarget[$target] += $pendingAliases
        $pendingAliases = @()
        continue
    }

    # Package Manager view aliases resolve through the shared managed routing helper
    # rather than a string literal. They are still aliases of the module.packages
    # route, so preserve them in its inventory entry instead of flagging them unmapped.
    $packageViewTargetMatch = [System.Text.RegularExpressions.Regex]::Match($line, 'Navigator\.GoToModule\?\.Invoke\(PackageManagerViewRouting\.NavigationKey\(PackageManagerViewTarget\.(?<view>Discover|Updates|Installed)\)\)')
    if ($packageViewTargetMatch.Success) {
        $target = 'module.packages'
        if (-not $aliasesByTarget.ContainsKey($target)) {
            $aliasesByTarget[$target] = @()
        }
        $aliasesByTarget[$target] += $pendingAliases
        $pendingAliases = @()
        continue
    }

    $navigateMatch = [System.Text.RegularExpressions.Regex]::Match($line, 'NavigateActive\("(?<tag>[^"]+)"\)')
    if ($navigateMatch.Success -and $pendingAliases.Count -gt 0) {
        $target = $navigateMatch.Groups['tag'].Value
        if (-not $aliasesByTarget.ContainsKey($target)) {
            $aliasesByTarget[$target] = @()
        }
        $aliasesByTarget[$target] += $pendingAliases
        $pendingAliases = @()
        continue
    }

    if ($line -match 'QueueAllAppsPickerFromStartPage\(\)' -and $pendingAliases.Count -gt 0) {
        $target = 'shell.allapps'
        if (-not $aliasesByTarget.ContainsKey($target)) {
            $aliasesByTarget[$target] = @()
        }
        $aliasesByTarget[$target] += $pendingAliases
        $pendingAliases = @()
        continue
    }

    if ($line -match '^\s*break;' -and $pendingAliases.Count -gt 0) {
        $unmappedAliases += $pendingAliases
        $pendingAliases = @()
    }
}

if ($pendingAliases.Count -gt 0) {
    $unmappedAliases += $pendingAliases
}

$xamlByClass = @{}
$pageXamlFiles = @(
    Get-ChildItem -LiteralPath (Join-Path $repo 'Pages') -Filter '*.xaml' -File -ErrorAction SilentlyContinue |
        Sort-Object FullName
)
foreach ($file in $pageXamlFiles) {
    $text = Get-TextFile $file.FullName
    $classMatch = [System.Text.RegularExpressions.Regex]::Match($text, 'x:Class\s*=\s*"(?<class>[^"]+)"')
    if ($classMatch.Success) {
        $xamlByClass[$classMatch.Groups['class'].Value.Split('.')[-1]] = [pscustomobject]@{
            xaml = Get-RepoRelativePath -FullPath $file.FullName -Root $repo
            codeBehind = if (Test-Path -LiteralPath ($file.FullName + '.cs')) { Get-RepoRelativePath -FullPath ($file.FullName + '.cs') -Root $repo } else { $null }
            text = $text
        }
    }
}

$controlNames = @(
    'Button', 'ToggleSwitch', 'Slider', 'ComboBox', 'TextBox', 'PasswordBox',
    'NumberBox', 'RadioButton', 'CheckBox', 'ListView', 'GridView', 'TreeView',
    'TabView', 'NavigationView', 'HyperlinkButton', 'AppBarButton',
    'MenuFlyoutItem', 'TeachingTip', 'InfoBar', 'WebView2'
)

# Shell routes can render a modal surface instead of navigating a Frame. Keep that
# distinction in the manifest so a launch-only run is never mistaken for page evidence.
$shellDialogRoutes = @{
    'shell.allapps' = [pscustomobject]@{
        expectedSurface = 'NewTabPickerDialog'
        detail = 'Modal All Apps picker; verify the dialog rather than a tab page.'
    }
}

$shellPageTypes = @{
    'dashboard' = 'DashboardPage'
    'about' = 'AboutPage'
    'settings' = 'SettingsPage'
    'manual' = 'ManualPage'
    'licenses' = 'LicensesPage'
}
$shellNames = @{
    'dashboard' = 'Dashboard'
    'about' = 'About'
    'settings' = 'Settings'
    'manual' = 'Manual'
    'licenses' = 'Licenses'
    'shell.allapps' = 'All Apps'
}
$knownShellRoutes = @($shellNames.Keys)

$dynamicRouteFamilies = @(
    [pscustomobject]@{
        id = 'search:<query>'
        kind = 'dynamic-route-family'
        expectedSurface = 'SearchResultsPage'
        source = @('MainWindow.xaml.cs', 'Pages/SearchResultsPage.xaml', 'Pages/SearchResultsPage.xaml.cs')
        initialStatus = 'not-started'
    },
    [pscustomobject]@{
        id = 'manual:<fragment>'
        kind = 'dynamic-route-family'
        expectedSurface = 'ManualPage section'
        source = @('MainWindow.xaml.cs', 'Pages/ManualPage.xaml', 'Pages/ManualPage.xaml.cs')
        initialStatus = 'not-started'
    },
    [pscustomobject]@{
        id = 'module.<id>#<fragment>'
        kind = 'dynamic-route-family'
        expectedSurface = 'Mapped module page with a navigation fragment'
        source = @('MainWindow.xaml.cs')
        initialStatus = 'not-started'
    },
    [pscustomobject]@{
        id = 'module.packages#(discover|updates|installed)'
        kind = 'dynamic-route-family'
        expectedSurface = 'PackageManagerModule selected view'
        source = @('MainWindow.xaml.cs', 'Services/PackageManagerViewRouting.cs', 'Pages/PackageManagerModule.xaml.cs')
        initialStatus = 'not-started'
    },
    [pscustomobject]@{
        id = 'weblogin?url=<uri>'
        kind = 'dynamic-route-family'
        expectedSurface = 'WebLoginModule opened at the requested URI'
        source = @('MainWindow.xaml.cs', 'Pages/WebLoginModule.xaml', 'Pages/WebLoginModule.xaml.cs')
        initialStatus = 'not-started'
    }
)

$routeRecords = @()
$allRouteTags = @($registryByTag.Keys + $typeByTag.Keys + $navTags + $categoryById.Keys + $knownShellRoutes | Sort-Object -Unique)
foreach ($tag in $allRouteTags) {
    $type = if ($typeByTag.ContainsKey($tag)) {
        $typeByTag[$tag]
    }
    elseif ($categoryById.ContainsKey($tag)) {
        'CategoryPage'
    }
    elseif ($shellPageTypes.ContainsKey($tag)) {
        $shellPageTypes[$tag]
    }
    else {
        $null
    }
    $xamlInfo = if ($type -and $xamlByClass.ContainsKey($type)) { $xamlByClass[$type] } else { $null }
    $controls = @{}
    $handlers = @()
    if ($xamlInfo) {
        foreach ($control in $controlNames) {
            $pattern = '<(?:[A-Za-z0-9_]+:)?' + [System.Text.RegularExpressions.Regex]::Escape($control) + '\b'
            $controls[$control] = (Get-Matches -Text $xamlInfo.text -Pattern $pattern).Count
        }
        $handlers = @(
            Get-Matches -Text $xamlInfo.text -Pattern '(?:Click|Toggled|ValueChanged|SelectionChanged|TextChanged|Loaded|Unloaded)\s*=\s*"(?<handler>[^"]+)"' |
                ForEach-Object { Get-FirstGroup $_ 'handler' } |
                Sort-Object -Unique
        )
    }

    $moduleStem = if ($type) { $type -replace 'Module$', '' } else { '' }
    $guessedServicePath = if ($moduleStem) { Join-Path $repo ('Services\' + $moduleStem + 'Service.cs') } else { '' }
    $service = if ($guessedServicePath -and (Test-Path -LiteralPath $guessedServicePath)) { Get-RepoRelativePath -FullPath $guessedServicePath -Root $repo } else { $null }
    $aliases = if ($aliasesByTarget.ContainsKey($tag)) { @($aliasesByTarget[$tag] | Sort-Object -Unique) } else { @() }
    if ($categoryById.ContainsKey($tag) -and $aliases -notcontains $tag) {
        $aliases += $tag
    }

    $routingState = @()
    if ($registryByTag.ContainsKey($tag)) { $routingState += 'registry' }
    if ($typeByTag.ContainsKey($tag)) { $routingState += 'mapType' }
    if ($navTags -contains $tag) { $routingState += 'navigation' }
    if ($categoryById.ContainsKey($tag)) { $routingState += @('categoryCatalog', 'navigation') }
    if ($tag -eq 'settings') { $routingState += @('shell', 'navigation') }
    if ($aliases.Count -gt 0) { $routingState += 'deepLink' }
    $routingState = @($routingState | Sort-Object -Unique)

    $relatedSource = @(
        'Services/ModuleRegistry.cs',
        'MainWindow.xaml.cs'
    )
    if ($xamlInfo) {
        $relatedSource += $xamlInfo.xaml
    }
    if ($xamlInfo -and $xamlInfo.codeBehind) {
        $relatedSource += $xamlInfo.codeBehind
    }
    if ($service) {
        $relatedSource += $service
    }
    if ($categoryById.ContainsKey($tag)) {
        $relatedSource += 'Catalog/Categories.cs'
    }

    $shellDialog = if ($shellDialogRoutes.ContainsKey($tag)) { $shellDialogRoutes[$tag] } else { $null }

    $routeRecords += [pscustomobject]@{
        id = $tag
        kind = if ($shellDialog) { 'shell-dialog' } elseif ($tag -like 'module.*') { 'module' } elseif ($categoryById.ContainsKey($tag)) { 'category' } else { 'route' }
        name = if ($registryByTag.ContainsKey($tag)) { $registryByTag[$tag].en } elseif ($categoryById.ContainsKey($tag)) { $categoryById[$tag].en } elseif ($shellNames.ContainsKey($tag)) { $shellNames[$tag] } else { $null }
        pageType = $type
        expectedSurface = if ($shellDialog) { $shellDialog.expectedSurface } else { $type }
        launchDisposition = if ($shellDialog) { $shellDialog.detail } else { 'Frame/page route or route-level surface.' }
        source = @($relatedSource | Where-Object { $_ } | Select-Object -Unique)
        aliases = $aliases
        routing = $routingState
        xamlControls = [pscustomobject]$controls
        xamlHandlers = $handlers
        initialStatus = 'not-started'
    }
}

$routingIssues = @()
foreach ($entry in $registryEntries) {
    if (-not $typeByTag.ContainsKey($entry.tag) -and $entry.tag -notin $knownShellRoutes) {
        $routingIssues += [pscustomobject]@{ kind = 'registry-without-maptype'; tag = $entry.tag; detail = 'Review whether this registry route intentionally resolves elsewhere.' }
    }
}
foreach ($tag in $typeByTag.Keys) {
    if (-not $registryByTag.ContainsKey($tag)) {
        $routingIssues += [pscustomobject]@{ kind = 'maptype-without-registry'; tag = $tag; detail = 'Review discoverability/search registration.' }
    }
}
foreach ($tag in $navTags) {
    if (-not $registryByTag.ContainsKey($tag) -and -not $typeByTag.ContainsKey($tag) -and -not $categoryById.ContainsKey($tag) -and $tag -notin $knownShellRoutes) {
        $routingIssues += [pscustomobject]@{ kind = 'navigation-without-known-route'; tag = $tag; detail = 'Review navigation tag and resolver.' }
    }
}

$companionPath = Join-Path $repo 'Services\CompanionAppService.cs'
$companions = @()
if (Test-Path -LiteralPath $companionPath) {
    $companionText = Get-TextFile $companionPath
    $companionPattern = 'Id\s*=\s*"(?<id>[^"]+)"\s*,\s*Kind\s*=\s*CompanionKind\.(?<kind>[A-Za-z]+)\s*,\s*(?:TitleEn|NameEn)\s*=\s*"(?<name>[^"]+)"'
    foreach ($match in Get-Matches -Text $companionText -Pattern $companionPattern -Options ([System.Text.RegularExpressions.RegexOptions]::Singleline)) {
        $companions += [pscustomobject]@{
            id = Get-FirstGroup $match 'id'
            kind = Get-FirstGroup $match 'kind'
            name = Get-FirstGroup $match 'name'
            source = 'Services/CompanionAppService.cs'
            initialStatus = 'not-started'
        }
    }
}

$externalAppsPath = Join-Path $repo 'Catalog\ExternalApps.cs'
$externalApps = @()
if (Test-Path -LiteralPath $externalAppsPath) {
    $externalAppsText = Get-TextFile $externalAppsPath
    $externalPattern = 'Id\s*=\s*"(?<id>[^"]+)"\s*,\s*NameEn\s*=\s*"(?<name>[^"]+)"'
    foreach ($match in Get-Matches -Text $externalAppsText -Pattern $externalPattern -Options ([System.Text.RegularExpressions.RegexOptions]::Singleline)) {
        $externalApps += [pscustomobject]@{
            id = Get-FirstGroup $match 'id'
            name = Get-FirstGroup $match 'name'
            source = 'Catalog/ExternalApps.cs'
            initialStatus = 'not-started'
        }
    }
}

$sourceExtensions = @('.cs', '.xaml', '.html', '.js', '.css', '.cpp', '.h')
$excludedDirectoryPattern = '\\(bin|obj|\.git|artifacts|ThirdParty|node_modules|packages|Generated Files)\\'
$sourceAudit = @()
$sourceFiles = @(
    Get-ChildItem -LiteralPath $repo -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object {
            $_.Extension -in $sourceExtensions -and
            $_.FullName -notmatch $excludedDirectoryPattern
        } |
        Sort-Object FullName
)
foreach ($file in $sourceFiles) {
    $content = Get-TextFile $file.FullName
    $sourceAudit += [pscustomobject]@{
        file = Get-RepoRelativePath -FullPath $file.FullName -Root $repo
        extension = $file.Extension
        lines = ($content -split "\r?\n").Count
        todoCount = (Get-Matches -Text $content -Pattern '\bTODO\b' -Options ([System.Text.RegularExpressions.RegexOptions]::IgnoreCase)).Count
        fixmeCount = (Get-Matches -Text $content -Pattern '\bFIXME\b' -Options ([System.Text.RegularExpressions.RegexOptions]::IgnoreCase)).Count
        notImplementedCount = (Get-Matches -Text $content -Pattern 'NotImplementedException').Count
        emptyCatchCount = (Get-Matches -Text $content -Pattern 'catch\s*(?:\([^)]*\))?\s*\{\s*\}').Count
        reviewStatus = 'not-started'
    }
}

$testProjects = @(
    Get-ChildItem -LiteralPath (Join-Path $repo 'tests') -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Extension -in @('.csproj', '.vcxproj') } |
        ForEach-Object { Get-RepoRelativePath -FullPath $_.FullName -Root $repo } |
        Sort-Object
)
$wikiPages = @(
    Get-ChildItem -LiteralPath (Join-Path $repo 'docs\wiki') -Recurse -Filter '*.md' -File -ErrorAction SilentlyContinue |
        ForEach-Object { Get-RepoRelativePath -FullPath $_.FullName -Root $repo } |
        Sort-Object
)

$manifest = [pscustomobject]@{
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    repoRoot = $repo
    generator = 'winforge-exhaustive-smoke/New-WinForgeSmokeInventory.ps1'
    excludedTrees = @('bin', 'obj', '.git', 'artifacts', 'ThirdParty', 'node_modules')
    counts = [pscustomobject]@{
        registryEntries = $registryEntries.Count
        mapTypeEntries = $typeByTag.Count
        navigationTags = $navTags.Count
        categoryRoutes = $categoryById.Count
        routes = $routeRecords.Count
        dynamicRouteFamilies = $dynamicRouteFamilies.Count
        aliases = ($aliasesByTarget.Values | ForEach-Object { $_ } | Sort-Object -Unique).Count
        companions = $companions.Count
        externalApps = $externalApps.Count
        sourceFiles = $sourceAudit.Count
        sourceLines = ($sourceAudit | Measure-Object -Property lines -Sum).Sum
        testProjects = $testProjects.Count
        wikiPages = $wikiPages.Count
    }
    routes = @($routeRecords | Sort-Object id)
    dynamicRoutes = @($dynamicRouteFamilies | Sort-Object id)
    routingIssues = @($routingIssues | Sort-Object kind, tag)
    unmappedAliases = @($unmappedAliases | Sort-Object -Unique)
    companions = @($companions | Sort-Object id)
    externalApps = @($externalApps | Sort-Object id)
    testProjects = $testProjects
    wikiPages = $wikiPages
    sourceAudit = $sourceAudit
}

$jsonPath = Join-Path $OutputDirectory 'manifest.json'
$csvPath = Join-Path $OutputDirectory 'manifest.csv'
$summaryPath = Join-Path $OutputDirectory 'summary.md'

$manifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $jsonPath -Encoding UTF8
$routeRecords |
    Sort-Object id |
    Select-Object id, kind, name, pageType, expectedSurface, launchDisposition,
        @{ Name = 'aliases'; Expression = { $_.aliases -join ';' } },
        @{ Name = 'routing'; Expression = { $_.routing -join ';' } },
        @{ Name = 'source'; Expression = { $_.source -join ';' } },
        initialStatus |
    Export-Csv -LiteralPath $csvPath -NoTypeInformation -Encoding UTF8

$summaryLines = @(
    '# WinForge smoke inventory',
    '',
    "Generated: $($manifest.generatedAtUtc)",
    '',
    '## Counts',
    '',
    "| Registry entries | $($manifest.counts.registryEntries) |",
    "| MapType entries | $($manifest.counts.mapTypeEntries) |",
    "| Static XAML navigation tags | $($manifest.counts.navigationTags) |",
    "| Runtime category routes | $($manifest.counts.categoryRoutes) |",
    "| Fixed base-route records | $($manifest.counts.routes) |",
    "| Dynamic route families | $($manifest.counts.dynamicRouteFamilies) |",
    "| Deep-link aliases | $($manifest.counts.aliases) |",
    "| Companion specs | $($manifest.counts.companions) |",
    "| External app specs | $($manifest.counts.externalApps) |",
    "| Source files | $($manifest.counts.sourceFiles) |",
    "| Source lines | $($manifest.counts.sourceLines) |",
    "| Test projects | $($manifest.counts.testProjects) |",
    "| Wiki pages | $($manifest.counts.wikiPages) |",
    '',
    '## Routing review queue',
    ''
)
if ($routingIssues.Count -eq 0 -and $unmappedAliases.Count -eq 0) {
    $summaryLines += 'No structural routing mismatches were detected by the extractor.'
}
else {
    foreach ($issue in ($routingIssues | Sort-Object kind, tag)) {
        $summaryLines += "- $($issue.kind): $($issue.tag): $($issue.detail)"
    }
    foreach ($alias in ($unmappedAliases | Sort-Object -Unique)) {
        $summaryLines += "- unmapped alias: $alias"
    }
}
$summaryLines += @(
    '',
    'The inventory is discovery evidence only. Create a ledger using the skill reference before assigning pass statuses.'
)
$summaryLines | Set-Content -LiteralPath $summaryPath -Encoding UTF8

Write-Host "WinForge smoke inventory created:"
Write-Host "  $jsonPath"
Write-Host "  $csvPath"
Write-Host "  $summaryPath"
Write-Host "Routes: $($manifest.counts.routes); source files: $($manifest.counts.sourceFiles); source lines: $($manifest.counts.sourceLines)"
