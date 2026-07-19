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
    "module.linetools" = @'
## Native C++/WinRT Status · 原生 C++/WinRT 狀態

**Evidence-count note · 證據計數備註：** The managed build's 161 warnings are recorded only as historical, non-contract output; the durable gate is exit 0 with 0 errors. · Managed build 嘅 161 個 warning 只記錄做歷史、非合約輸出；長期 gate 係 exit 0 同 0 errors。

**EN —** Native source now contains a dedicated Line Tools renderer backed by the shared standard-C++ Line Processing core. It preserves managed-compatible empty and trailing lines, UTF-16 character counts, ASCII word boundaries, both numbering formats, Unicode decimal-number removal, prefix/suffix/quotes, literal join/split, code-unit reversal, ordinal-ignore-case sorting and first-win deduplication, Unicode whitespace cleanup, and cryptographic Fisher–Yates shuffle. Native Debug and Release builds each exit 0; both core executables pass **630/630**, including **70/70** Line Processing contracts. Renderer accounting is **24/346 fixed routes**, with **322 fixed routes** pending plus five dynamic route families.

**粵語 —** 原生 source 而家有專用 Line Tools renderer，由共用標準 C++ Line Processing core 支援。佢保留 managed 相容嘅空行／尾空行、UTF-16 字元統計、ASCII 字詞邊界、兩種編號、Unicode 十進位數字編號移除、前綴／後綴／引號、literal 合併／拆分、code-unit 反轉、ordinal-ignore-case 排序、保留第一項去重、Unicode 空白清理，同密碼學 Fisher–Yates 打亂。原生 Debug／Release build 各 exit 0，兩個 core executable 各 **630/630**，包括 Line Processing **70/70**。Renderer 計數係 **24/346 條固定 route**，仲有 **322 條固定 route** 同五組動態 route 家族待完成。

**Focused validation green; visual capture blocked · 專項驗證通過；視覺擷取受阻：** Catalog parity passes all 346 fixed routes plus five dynamic families, focused `-LineRoutesOnly` UI Automation passes **42/42** across all seven registered aliases, the full owned native shell smoke passes **348/348** with exit 0, and the managed Debug x64 solution build exits 0 with 0 errors (its 161 warnings are historical, non-contract output). LowLevel Computer Use MCP 1.28.1 launched `--page lines`, `--page textsort`, and `--page textwrap` on private desktops; the first pass resolved **852×880** windows at PIDs 29740, 19176, and 25196, and inspected `rendered_ok` `PrintWindow` frames were composition-white/invalid. A compositor retry started PIDs 21696, 23644, and 22048, but `show_headless_desktop` failed for all three with Win32 error 5, Access denied, before monitor capture. All exact processes and desktops were cleaned, including Text Sort's brief 0×0 ghost. Required run-winforge attempts used `-Native -WaitMs 16000`; `CopyFromScreen` was unavailable, blank/near-uniform fallbacks were rejected, and all three exited 1. The artifact directory is empty and no canonical image was replaced, so visual evidence is conclusively `capture-blocked`. Hosted release-per-push and Git integration/remote proof remain pending. · Catalog parity、全部七個 alias 嘅專項 UIA **42/42**、完整自有原生 shell smoke **348/348**，同 managed 0-error build 已通過；161 個 warning 只係歷史、非合約輸出。LowLevel MCP 準確開過三個 canonical command；第一輪解析 **852×880** window，PID 29740／19176／25196，`rendered_ok` 圖檢查後係 composition-white 無效畫面。Compositor retry 開過 PID 21696／23644／22048，但 monitor capture 前已因 Win32 error 5 Access denied 失敗；全部指定 process 同 desktop 已清理，包括 Text Sort 短暫嘅 0×0 ghost。Run-winforge 用 `-Native -WaitMs 16000` 試過三頁，因 `CopyFromScreen` 唔可用同空白 fallback 被拒而全部 exit 1。Artifact directory 已清空，冇替換 canonical 圖，所以 visual 最終係 `capture-blocked`；release 同 Git 證明仍待完成。
'@
    "module.textsort" = @'
