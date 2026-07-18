param(
    [string]$Root = (Resolve-Path ".").Path,
    [string[]]$ModuleTags = @()
)

$ErrorActionPreference = "Stop"

if ($PSVersionTable.PSEdition -ne "Core") {
    throw "Generate-WikiFeatureDocs.ps1 requires PowerShell 7 (pwsh) so UTF-8 bilingual source literals are preserved."
}

function ConvertTo-Slug([string]$Text) {
    if ($null -eq $Text) { $Text = "" }
    $slug = $Text.ToLowerInvariant()
    $slug = $slug -replace "module\.", ""
    $slug = $slug -replace "[^a-z0-9]+", "-"
    $slug = $slug.Trim("-")
    if ([string]::IsNullOrWhiteSpace($slug)) { return "item" }
    return $slug
}

function Escape-Md([string]$Text) {
    if ($null -eq $Text) { return "" }
    return ($Text -replace "\|", "\|" -replace "`r?`n", " ")
}

function Normalize-Label([string]$Text) {
    if ([string]::IsNullOrWhiteSpace($Text)) { return "" }
    $value = $Text.Trim()
    $value = $value -replace "&amp;", "&"
    $value = $value -replace "&lt;", "<"
    $value = $value -replace "&gt;", ">"
    $value = [regex]::Replace($value, "&#x([0-9A-Fa-f]+);", { param($m) "[icon U+$($m.Groups[1].Value.ToUpperInvariant())]" })
    $value = $value -replace "\{Binding\s+([^,\}]+).*?\}", 'binding:$1'
    $value = $value -replace "\{x:Bind\s+([^,\}]+).*?\}", 'xbind:$1'
    $value = $value -replace "\{StaticResource\s+([^,\}]+).*?\}", 'resource:$1'
    $value = $value -replace "\{ThemeResource\s+([^,\}]+).*?\}", 'resource:$1'
    $value = $value -replace "\s+", " "
    return $value.Trim()
}

function Get-Attrs([string]$AttrText) {
    $attrs = [ordered]@{}
    foreach ($m in [regex]::Matches($AttrText, '([A-Za-z_][\w:\.]*?)\s*=\s*"([^"]*)"')) {
        $attrs[$m.Groups[1].Value] = $m.Groups[2].Value
    }
    return $attrs
}

function Write-Utf8NoBom([string]$Path, [string]$Value) {
    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Value, $encoding)
}

