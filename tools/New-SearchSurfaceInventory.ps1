[CmdletBinding()]
param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [string]$CsvPath = 'docs/audits/search-surface-inventory-2026-07-24.csv',
    [string]$MarkdownPath = 'docs/audits/search-surface-inventory-2026-07-24.md'
)

$ErrorActionPreference = 'Stop'
$RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path

$integrated = @{
    'Pages/DashboardPage.xaml#SearchBox' = 'Core dashboard catalog search.'
    'Pages/CategoryPage.xaml#FilterBox' = 'Per-category tweak catalog search.'
    'Pages/SearchResultsPage.xaml#SearchBox' = 'Combined modules and tweak search.'
    'Pages/ManualPage.xaml#FilterBox' = 'In-app manual search.'
    'Pages/AppLauncherModule.xaml#SearchBox' = 'External app catalog search.'
    'Pages/LicensesPage.xaml#SearchBox' = 'License and source notice search.'
    'Pages/OpenSourceAppHubModule.xaml#SearchBox' = 'Native OSS clone catalog search.'
    'Pages/SettingsHubModule.xaml#FilterBox' = 'In-app and Windows settings catalogs.'
    'Pages/OfflineDocsPage.xaml#SearchBox' = 'Offline article title/body search using the shared matcher.'
    'Pages/SupportTicketsPage.xaml#TicketSearchBox' = 'Local support-ticket search using the shared matcher.'
    'Pages/TotpModule.xaml#EntrySearchBox' = 'Vault-entry metadata search using the shared matcher.'
}

$specialized = @{
    'Pages/AndroidAdbModule.xaml#LogTagBox' = 'ADB logcat tag selector; preserve logcat semantics.'
    'Pages/AwsCliModule.xaml#ResourceQueryBox' = 'AWS/JMESPath resource query; never reinterpret as .NET regex.'
    'Pages/EverythingSearchModule.xaml#SearchBox' = 'File-index query with its own regex/provider contract; needs a dedicated adapter.'
    'Pages/HexEditorModule.xaml#FindBox' = 'Hex/text byte search with a selected encoding mode.'
    'Pages/PackageManagerModule.xaml#SearchBox' = 'Remote package-provider query; local regex requires a provider-aware result adapter.'
    'Pages/RenameModule.xaml#FindBox' = 'Rename transformation input; preserve its explicit rename/regex semantics.'
    'Pages/WiresharkModule.xaml#CaptureFilterBox' = 'BPF capture-filter dialect.'
    'Pages/WiresharkModule.xaml#DisplayFilterBox' = 'Wireshark display-filter dialect.'
    'Pages/WiresharkModule.xaml#FileFilterBox' = 'Wireshark display-filter dialect for saved captures.'
}

$dedicated = @{
    'Pages/AudioTaggerModule.xaml#FromNamePattern' = 'Tag-to-filename template, not a search bar.'
    'Pages/AudioTaggerModule.xaml#ToNamePattern' = 'Filename-to-tag template, not a search bar.'
    'Pages/BulkOpsModule.xaml#PatternBox' = 'Bulk file-operation pattern configured with the page mode.'
    'Pages/GlobTesterModule.xaml#PatternBox' = 'Glob dialect test input.'
    'Pages/GlobTesterModule.xaml#RegexBox' = 'Read-only generated regex output.'
    'Pages/JsonPathModule.xaml#QueryBox' = 'JSONPath dialect test input.'
    'Pages/RegexTesterModule.xaml#PatternBox' = 'Dedicated full .NET regex builder/tester already shipped.'
    'Pages/UrlToolsModule.xaml#QueryField' = 'Read-only parsed URL query output.'
}

$componentInternals = @{
    'Controls/SearchPatternBox.xaml#QueryBox' = 'Primary query editor owned by the reusable synchronized search component.'
    'Controls/SearchPatternBox.xaml#RawPatternBox' = 'Raw regex editor owned by the reusable synchronized search component.'
    'Controls/SearchPatternBox.xaml#SampleBox' = 'Session-only preview sample owned by the reusable synchronized search component.'
}