## Native C++/WinRT Status · 原生 C++/WinRT 狀態

**Evidence-count note · 證據計數備註：** The managed build's 161 warnings are recorded only as historical, non-contract output; the durable gate is exit 0 with 0 errors. · Managed build 嘅 161 個 warning 只記錄做歷史、非合約輸出；長期 gate 係 exit 0 同 0 errors。

**EN —** Native source now contains a dedicated Line Sort & Dedupe renderer backed by the shared standard-C++ Line Processing core. It preserves the managed operation order—trim, remove blanks, first-win deduplicate, sort, reverse, then shuffle—with keep-order, ordinal ascending/descending, and natural numeric modes. It also preserves case-insensitive and trim-before-compare keys, live input/output/duplicate counts, recompute and re-shuffle actions, option-preserving Clear, and explicit clipboard Copy. Native Debug and Release builds each exit 0; both core executables pass **630/630**, including **70/70** Line Processing contracts. Renderer accounting is **24/346 fixed routes**, with **322 fixed routes** pending plus five dynamic route families.

**粵語 —** 原生 source 而家有專用 Line Sort & Dedupe renderer，由共用標準 C++ Line Processing core 支援。佢保留 managed 次序：修剪、移除空白行、保留第一項去重、排序、反轉、最後打亂；亦有保留原次序、ordinal 升序／降序同自然數字排序。大小寫忽略、比較前修剪、即時輸入／輸出／去重統計、重新計算、再打亂、保留選項嘅 Clear 同明確 Copy 都有保留。原生 Debug／Release build 各 exit 0，兩個 core executable 各 **630/630**，包括 Line Processing **70/70**。Renderer 計數係 **24/346 條固定 route**，仲有 **322 條固定 route** 同五組動態 route 家族待完成。

**Focused validation green; visual capture blocked · 專項驗證通過；視覺擷取受阻：** Catalog parity passes all 346 fixed routes plus five dynamic families, focused `-LineRoutesOnly` UI Automation passes **42/42** across all seven registered aliases, the full owned native shell smoke passes **348/348** with exit 0, and the managed Debug x64 solution build exits 0 with 0 errors (its 161 warnings are historical, non-contract output). LowLevel Computer Use MCP 1.28.1 launched `--page lines`, `--page textsort`, and `--page textwrap` on private desktops; the first pass resolved **852×880** windows at PIDs 29740, 19176, and 25196, and inspected `rendered_ok` `PrintWindow` frames were composition-white/invalid. A compositor retry started PIDs 21696, 23644, and 22048, but `show_headless_desktop` failed for all three with Win32 error 5, Access denied, before monitor capture. All exact processes and desktops were cleaned, including Text Sort's brief 0×0 ghost. Required run-winforge attempts used `-Native -WaitMs 16000`; `CopyFromScreen` was unavailable, blank/near-uniform fallbacks were rejected, and all three exited 1. The artifact directory is empty and no canonical image was replaced, so visual evidence is conclusively `capture-blocked`. Hosted release-per-push and Git integration/remote proof remain pending. · Catalog parity、全部七個 alias 嘅專項 UIA **42/42**、完整自有原生 shell smoke **348/348**，同 managed 0-error build 已通過；161 個 warning 只係歷史、非合約輸出。LowLevel MCP 準確開過三個 canonical command；第一輪解析 **852×880** window，PID 29740／19176／25196，`rendered_ok` 圖檢查後係 composition-white 無效畫面。Compositor retry 開過 PID 21696／23644／22048，但 monitor capture 前已因 Win32 error 5 Access denied 失敗；全部指定 process 同 desktop 已清理，包括 Text Sort 短暫嘅 0×0 ghost。Run-winforge 用 `-Native -WaitMs 16000` 試過三頁，因 `CopyFromScreen` 唔可用同空白 fallback 被拒而全部 exit 1。Artifact directory 已清空，冇替換 canonical 圖，所以 visual 最終係 `capture-blocked`；release 同 Git 證明仍待完成。
'@
    "module.textwrap" = @'