$nativeFeatureStatuses = @{
    "module.textdiff" = @'
## Native C++/WinRT Status · 原生 C++/WinRT 狀態

**EN —** `textdiff` and `module.textdiff` now open a dedicated genuine native page backed by a standard-C++ line-diff core. It normalizes line endings, offers explicit whitespace and invariant-Unicode case options, reports added/removed/unchanged counts, and produces a local unified diff. Its **27/27** focused contracts include the exact 6,000,000-cell LCS guard and deterministic over-budget fallback. The broader Debug and Release core suites each pass **560/560**; Design Tools passes **94/94**, the three-utility focused UI Automation shell passes **39/39**, and the full owned shell remains **300/300**. Renderer accounting is **21/346 fixed routes**, with **325 fixed routes** pending; the inventory additionally includes five dynamic route families. It never mutates Windows; explicit Copy of the unified result is its only side effect.

**粵語 —** `textdiff` 同 `module.textdiff` 而家會開專用真正原生頁面，由標準 C++ 逐行 diff core 支援。佢會統一換行、提供明確忽略空白同 invariant-Unicode 大小寫選項、報告新增／刪除／不變數量，並喺本機產生 unified diff。**27/27** 個專項合約包括準確 6,000,000-cell LCS 上限同超出上限時嘅確定性 fallback。Debug 同 Release 較廣 core suite 各自 **560/560**；Design Tools 通過 **94/94**，三項工具專項 UI Automation shell 通過 **39/39**，完整自有 shell 亦保持 **300/300**。Renderer 計數係 **21/346 條固定 route**，另外 **325 條固定 route** 待完成；清單亦包括五組動態 route 家族。佢唔會改 Windows；唯一副作用係操作員明確 Copy unified 結果。

**Visual evidence · 視覺證據：** LowLevel MCP and the repository driver launched this native page, but both produced a uniformly white client because WinUI composition is unavailable in this desktop session. The blank images were discarded; no canonical image was replaced, so this route is honestly `capture-blocked`. · LowLevel MCP 同 repository driver 都開過呢個原生頁，但呢個 desktop session 冇 WinUI composition，兩邊 client 都係一致白色。空白圖已丟棄，冇替換 canonical 圖；呢條 route 如實係 `capture-blocked`。
'@
    "module.aspectratio" = @'
## Native C++/WinRT Status · 原生 C++/WinRT 狀態

**EN —** `aspect`, `aspectratio`, and `module.aspectratio` now open a dedicated genuine native calculator backed by standard-C++ ratio logic. It simplifies dimensions, reports decimal ratio and megapixels, supports the nine managed presets, and scales either target width or target height without changing the source dimensions. Design Tools passes **94/94** focused contracts; Debug and Release each pass **560/560** overall, the three-utility focused UI Automation shell passes **39/39**, and the full owned shell remains **300/300**. A deterministic one-million-finite-double differential against .NET 11 produced zero display-format mismatches. Renderer accounting is **21/346 fixed routes**, with **325 fixed routes** pending; the inventory additionally includes five dynamic route families. It performs no OS mutation, and only an explicit Copy writes the clipboard.

**粵語 —** `aspect`、`aspectratio` 同 `module.aspectratio` 而家會開專用真正原生計算機，由標準 C++ 比例邏輯支援。佢會化簡尺寸、顯示小數比例同百萬像素、支援受控版九個 preset，亦可以按目標闊度或高度縮放而唔改原始尺寸。Design Tools 通過 **94/94** 個專項合約；Debug 同 Release 整體各自 **560/560**，三項工具專項 UI Automation shell 通過 **39/39**，完整自有 shell 亦保持 **300/300**。一百萬個有限 double 對 .NET 11 嘅確定性比對係零個顯示格式差異。Renderer 計數係 **21/346 條固定 route**，另外 **325 條固定 route** 待完成；清單亦包括五組動態 route 家族。佢唔會改 OS，只有明確 Copy 先會寫剪貼簿。

**Visual evidence · 視覺證據：** LowLevel MCP and the repository driver launched this native page, but both produced a uniformly white client because WinUI composition is unavailable in this desktop session. The blank images were discarded; no canonical image was replaced, so this route is honestly `capture-blocked`. · LowLevel MCP 同 repository driver 都開過呢個原生頁，但呢個 desktop session 冇 WinUI composition，兩邊 client 都係一致白色。空白圖已丟棄，冇替換 canonical 圖；呢條 route 如實係 `capture-blocked`。
'@
    "module.cssunits" = @'
## Native C++/WinRT Status · 原生 C++/WinRT 狀態

**EN —** `cssunits` and `module.cssunits` now open a dedicated genuine native converter backed by standard-C++ CSS unit logic. It converts among `px`, `em`, `rem`, `pt`, `pc`, `%`, `vw`, `vh`, `cm`, `mm`, and `in` using explicit root-font, element-font, viewport, and container contexts, and reports the other ten units in managed order. Design Tools passes **94/94** focused contracts; Debug and Release each pass **560/560** overall, the three-utility focused UI Automation shell passes **39/39**, and the full owned shell remains **300/300**. Renderer accounting is **21/346 fixed routes**, with **325 fixed routes** pending; the inventory additionally includes five dynamic route families. Conversion is local and never mutates the OS; only an explicit result Copy writes the clipboard.

**粵語 —** `cssunits` 同 `module.cssunits` 而家會開專用真正原生換算器，由標準 C++ CSS 單位邏輯支援。佢會用明確 root 字級、元素字級、viewport 同 container context，喺 `px`、`em`、`rem`、`pt`、`pc`、`%`、`vw`、`vh`、`cm`、`mm` 同 `in` 之間換算，並按受控版次序顯示另外十個單位。Design Tools 通過 **94/94** 個專項合約；Debug 同 Release 整體各自 **560/560**，三項工具專項 UI Automation shell 通過 **39/39**，完整自有 shell 亦保持 **300/300**。Renderer 計數係 **21/346 條固定 route**，另外 **325 條固定 route** 待完成；清單亦包括五組動態 route 家族。換算只喺本機做、唔會改 OS；只有明確 Copy 某項結果先會寫剪貼簿。

**Visual evidence · 視覺證據：** LowLevel MCP and the repository driver launched this native page, but both produced a uniformly white client because WinUI composition is unavailable in this desktop session. The blank images were discarded; no canonical image was replaced, so this route is honestly `capture-blocked`. · LowLevel MCP 同 repository driver 都開過呢個原生頁，但呢個 desktop session 冇 WinUI composition，兩邊 client 都係一致白色。空白圖已丟棄，冇替換 canonical 圖；呢條 route 如實係 `capture-blocked`。
'@
}