# Code-built search fields are kept in this hand-written manifest because a XAML-only
# traversal cannot see them. The evidence marker is checked before the row is emitted so
# a renamed or deleted field cannot silently shrink the inventory.
$codeSearchSurfaces = [ordered]@{
    'Pages/AboutPage.xaml.cs#search' = [pscustomobject]@{
        Evidence = 'new SearchPatternBox'; Type = 'SearchPatternBox'; Classification = 'integrated-core'; Status = 'shipped'
        Notes = 'Offline changelog search; the complete SearchPatternBox.Spec reaches date-filtered results.'
    }
    'Pages/SettingsPage.xaml.cs#search' = [pscustomobject]@{
        Evidence = 'new SearchPatternBox'; Type = 'SearchPatternBox'; Classification = 'integrated-core'; Status = 'shipped'
        Notes = 'Settings search built in code and bound to the shared matcher.'
    }
    'MainWindow.xaml.cs#NewTabPickerSearchBox' = [pscustomobject]@{
        Evidence = 'new SearchPatternBox'; Type = 'SearchPatternBox'; Classification = 'integrated-core'; Status = 'shipped'
        Notes = 'Code-built new-tab picker search uses the shared matcher, full builder, synchronized flags, category filter, and keyboard-first activation.'
    }
    'MainWindow.xaml.cs#NewTabPickerCategorySearchBox' = [pscustomobject]@{
        Evidence = 'new SearchablePickerBox'; Type = 'SearchablePickerBox'; Classification = 'integrated-core'; Status = 'shipped'
        Notes = 'Code-built category picker owns an anchored SearchPatternBox builder, metadata-aware matching, keyboard selection, no-match/error status, and focus return.'
    }
    'Services/CommandPaletteWindow.cs#_search' = [pscustomobject]@{
        Evidence = 'private readonly SearchPatternBox _search'; Type = 'SearchPatternBox'; Classification = 'integrated-core'; Status = 'shipped'
        Notes = 'Code-built command-palette search uses the shared builder, synchronized flags, query-only Enter, bounded matcher status, and localized accessible names.'
    }
    'Pages/BitwardenConnectionView.cs#_searchBox' = [pscustomobject]@{
        Evidence = '_searchBox = new AutoSuggestBox'; Type = 'AutoSuggestBox'; Classification = 'plain-text-later'; Status = 'remaining'
        Notes = 'Code-built vault search; shared builder migration remains pending.'
    }
    'Pages/PdfToolkitModule.Viewer.cs#_viewerSearchBox' = [pscustomobject]@{
        Evidence = '_viewerSearchBox = new TextBox'; Type = 'TextBox'; Classification = 'plain-text-later'; Status = 'remaining'
        Notes = 'Code-built PDF text search; shared builder migration remains pending.'
    }
}

$files = @(Get-ChildItem -LiteralPath $RepoRoot -Recurse -Filter '*.xaml' -File |
    Where-Object { $_.FullName -notmatch '[\\/](?:bin|obj|\.git|\.artifacts|artifacts|ThirdParty)[\\/]' } |
    Select-Object -ExpandProperty FullName)

$elementPattern = '<(?:[A-Za-z0-9_]+:)?(?:AutoSuggestBox|TextBox|SearchPatternBox)\b(?:(?!</?(?:[A-Za-z0-9_]+:)?(?:AutoSuggestBox|TextBox|SearchPatternBox)\b)[\s\S])*?/?>'
$rows = [System.Collections.Generic.List[object]]::new()