## Native C++/WinRT Status · 原生 C++/WinRT 狀態

**Evidence-count note · 證據計數備註：** The managed build's 161 warnings are recorded only as historical, non-contract output; the durable gate is exit 0 with 0 errors. · Managed build 嘅 161 個 warning 只記錄做歷史、非合約輸出；長期 gate 係 exit 0 同 0 errors。

**EN —** Native source now contains a dedicated Text Wrap renderer backed by the shared standard-C++ Line Processing core. It preserves the managed 72-column default and 1–2000 range, optional long-word chunking, paragraph-aware hard wrap, unwrap and two-pass reflow, output-chaining prefix and hanging-indent actions, UTF-16 readout metrics, and explicit-only clipboard Copy. Native Debug and Release builds each exit 0; both core executables pass **630/630**, including **70/70** Line Processing contracts. Renderer accounting is **24/346 fixed routes**, with **322 fixed routes** pending plus five dynamic route families.

**粵語 —** 原生 source 而家有專用 Text Wrap renderer，由共用標準 C++ Line Processing core 支援。佢保留 managed 版 72 字元預設同 1–2000 範圍、可選長字拆段、按段落硬換行、拉直同兩階段重排、由現有輸出繼續做前綴／懸掛縮排、UTF-16 readout 統計，同只限明確 Copy 嘅剪貼簿操作。原生 Debug／Release build 各 exit 0，兩個 core executable 各 **630/630**，包括 Line Processing **70/70**。Renderer 計數係 **24/346 條固定 route**，仲有 **322 條固定 route** 同五組動態 route 家族待完成。

**Focused validation green; visual capture blocked · 專項驗證通過；視覺擷取受阻：** Catalog parity passes all 346 fixed routes plus five dynamic families, focused `-LineRoutesOnly` UI Automation passes **42/42** across all seven registered aliases, the full owned native shell smoke passes **348/348** with exit 0, and the managed Debug x64 solution build exits 0 with 0 errors (its 161 warnings are historical, non-contract output). LowLevel Computer Use MCP 1.28.1 launched `--page lines`, `--page textsort`, and `--page textwrap` on private desktops; the first pass resolved **852×880** windows at PIDs 29740, 19176, and 25196, and inspected `rendered_ok` `PrintWindow` frames were composition-white/invalid. A compositor retry started PIDs 21696, 23644, and 22048, but `show_headless_desktop` failed for all three with Win32 error 5, Access denied, before monitor capture. All exact processes and desktops were cleaned, including Text Sort's brief 0×0 ghost. Required run-winforge attempts used `-Native -WaitMs 16000`; `CopyFromScreen` was unavailable, blank/near-uniform fallbacks were rejected, and all three exited 1. The artifact directory is empty and no canonical image was replaced, so visual evidence is conclusively `capture-blocked`. Hosted release-per-push and Git integration/remote proof remain pending. · Catalog parity、全部七個 alias 嘅專項 UIA **42/42**、完整自有原生 shell smoke **348/348**，同 managed 0-error build 已通過；161 個 warning 只係歷史、非合約輸出。LowLevel MCP 準確開過三個 canonical command；第一輪解析 **852×880** window，PID 29740／19176／25196，`rendered_ok` 圖檢查後係 composition-white 無效畫面。Compositor retry 開過 PID 21696／23644／22048，但 monitor capture 前已因 Win32 error 5 Access denied 失敗；全部指定 process 同 desktop 已清理，包括 Text Sort 短暫嘅 0×0 ghost。Run-winforge 用 `-Native -WaitMs 16000` 試過三頁，因 `CopyFromScreen` 唔可用同空白 fallback 被拒而全部 exit 1。Artifact directory 已清空，冇替換 canonical 圖，所以 visual 最終係 `capture-blocked`；release 同 Git 證明仍待完成。
'@
    "module.textstats" = @'