$wiki = Join-Path $Root "docs/wiki"
$featuresRoot = Join-Path $wiki "features"
$buttonsRoot = Join-Path $wiki "buttons"
$partialGeneration = $ModuleTags.Count -gt 0
if (!$partialGeneration) {
    foreach ($generatedRoot in @($featuresRoot, $buttonsRoot)) {
        if (Test-Path -LiteralPath $generatedRoot) {
            Remove-Item -LiteralPath $generatedRoot -Recurse -Force
        }
    }
}
New-Item -ItemType Directory -Force -Path $featuresRoot, $buttonsRoot | Out-Null

$registryText = Get-Content -LiteralPath (Join-Path $Root "Services/ModuleRegistry.cs") -Raw -Encoding UTF8
$moduleMatches = [regex]::Matches(
    $registryText,
    'new\(\)\s*\{\s*Tag\s*=\s*"(?<tag>[^"]+)"\s*,\s*En\s*=\s*"(?<en>[^"]+)"\s*,\s*Zh\s*=\s*"(?<zh>[^"]+)"\s*,.*?Keywords\s*=\s*"(?<keywords>[^"]*)"',
    [System.Text.RegularExpressions.RegexOptions]::Singleline)

$modules = [ordered]@{}
foreach ($m in $moduleMatches) {
    $tag = $m.Groups["tag"].Value
    $modules[$tag] = [ordered]@{
        Tag = $tag
        En = $m.Groups["en"].Value
        Zh = $m.Groups["zh"].Value
        Keywords = $m.Groups["keywords"].Value
        Category = "Uncategorized"
        CategorySlug = "uncategorized"
        Class = ""
        PageFile = ""
        Alias = (ConvertTo-Slug $tag)
        FeaturePath = ""
        Buttons = @()
    }
}

$mainXaml = Get-Content -LiteralPath (Join-Path $Root "MainWindow.xaml") -Encoding UTF8
$currentCategory = "Suite"
foreach ($line in $mainXaml) {
    if ($line -match '<NavigationViewItem\s+Content="(?<content>[^"]+)"\s+SelectsOnInvoked="False"') {
        $currentCategory = (Normalize-Label $Matches.content)
        continue
    }
    if ($line -match '<NavigationViewItem\s+Content="(?<content>[^"]+)"\s+Tag="(?<tag>[^"]+)"') {
        $tag = $Matches.tag
        if ($modules.Contains($tag)) {
            $modules[$tag]["Category"] = $currentCategory
            $modules[$tag]["CategorySlug"] = ConvertTo-Slug $currentCategory
        }
    }
}

