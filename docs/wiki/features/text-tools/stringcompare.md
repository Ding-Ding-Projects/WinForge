# String Compare · 字串相似度

**EN —** Feature reference generated from the WinForge module registry, navigation map, and page XAML.
**粵語 —** 呢份功能參考由 WinForge 模組登記、導覽地圖同頁面 XAML 生成。

| Field · 欄位 | Value · 值 |
|---|---|
| Tag · 標籤 | <code>module.stringcompare</code> |
| Deep-link alias · 深層連結別名 | <code>stringcompare</code> |
| Category · 分類 | Text Tools · 文字工具 |
| Page class · 頁面類別 | <code>StringCompareModule</code> |
| Page XAML · 頁面 XAML | <code>Pages/StringCompareModule.xaml</code> |
| Button docs · 按鈕文件 | 1 |

## What It Covers · 功能範圍

**EN —** String Compare is registered in WinForge search and navigation with these keywords: <code>string compare similarity levenshtein edit distance damerau hamming jaro winkler substring subsequence diff text 字串 相似度 比較 編輯距離 差異 文字</code>.

**粵語 —** 字串相似度 已登記喺 WinForge 搜尋同導覽，關鍵字包括：<code>string compare similarity levenshtein edit distance damerau hamming jaro winkler substring subsequence diff text 字串 相似度 比較 編輯距離 差異 文字</code>。

## Native C++/WinRT Status · 原生 C++/WinRT 狀態

**EN —** `similarity`, `stringcompare`, and `module.stringcompare` now open a dedicated native String Compare page backed by standard-C++ `TextAnalysis`. It preserves managed case/whitespace normalization, UTF-16 length and Hamming metrics, optimal-string-alignment distance, Jaro–Winkler, longest common substring and subsequence, and the greater-than-2,000 distance guard. Exact greedy Jaro matching is optimized subquadratically for large adversarial inputs; navigation away releases retained strings, controls, and rows. All comparison is local and only explicit Copy writes the clipboard.

**粵語 —** `similarity`、`stringcompare` 同 `module.stringcompare` 而家會開專用原生字串相似度頁，由標準 C++ `TextAnalysis` 支援。佢保留 managed 大小寫／空白正規化、UTF-16 長度同 Hamming 指標、optimal-string-alignment distance、Jaro–Winkler、最長公共子串同子序列，以及超過 2,000 時嘅 distance guard。準確 greedy Jaro 匹配已優化到 subquadratic，對大型對抗輸入亦保持效率；離開 route 會釋放保留字串、控制項同列。比較全部只喺本機做，只有明確 Copy 會寫剪貼簿。

**Evidence · 證據：** renderer accounting is **27/346 fixed routes**, leaving **319 fixed routes** plus five dynamic families; Debug and Release core each pass **714/714**, including Text Analysis **84/84**; managed Structured Text Tools passes **7/7**; focused text-analysis UI Automation passes **40/40** across seven aliases; the DPI-aware utility regression passes **39/39**; the final exhaustive native shell passes **388/388 (388 passed, 0 failed)**; catalog parity covers 346 fixed routes plus five families; and the managed build exits 0 with 0 errors. LowLevel MCP 1.28.1 resolved a 1980×1320 native window for each canonical page, but every inspected 1958×1264 client was exactly one white RGB color; run-winforge independently rejected all three blank fallbacks with exit 1. Six invalid PNGs were removed, exact processes/desktops were cleaned, and no canonical screenshot changed, so visual evidence is `capture-blocked`. · Renderer 計 **27/346**，仲有 **319** 條固定 route 同五組動態家族；714/714、84/84、7/7、40/40、39/39、完整 shell **388/388（388 passed、0 failed）**、catalog parity 同 managed 0-error build 已通過。LowLevel 三張 client 都係純白，run-winforge 三次亦拒絕空白 fallback；無效圖同指定 process／desktop 已清理，冇改 canonical 截圖，visual 係 `capture-blocked`。

## Buttons And Controls · 按鈕與控制項

| Button · 按鈕 | Type · 類型 | XAML name · 名稱 | Handler · 處理函式 |
|---|---|---|---|
| [CopyButton](../../buttons/text-tools/stringcompare/001-copybutton.md) | `Button` | `CopyButton` | `Copy_Click` |