## Native C++/WinRT Status · 原生 C++/WinRT 狀態

**EN —** `textstats` and `module.textstats` now open a dedicated native Text Statistics page backed by the standard-C++ `TextAnalysis` core. It preserves managed UTF-16 and CJK-aware word counting, sentence and paragraph counts, average word/sentence lengths, reading and speaking durations, Flesch readability, and the top ten words. Analysis is entirely local and has no clipboard, file, network, process, or operating-system side effect.

**粵語 —** `textstats` 同 `module.textstats` 而家會開專用原生文字統計頁，由標準 C++ `TextAnalysis` core 支援。佢保留 managed 版 UTF-16 同 CJK-aware 字詞統計、句子／段落數量、平均字詞／句子長度、閱讀／朗讀時間、Flesch 可讀性同頭十個常用詞。所有分析只喺本機運算，不會改剪貼簿、檔案、網絡、process 或作業系統。

**Evidence · 證據：** renderer accounting remains **27/346 fixed routes**, leaving **319 fixed routes** plus five dynamic families; Debug and Release core each pass **717/717**, including Text Analysis **87/87**; focused text-analysis UI Automation passes **40/40** across seven aliases; the full native shell passes **388/388 (388 passed, 0 failed)**; and catalog parity covers 346 fixed routes plus five families. The post-c6 `wordfreq` retry remains `capture-blocked`: the repo-local LowLevel checkout was present but not callable in this Codex session, and the repository driver rejected its blank/near-uniform fallback. No PNG was retained or substituted. Only Word Frequency had this fresh retry; Text Statistics and String Compare retain their historical `capture-blocked` records. · Renderer 計數維持 **27/346** 條固定 route，仲有 **319** 條固定 route 同五組動態家族；Debug 同 Release core 各自通過 **717/717**，包括 Text Analysis **87/87**；專項文字分析 UI Automation 喺七個 alias 通過 **40/40**；完整 native shell 通過 **388/388（388 passed、0 failed）**；catalog parity 覆蓋 346 條固定 route 加五組動態家族。c6 後嘅 `wordfreq` 重試仍然係 `capture-blocked`：repo 本機 LowLevel checkout 喺度但目前 Codex session 冇可呼叫工具，而 repository driver 拒絕咗空白／近乎單色 fallback。冇保留或替代 PNG。只有詞頻統計有呢次最新重試；文字統計同字串相似度保留歷史 `capture-blocked` 紀錄。
'@
    "module.wordfreq" = @'
## Native C++/WinRT Status · 原生 C++/WinRT 狀態

**EN —** `wordfreq` and `module.wordfreq` now open a dedicated native Word Frequency page backed by standard-C++ `TextAnalysis`. It preserves word, bigram, and Unicode-scalar modes; case, punctuation, number, and managed stop-word options; current-culture ordering; exact count, proportional bar, and percentage rows; and managed-compatible CSV escaping/output. Its `CurrentCultureIgnoreCase` comparer applies .NET Runtime width/kana tailoring, folds fullwidth case while keeping width and kana significant, keeps canonical-equivalent terms stably ordered, and shares an initialized collator without per-comparison serialization. The named total remains queryable to assistive technology without a per-edit polite live announcement; MIT attribution is in `THIRD-PARTY-NOTICES.txt`. Analysis is local, and only the explicit Copy action writes the clipboard.

**粵語 —** `wordfreq` 同 `module.wordfreq` 而家會開專用原生詞頻統計頁，由標準 C++ `TextAnalysis` 支援。佢保留單詞、bigram 同 Unicode scalar 模式、大小寫／標點／數字／managed 停用詞選項、依目前文化排序、準確數量／比例條形／百分比列，同 managed 相容 CSV escape／輸出。佢嘅 `CurrentCultureIgnoreCase` comparer 採用 .NET Runtime 闊度／假名 tailoring：fullwidth case 會折疊，但闊度同假名仍然有分別；canonical-equivalent 詞語保持穩定次序，而且共用已初始化嘅 collator，唔會每次比較都序列化。命名 total 仍可畀輔助科技查詢，但每次修改唔再發 polite live announcement；MIT 歸屬資料喺 `THIRD-PARTY-NOTICES.txt`。分析只喺本機做，只有明確 Copy 動作會寫剪貼簿。