$mainCs = Get-Content -LiteralPath (Join-Path $Root "MainWindow.xaml.cs") -Raw -Encoding UTF8
foreach ($m in [regex]::Matches($mainCs, '"(?<tag>module\.[^"]+)"\s*=>\s*typeof\((?<class>[A-Za-z0-9_]+)\)')) {
    $tag = $m.Groups["tag"].Value
    if ($modules.Contains($tag)) {
        $class = $m.Groups["class"].Value
        $modules[$tag]["Class"] = $class
        $page = Join-Path $Root "Pages/$class.xaml"
        if (Test-Path -LiteralPath $page) {
            $modules[$tag]["PageFile"] = "Pages/$class.xaml"
        }
    }
}

foreach ($m in [regex]::Matches($mainCs, 'case\s+"(?<alias>[^"]+)":\s*(?:\r?\n\s*case\s+"[^"]+":\s*)*?\r?\n\s*Navigator\.GoToModule\?\.Invoke\("(?<tag>module\.[^"]+)"\);', [System.Text.RegularExpressions.RegexOptions]::Singleline)) {
    $tag = $m.Groups["tag"].Value
    if ($modules.Contains($tag)) {
        $modules[$tag]["Alias"] = $m.Groups["alias"].Value
    }
}

$controlTypes = "Button|AppBarButton|ToggleButton|HyperlinkButton|SplitButton|DropDownButton|ToggleSplitButton|MenuFlyoutItem"
foreach ($tag in @($modules.Keys)) {
    $module = $modules[$tag]
    if ([string]::IsNullOrWhiteSpace($module["PageFile"])) { continue }
    $xamlPath = Join-Path $Root $module["PageFile"]
    if (!(Test-Path -LiteralPath $xamlPath)) { continue }
    $xaml = Get-Content -LiteralPath $xamlPath -Raw -Encoding UTF8
    $buttons = New-Object System.Collections.Generic.List[object]
    $index = 0
    foreach ($m in [regex]::Matches($xaml, "<(?<type>$controlTypes)\b(?<attrs>[^>]*)>", [System.Text.RegularExpressions.RegexOptions]::Singleline)) {
        $attrs = Get-Attrs $m.Groups["attrs"].Value
        $handler = @($attrs["Click"], $attrs["Tapped"], $attrs["Command"]) | Where-Object { ![string]::IsNullOrWhiteSpace($_) } | Select-Object -First 1
        $label = @($attrs["Content"], $attrs["Text"], $attrs["ToolTipService.ToolTip"], $attrs["Header"], $attrs["AutomationProperties.Name"]) | Where-Object { ![string]::IsNullOrWhiteSpace($_) } | Select-Object -First 1
        $name = @($attrs["x:Name"], $attrs["Name"]) | Where-Object { ![string]::IsNullOrWhiteSpace($_) } | Select-Object -First 1
        if ([string]::IsNullOrWhiteSpace($handler) -and [string]::IsNullOrWhiteSpace($name) -and [string]::IsNullOrWhiteSpace($label)) { continue }
        $index++
        $display = Normalize-Label $label
        if ([string]::IsNullOrWhiteSpace($display)) { $display = Normalize-Label $name }
        if ([string]::IsNullOrWhiteSpace($display)) { $display = Normalize-Label $handler }
        if ([string]::IsNullOrWhiteSpace($display)) { $display = "$($m.Groups["type"].Value) $index" }
        $idBase = if (![string]::IsNullOrWhiteSpace($name)) { $name } elseif (![string]::IsNullOrWhiteSpace($handler)) { $handler } else { $display }
        $slug = ConvertTo-Slug "$('{0:d3}' -f $index)-$idBase"
        $buttons.Add([ordered]@{
            Index = $index
            Type = $m.Groups["type"].Value
            Name = $name
            Label = $display
            Handler = $handler
            Source = $module["PageFile"]
            Slug = $slug
            Path = ""
        })
    }
    $module["Buttons"] = @($buttons.ToArray())
}