foreach ($file in $files) {
    $text = Get-Content -LiteralPath $file -Raw
    foreach ($match in [regex]::Matches($text, $elementPattern, [Text.RegularExpressions.RegexOptions]::IgnoreCase)) {
        $tag = $match.Value
        if ($tag -notmatch '(?i)search|filter|query|find|pattern|regex|搜尋|篩選|搵') { continue }

        $name = if ($tag -match '(?:x:Name|Name)="([^"]+)"') { $Matches[1] } else { '(unnamed)' }
        $type = if ($tag -match '^<(?:(?:[A-Za-z0-9_]+):)?(AutoSuggestBox|TextBox|SearchPatternBox)') { $Matches[1] } else { 'Unknown' }
        $line = 1 + ([regex]::Matches($text.Substring(0, $match.Index), "`n").Count)
        $relative = [IO.Path]::GetRelativePath($RepoRoot, $file).Replace('\', '/')
        $key = "$relative#$name"

        $classification = 'plain-text-later'
        $status = 'remaining'
        $notes = 'Local plain-text search/filter; eligible for the shared control in a later integration batch.'

        if ($integrated.ContainsKey($key)) {
            $classification = 'integrated-core'
            $status = 'shipped'
            $notes = $integrated[$key]
        }
        elseif ($specialized.ContainsKey($key)) {
            $classification = 'specialized-dialect'
            $status = 'adapter-required'
            $notes = $specialized[$key]
        }
        elseif ($dedicated.ContainsKey($key)) {
            $classification = if ($key -like '*#RegexBox' -or $key -like '*#QueryField') { 'read-only-output' } else { 'dedicated-pattern-tool' }
            $status = 'not-applicable'
            $notes = $dedicated[$key]
        }
        elseif ($componentInternals.ContainsKey($key)) {
            $classification = 'shared-control-internal'
            $status = 'infrastructure'
            $notes = $componentInternals[$key]
        }
        elseif ($relative -eq 'Pages/TextReplaceModule.xaml' -and $name -eq '(unnamed)') {
            $classification = 'dedicated-pattern-tool'
            $status = 'not-applicable'
            $notes = 'Find/replace rule editor inside a data template; preserve its explicit regex option and replacement semantics.'
        }

        $rows.Add([pscustomobject]@{
            Id = if ($name -eq '(unnamed)') { "$relative#unnamed-$line" } else { $key }
            Source = $relative
            Line = $line
            Control = $name
            Type = $type
            Classification = $classification
            Status = $status
            Notes = $notes
        })
    }
}

foreach ($entry in $codeSearchSurfaces.GetEnumerator()) {
    $key = $entry.Key
    $separator = $key.LastIndexOf('#')
    $relative = $key.Substring(0, $separator)
    $control = $key.Substring($separator + 1)
    $absolute = Join-Path $RepoRoot ($relative.Replace('/', [IO.Path]::DirectorySeparatorChar))
    if (-not (Test-Path -LiteralPath $absolute -PathType Leaf)) {
        throw "Required code-built search source is missing: $relative"
    }

    $text = Get-Content -LiteralPath $absolute -Raw
    if (-not $text.Contains([string]$entry.Value.Evidence, [StringComparison]::Ordinal)) {
        throw "Required code-built search evidence is missing: $key -> $($entry.Value.Evidence)"
    }

    $line = 1 + ([regex]::Matches($text.Substring(0, $text.IndexOf([string]$entry.Value.Evidence, [StringComparison]::Ordinal)), "`n").Count)
    $rows.Add([pscustomobject]@{
        Id = $key
        Source = $relative
        Line = $line
        Control = $control
        Type = $entry.Value.Type
        Classification = $entry.Value.Classification
        Status = $entry.Value.Status
        Notes = $entry.Value.Notes
    })
}

$rows = @($rows | Sort-Object Source, Line)
$csvTarget = Join-Path $RepoRoot $CsvPath
$markdownTarget = Join-Path $RepoRoot $MarkdownPath
New-Item -ItemType Directory -Force -Path (Split-Path $csvTarget) | Out-Null
$rows | Export-Csv -LiteralPath $csvTarget -NoTypeInformation -Encoding utf8

$counts = @($rows | Group-Object Classification | Sort-Object Name)
$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('# Search and query surface inventory · 搜尋與查詢介面清單')
$lines.Add('')
$lines.Add('Generated from the current WinUI XAML plus the hand-written code-built search manifest by `tools/New-SearchSurfaceInventory.ps1`. The inventory deliberately separates ordinary product search from domain-specific query languages and configuration fields; a specialized field is never silently reinterpreted as .NET regex. · 呢份清單由現時 WinUI XAML 加手寫 code-built 搜尋 manifest 自動產生，刻意分開一般產品搜尋、專用查詢語言同設定欄位；專用欄位絕對唔會靜靜雞改成 .NET 正則。')
$lines.Add('')
$xamlRows = @($rows | Where-Object { $_.Source -like '*.xaml' })
$codeRows = @($rows | Where-Object { $_.Source -notlike '*.xaml' })
$lines.Add("Total candidate controls: **$($rows.Count)** across **$(@($rows.Source | Sort-Object -Unique).Count)** source files (**$($xamlRows.Count)** XAML controls and **$($codeRows.Count)** code-built controls). · 候選控制項總數：**$($rows.Count)**，分佈喺 **$(@($rows.Source | Sort-Object -Unique).Count)** 個來源檔案（**$($xamlRows.Count)** 個 XAML 控制，同 **$($codeRows.Count)** 個 code-built 控制）。")
$lines.Add('')
$lines.Add('| Classification | Count | Meaning |')
$lines.Add('| --- | ---: | --- |')
$meaning = @{
    'integrated-core' = 'Shared plain-text-first SearchPatternBox is active; the page uses the synchronized .NET pattern and flags.'
    'plain-text-later' = 'Applicable ordinary local search/filter, scheduled for later batches.'
    'specialized-dialect' = 'Requires a domain/provider adapter; do not force .NET regex semantics.'
    'dedicated-pattern-tool' = 'Configuration or purpose-built pattern editor, not a product search bar.'
    'read-only-output' = 'Output field, not editable search input.'
    'shared-control-internal' = 'Internal editor in the reusable synchronized search component, counted once as infrastructure rather than as another product surface.'
}
foreach ($count in $counts) { $lines.Add("| $($count.Name) | $($count.Count) | $($meaning[$count.Name]) |") }
$lines.Add('')
$lines.Add('## Complete classified inventory · 完整分類清單')
$lines.Add('')
$lines.Add('| Source | Control | Classification | Status | Notes |')
$lines.Add('| --- | --- | --- | --- | --- |')
foreach ($row in $rows) {
    $note = ($row.Notes -replace '\|', '\|')
    $lines.Add("| ``$($row.Source):$($row.Line)`` | ``$($row.Control)`` | $($row.Classification) | $($row.Status) | $note |")
}
$lines.Add('')
$lines.Add('The CSV beside this page is the machine-readable ledger. Regenerate both files after adding, removing, renaming, or integrating a candidate surface. · 同目錄 CSV 係機器可讀 ledger；新增、刪除、改名或整合候選介面後要重新產生兩份檔案。')

Set-Content -LiteralPath $markdownTarget -Value $lines -Encoding utf8
Write-Output "PASS search-surface inventory: $($rows.Count) controls / $(@($rows.Source | Sort-Object -Unique).Count) files"
foreach ($count in $counts) { Write-Output ("  {0}: {1}" -f $count.Name, $count.Count) }