**Evidence · 證據：** renderer accounting remains **27/346 fixed routes**, leaving **319 fixed routes** plus five dynamic families; Debug and Release core each pass **717/717**, including Text Analysis **87/87**; focused text-analysis UI Automation passes **40/40** across seven aliases; the full native shell passes **388/388 (388 passed, 0 failed)**; and catalog parity covers 346 fixed routes plus five families. The post-c6 `wordfreq` retry remains `capture-blocked`: the repo-local LowLevel checkout was present but not callable in this Codex session, and the repository driver rejected its blank/near-uniform fallback. No PNG was retained or substituted. Only Word Frequency had this fresh retry; Text Statistics and String Compare retain their historical `capture-blocked` records. · Renderer 計數維持 **27/346** 條固定 route，仲有 **319** 條固定 route 同五組動態家族；Debug 同 Release core 各自通過 **717/717**，包括 Text Analysis **87/87**；專項文字分析 UI Automation 喺七個 alias 通過 **40/40**；完整 native shell 通過 **388/388（388 passed、0 failed）**；catalog parity 覆蓋 346 條固定 route 加五組動態家族。c6 後嘅 `wordfreq` 重試仍然係 `capture-blocked`：repo 本機 LowLevel checkout 喺度但目前 Codex session 冇可呼叫工具，而 repository driver 拒絕咗空白／近乎單色 fallback。冇保留或替代 PNG。只有詞頻統計有呢次最新重試；文字統計同字串相似度保留歷史 `capture-blocked` 紀錄。
'@
    "module.stringcompare" = @'
## Native C++/WinRT Status · 原生 C++/WinRT 狀態

**EN —** `similarity`, `stringcompare`, and `module.stringcompare` now open a dedicated native String Compare page backed by standard-C++ `TextAnalysis`. It preserves managed case/whitespace normalization, UTF-16 length and Hamming metrics, optimal-string-alignment distance, Jaro–Winkler, longest common substring and subsequence, and the greater-than-2,000 distance guard. Exact greedy Jaro matching is optimized subquadratically for large adversarial inputs; navigation away releases retained strings, controls, and rows. All comparison is local and only explicit Copy writes the clipboard.

**粵語 —** `similarity`、`stringcompare` 同 `module.stringcompare` 而家會開專用原生字串相似度頁，由標準 C++ `TextAnalysis` 支援。佢保留 managed 大小寫／空白正規化、UTF-16 長度同 Hamming 指標、optimal-string-alignment distance、Jaro–Winkler、最長公共子串同子序列，以及超過 2,000 時嘅 distance guard。準確 greedy Jaro 匹配已優化到 subquadratic，對大型對抗輸入亦保持效率；離開 route 會釋放保留字串、控制項同列。比較全部只喺本機做，只有明確 Copy 會寫剪貼簿。