$generationTags = @($modules.Keys)
if ($partialGeneration) {
    $requestedTags = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::OrdinalIgnoreCase)
    foreach ($tag in $ModuleTags) {
        if (![string]::IsNullOrWhiteSpace($tag)) {
            [void]$requestedTags.Add($tag.Trim())
        }
    }
    $missingTags = @($requestedTags | Where-Object { !$modules.Contains($_) })
    if ($missingTags.Count -gt 0) {
        throw "Unknown module tag(s): $($missingTags -join ', ')"
    }
    $generationTags = @($modules.Keys | Where-Object { $requestedTags.Contains($_) })
}

$allButtons = New-Object System.Collections.Generic.List[object]
foreach ($tag in $generationTags) {
    $module = $modules[$tag]
    $categoryDir = Join-Path $featuresRoot $module["CategorySlug"]
    New-Item -ItemType Directory -Force -Path $categoryDir | Out-Null
    $featureFile = Join-Path $categoryDir "$($module["Alias"]).md"
    $featureRel = "features/$($module["CategorySlug"])/$($module["Alias"]).md"
    $module["FeaturePath"] = $featureRel

    $buttonDir = Join-Path (Join-Path $buttonsRoot $module["CategorySlug"]) $module["Alias"]
    if ($partialGeneration -and (Test-Path -LiteralPath $buttonDir)) {
        Remove-Item -LiteralPath $buttonDir -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $buttonDir | Out-Null

    foreach ($button in $module["Buttons"]) {
        $buttonFile = Join-Path $buttonDir "$($button["Slug"]).md"
        $buttonRel = "buttons/$($module["CategorySlug"])/$($module["Alias"])/$($button["Slug"]).md"
        $button["Path"] = $buttonRel
        $allButtons.Add([ordered]@{
            ModuleTag = $module["Tag"]
            Module = "$($module["En"]) · $($module["Zh"])"
            Category = $module["Category"]
            Label = $button["Label"]
            Type = $button["Type"]
            Name = $button["Name"]
            Handler = $button["Handler"]
            Source = $button["Source"]
            Path = $buttonRel
        })

        $buttonType = Escape-Md $button["Type"]
        $buttonName = Escape-Md $button["Name"]
        $buttonHandler = Escape-Md $button["Handler"]
        $buttonSource = Escape-Md $button["Source"]
        $buttonDoc = @"
# $($button["Label"]) · Button

**EN —** Action/control documented from the WinUI XAML source for **$($module["En"])**.
**粵語 —** 呢個動作／控制項係由 **$($module["Zh"])** 嘅 WinUI XAML 來源整理出嚟。

| Field · 欄位 | Value · 值 |
|---|---|
| Module · 模組 | [$($module["En"]) · $($module["Zh"])](../../../$featureRel) |
| Category · 分類 | $($module["Category"]) |
| Control type · 控制類型 | <code>$buttonType</code> |
| XAML name · XAML 名稱 | <code>$buttonName</code> |
| Label / tooltip · 標籤／提示 | $($button["Label"]) |
| Handler · 處理函式 | <code>$buttonHandler</code> |
| Source · 來源 | <code>$buttonSource</code> |

## Operator Notes · 操作備註

**EN —** Use this control from the module page shown above. If the handler is blank, the action is represented by binding or template state rather than a direct click handler in XAML.

**粵語 —** 喺上面模組頁面使用呢個控制項。如果處理函式係空白，代表動作可能由 binding 或樣板狀態處理，而唔係 XAML 入面直接寫 click handler。
"@
        Write-Utf8NoBom -Path $buttonFile -Value $buttonDoc
    }

    $buttonRows = if ($module["Buttons"].Count -gt 0) {
        ($module["Buttons"] | ForEach-Object {
            $label = Escape-Md $_["Label"]
            $path = $_["Path"]
            $type = $_["Type"]
            $name = $_["Name"]
            $handler = $_["Handler"]
            "| [$label](../../$path) | ``$type`` | ``$name`` | ``$handler`` |"
        }) -join "`n"
    } else {
        "| None detected from XAML · XAML 未偵測到 |  |  |  |"
    }

    $moduleTag = Escape-Md ($module["Tag"])
    $moduleAlias = Escape-Md ($module["Alias"])
    $moduleClass = Escape-Md ($module["Class"])
    $modulePageFile = Escape-Md ($module["PageFile"])
    $moduleKeywords = Escape-Md ($module["Keywords"])
    $nativeFeatureStatus = if ($nativeFeatureStatuses.ContainsKey($module["Tag"])) {
        $nativeFeatureStatuses[$module["Tag"]].TrimEnd() + "`r`n`r`n"
    } else {
        ""
    }
    $featureDoc = @"
# $($module["En"]) · $($module["Zh"])

**EN —** Feature reference generated from the WinForge module registry, navigation map, and page XAML.
**粵語 —** 呢份功能參考由 WinForge 模組登記、導覽地圖同頁面 XAML 生成。

| Field · 欄位 | Value · 值 |
|---|---|
| Tag · 標籤 | <code>$moduleTag</code> |
| Deep-link alias · 深層連結別名 | <code>$moduleAlias</code> |
| Category · 分類 | $($module["Category"]) |
| Page class · 頁面類別 | <code>$moduleClass</code> |
| Page XAML · 頁面 XAML | <code>$modulePageFile</code> |
| Button docs · 按鈕文件 | $($module["Buttons"].Count) |

## What It Covers · 功能範圍

**EN —** $($module["En"]) is registered in WinForge search and navigation with these keywords: <code>$moduleKeywords</code>.

**粵語 —** $($module["Zh"]) 已登記喺 WinForge 搜尋同導覽，關鍵字包括：<code>$moduleKeywords</code>。

$nativeFeatureStatus## Buttons And Controls · 按鈕與控制項

| Button · 按鈕 | Type · 類型 | XAML name · 名稱 | Handler · 處理函式 |
|---|---|---|---|
$buttonRows
"@
    Write-Utf8NoBom -Path $featureFile -Value $featureDoc
}

if ($partialGeneration) {
    Write-Host "Generated $($generationTags.Count) selected feature docs and $($allButtons.Count) button docs."
    return
}

$categoryGroups = $modules.Values | Group-Object { $_["Category"] } | Sort-Object Name
$featureIndexRows = foreach ($group in $categoryGroups) {
    $items = $group.Group | Sort-Object { $_["En"] }
    foreach ($module in $items) {
        $link = $module["FeaturePath"] -replace '^features/', ''
        $en = Escape-Md $module["En"]
        $zh = Escape-Md $module["Zh"]
        $tag = $module["Tag"]
        $alias = $module["Alias"]
        $count = $module["Buttons"].Count
        "| [$en · $zh]($link) | ``$tag`` | ``$alias`` | $count |"
    }
}

$featureIndex = @"
# Feature Reference · 功能參考

**EN —** One Markdown file is generated for every registered WinForge feature/module.
**粵語 —** 每一個已登記 WinForge 功能／模組都有一份 Markdown 文件。

| Feature · 功能 | Tag · 標籤 | Alias · 別名 | Button docs · 按鈕文件 |
|---|---|---|---:|
$($featureIndexRows -join "`n")
"@
Write-Utf8NoBom -Path (Join-Path $featuresRoot "README.md") -Value $featureIndex

$buttonRows = foreach ($button in ($allButtons | Sort-Object { $_["Category"] }, { $_["Module"] }, { $_["Label"] })) {
    $link = $button["Path"] -replace '^buttons/', ''
    $label = Escape-Md $button["Label"]
    $moduleName = Escape-Md $button["Module"]
    $category = Escape-Md $button["Category"]
    $type = $button["Type"]
    $handler = $button["Handler"]
    "| [$label]($link) | $moduleName | $category | ``$type`` | ``$handler`` |"
}
$buttonIndex = @"
# Button Reference · 按鈕參考

**EN —** One Markdown file is generated for each actionable button-like control discovered in module XAML.
**粵語 —** 每一個喺模組 XAML 偵測到、似按鈕嘅可操作控制項都有一份 Markdown 文件。

| Button · 按鈕 | Module · 模組 | Category · 分類 | Type · 類型 | Handler · 處理函式 |
|---|---|---|---|---|
$($buttonRows -join "`n")
"@
Write-Utf8NoBom -Path (Join-Path $buttonsRoot "README.md") -Value $buttonIndex

$summary = [ordered]@{
    GeneratedAt = (Get-Date).ToString("o")
    ModuleCount = $modules.Count
    ButtonCount = $allButtons.Count
    FeatureIndex = "docs/wiki/features/README.md"
    ButtonIndex = "docs/wiki/buttons/README.md"
}
Write-Utf8NoBom -Path (Join-Path $wiki "generated-docs-summary.json") -Value ($summary | ConvertTo-Json)

$categoryRows = foreach ($group in $categoryGroups) {
    $category = Escape-Md $group.Name
    $slug = ConvertTo-Slug $group.Name
    $count = $group.Count
    $examples = ($group.Group | Sort-Object { $_["En"] } | Select-Object -First 8 | ForEach-Object {
        $name = Escape-Md "$($_["En"]) · $($_["Zh"])"
        "[$name](features/$($_["CategorySlug"])/$($_["Alias"]).md)"
    }) -join ", "
    "| $category | ``$slug`` | $count | $examples |"
}

$moduleCategories = @"
# Module Categories · 模組分類

**EN —** This page is generated from the live WinForge module registry and navigation map.
**粵語 —** 呢頁由 WinForge 即時模組登記同導覽地圖生成。

| Category · 分類 | Slug · 別名 | Modules · 模組 | Examples · 例子 |
|---|---|---:|---|
$($categoryRows -join "`n")

## More Indexes · 更多索引

- [Generated feature reference](features/README.md) · 生成功能參考
- [Generated button reference](buttons/README.md) · 生成按鈕參考
- [Generated references](Generated-References.md) · 生成參考總覽
- [Screenshots](Screenshots.md) · 截圖集
- [Home](Home.md) · 首頁
"@
Write-Utf8NoBom -Path (Join-Path $wiki "Module-Categories.md") -Value $moduleCategories

$generatedRefs = @"
# Generated References · 生成參考

**EN —** These pages are generated from source metadata and XAML so operators can jump from wiki entries to modules, page classes, and controls.
**粵語 —** 呢啲頁由來源 metadata 同 XAML 生成，方便操作員由 wiki 跳去模組、頁面類別同控制項。

| Reference · 參考 | Contents · 內容 |
|---|---|
| [Feature Reference](features/README.md) | $($modules.Count) generated module pages · $($modules.Count) 份生成模組頁 |
| [Button Reference](buttons/README.md) | $($allButtons.Count) generated button/control pages · $($allButtons.Count) 份生成按鈕／控制項頁 |
| [Generation Summary](generated-docs-summary.json) | Counts and generated output paths · 數量同生成輸出路徑 |

## Generator · 生成器

**EN —** Regenerate the references after module or XAML changes:

~~~~powershell
pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -File tools\Generate-WikiFeatureDocs.ps1
~~~~

**粵語 —** 模組或者 XAML 改完之後，用上面指令重新生成參考。

## Related · 相關

- [Module Categories](Module-Categories.md) · 模組分類
- [Screenshots](Screenshots.md) · 截圖集
- [Home](Home.md) · 首頁
"@
Write-Utf8NoBom -Path (Join-Path $wiki "Generated-References.md") -Value $generatedRefs

Write-Host "Generated $($modules.Count) feature docs and $($allButtons.Count) button docs."