**Evidence · 證據：** renderer accounting remains **27/346 fixed routes**, leaving **319 fixed routes** plus five dynamic families; Debug and Release core each pass **717/717**, including Text Analysis **87/87**; focused text-analysis UI Automation passes **40/40** across seven aliases; the full native shell passes **388/388 (388 passed, 0 failed)**; and catalog parity covers 346 fixed routes plus five families. The post-c6 `wordfreq` retry remains `capture-blocked`: the repo-local LowLevel checkout was present but not callable in this Codex session, and the repository driver rejected its blank/near-uniform fallback. No PNG was retained or substituted. Only Word Frequency had this fresh retry; Text Statistics and String Compare retain their historical `capture-blocked` records. · Renderer 計數維持 **27/346** 條固定 route，仲有 **319** 條固定 route 同五組動態家族；Debug 同 Release core 各自通過 **717/717**，包括 Text Analysis **87/87**；專項文字分析 UI Automation 喺七個 alias 通過 **40/40**；完整 native shell 通過 **388/388（388 passed、0 failed）**；catalog parity 覆蓋 346 條固定 route 加五組動態家族。c6 後嘅 `wordfreq` 重試仍然係 `capture-blocked`：repo 本機 LowLevel checkout 喺度但目前 Codex session 冇可呼叫工具，而 repository driver 拒絕咗空白／近乎單色 fallback。冇保留或替代 PNG。只有詞頻統計有呢次最新重試；文字統計同字串相似度保留歷史 `capture-blocked` 紀錄。
'@
    "module.phonetic" = @'
## Native C++/WinRT Status · 原生 C++/WinRT 狀態

**EN —** `nato`, `phonetic`, and `module.phonetic` now open a dedicated native Phonetic Speller backed by the standard-C++ `ReferenceText` core. It preserves the managed NATO, police, and simple alphabets, digit names, uppercase and punctuation options, spoken output, per-UTF-16-code-unit rows matching managed enumeration, and explicit-only clipboard Copy.

**粵語 —** `nato`、`phonetic` 同 `module.phonetic` 而家會開專用原生拼讀字母表頁，由標準 C++ `ReferenceText` core 支援。佢保留 managed 版 NATO、警察同簡易字母表、數字讀法、大寫／標點選項、朗讀輸出、同 managed 列舉一致嘅逐 UTF-16 code unit 列，同只限明確 Copy 嘅剪貼簿操作。

**Current local evidence · 目前本機證據：** renderer accounting is **30/346 fixed routes**, leaving **316 fixed routes** plus five dynamic families; native Debug and Release x64 solution builds each exit 0 with 0 errors; Debug and Release core executables each pass **759/759**; catalog parity passes all 346 fixed routes plus five dynamic families; focused reference-text UI Automation passes **29/29** across all eight aliases; and the fresh full native shell smoke passes **417/417 (417 passed, 0 failed)**. LowLevel Computer Use MCP is not callable in this Codex session. A fresh post-integration `htmlentities` repository-driver retry found `CopyFromScreen` unavailable and rejected a blank/near-uniform `PrintWindow` client. No PNG was created or retained, no WinForge process remained, and no canonical screenshot changed, so visual evidence is honestly `capture-blocked`. · Renderer 計 **30/346**，仲有 **316** 條固定 route 同五組動態家族；Debug／Release build 各 0 errors、兩個 core 各 **759/759**、catalog parity 同八個 alias UIA **29/29** 已通過；最新完整 native shell smoke 通過 **417/417（417 passed、0 failed）**。今次 session 冇 LowLevel MCP；整合後最新 `htmlentities` repository-driver 重試發現 `CopyFromScreen` 唔可用，並拒絕空白／近乎單色 `PrintWindow` client。冇建立或保留 PNG、冇殘留 WinForge process、冇改 canonical 截圖，所以 visual 如實係 `capture-blocked`。
'@
    "module.boxtext" = @'
## Native C++/WinRT Status · 原生 C++/WinRT 狀態

**EN —** `boxtext` and `module.boxtext` now open a dedicated native Box & Banner Text page backed by the standard-C++ `ReferenceText` core. It preserves eight managed border/comment styles, left/center/right alignment, bounded padding, optional titles, multiline and tab-aware layout, Unicode display width, live output, and explicit-only clipboard Copy.

**粵語 —** `boxtext` 同 `module.boxtext` 而家會開專用原生文字方框／橫幅頁，由標準 C++ `ReferenceText` core 支援。佢保留 managed 版八種邊框／註解風格、左／中／右對齊、有界 padding、可選標題、多行同 tab-aware 排版、Unicode 顯示闊度、即時輸出，同只限明確 Copy 嘅剪貼簿操作。

**Current local evidence · 目前本機證據：** renderer accounting is **30/346 fixed routes**, leaving **316 fixed routes** plus five dynamic families; native Debug and Release x64 solution builds each exit 0 with 0 errors; Debug and Release core executables each pass **759/759**; catalog parity passes all 346 fixed routes plus five dynamic families; focused reference-text UI Automation passes **29/29** across all eight aliases; and the fresh full native shell smoke passes **417/417 (417 passed, 0 failed)**. LowLevel Computer Use MCP is not callable in this Codex session. A fresh post-integration `htmlentities` repository-driver retry found `CopyFromScreen` unavailable and rejected a blank/near-uniform `PrintWindow` client. No PNG was created or retained, no WinForge process remained, and no canonical screenshot changed, so visual evidence is honestly `capture-blocked`. · Renderer 計 **30/346**，仲有 **316** 條固定 route 同五組動態家族；Debug／Release build 各 0 errors、兩個 core 各 **759/759**、catalog parity 同八個 alias UIA **29/29** 已通過；最新完整 native shell smoke 通過 **417/417（417 passed、0 failed）**。今次 session 冇 LowLevel MCP；整合後最新 `htmlentities` repository-driver 重試發現 `CopyFromScreen` 唔可用，並拒絕空白／近乎單色 `PrintWindow` client。冇建立或保留 PNG、冇殘留 WinForge process、冇改 canonical 截圖，所以 visual 如實係 `capture-blocked`。
'@
    "module.htmlentities" = @'
## Native C++/WinRT Status · 原生 C++/WinRT 狀態

**EN —** `entities`, `htmlentities`, and `module.htmlentities` now open a dedicated native HTML Entities page backed by the standard-C++ `ReferenceText` core. It preserves must-escape and optional non-ASCII encoding, named and decimal/hex numeric decoding, Unicode-scalar handling with invalid UTF-16 safety, managed-compatible scan behavior, a local 50-row bilingual entity reference, live counts, and explicit-only clipboard Copy.

**粵語 —** `entities`、`htmlentities` 同 `module.htmlentities` 而家會開專用原生 HTML 實體頁，由標準 C++ `ReferenceText` core 支援。佢保留必需 escape 同可選非 ASCII 編碼、具名／十進位／十六進位數字解碼、Unicode scalar 同無效 UTF-16 安全處理、managed 相容掃描行為、本機 50 項雙語實體參考、即時統計，同只限明確 Copy 嘅剪貼簿操作。

**Current local evidence · 目前本機證據：** renderer accounting is **30/346 fixed routes**, leaving **316 fixed routes** plus five dynamic families; native Debug and Release x64 solution builds each exit 0 with 0 errors; Debug and Release core executables each pass **759/759**; catalog parity passes all 346 fixed routes plus five dynamic families; focused reference-text UI Automation passes **29/29** across all eight aliases; and the fresh full native shell smoke passes **417/417 (417 passed, 0 failed)**. LowLevel Computer Use MCP is not callable in this Codex session. A fresh post-integration `htmlentities` repository-driver retry found `CopyFromScreen` unavailable and rejected a blank/near-uniform `PrintWindow` client. No PNG was created or retained, no WinForge process remained, and no canonical screenshot changed, so visual evidence is honestly `capture-blocked`. · Renderer 計 **30/346**，仲有 **316** 條固定 route 同五組動態家族；Debug／Release build 各 0 errors、兩個 core 各 **759/759**、catalog parity 同八個 alias UIA **29/29** 已通過；最新完整 native shell smoke 通過 **417/417（417 passed、0 failed）**。今次 session 冇 LowLevel MCP；整合後最新 `htmlentities` repository-driver 重試發現 `CopyFromScreen` 唔可用，並拒絕空白／近乎單色 `PrintWindow` client。冇建立或保留 PNG、冇殘留 WinForge process、冇改 canonical 截圖，所以 visual 如實係 `capture-blocked`。
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
