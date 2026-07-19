# Word Frequency · 詞頻統計

**EN —** Feature reference generated from the WinForge module registry, navigation map, and page XAML.
**粵語 —** 呢份功能參考由 WinForge 模組登記、導覽地圖同頁面 XAML 生成。

| Field · 欄位 | Value · 值 |
|---|---|
| Tag · 標籤 | <code>module.wordfreq</code> |
| Deep-link alias · 深層連結別名 | <code>wordfreq</code> |
| Category · 分類 | Text Tools · 文字工具 |
| Page class · 頁面類別 | <code>WordFreqModule</code> |
| Page XAML · 頁面 XAML | <code>Pages/WordFreqModule.xaml</code> |
| Button docs · 按鈕文件 | 1 |

## What It Covers · 功能範圍

**EN —** Word Frequency is registered in WinForge search and navigation with these keywords: <code>word frequency count bigram character stop words rank text analysis csv 詞頻 字頻 統計 排名 文字 分析 計數</code>.

**粵語 —** 詞頻統計 已登記喺 WinForge 搜尋同導覽，關鍵字包括：<code>word frequency count bigram character stop words rank text analysis csv 詞頻 字頻 統計 排名 文字 分析 計數</code>。

## Native C++/WinRT Status · 原生 C++/WinRT 狀態

**EN —** `wordfreq` and `module.wordfreq` now open a dedicated native Word Frequency page backed by standard-C++ `TextAnalysis`. It preserves word, bigram, and Unicode-scalar modes; case, punctuation, number, and managed stop-word options; current-culture ordering; exact count, proportional bar, and percentage rows; and managed-compatible CSV escaping/output. Analysis is local, and only the explicit Copy action writes the clipboard.

**粵語 —** `wordfreq` 同 `module.wordfreq` 而家會開專用原生詞頻統計頁，由標準 C++ `TextAnalysis` 支援。佢保留單詞、bigram 同 Unicode scalar 模式、大小寫／標點／數字／managed 停用詞選項、依目前文化排序、準確數量／比例條形／百分比列，同 managed 相容 CSV escape／輸出。分析只喺本機做，只有明確 Copy 動作會寫剪貼簿。

**Evidence · 證據：** renderer accounting is **27/346 fixed routes**, leaving **319 fixed routes** plus five dynamic families; Debug and Release core each pass **714/714**, including Text Analysis **84/84**; managed Structured Text Tools passes **7/7**; focused text-analysis UI Automation passes **40/40** across seven aliases; the DPI-aware utility regression passes **39/39**; the final exhaustive native shell passes **388/388 (388 passed, 0 failed)**; catalog parity covers 346 fixed routes plus five families; and the managed build exits 0 with 0 errors. LowLevel MCP 1.28.1 resolved a 1980×1320 native window for each canonical page, but every inspected 1958×1264 client was exactly one white RGB color; run-winforge independently rejected all three blank fallbacks with exit 1. Six invalid PNGs were removed, exact processes/desktops were cleaned, and no canonical screenshot changed, so visual evidence is `capture-blocked`. · Renderer 計 **27/346**，仲有 **319** 條固定 route 同五組動態家族；714/714、84/84、7/7、40/40、39/39、完整 shell **388/388（388 passed、0 failed）**、catalog parity 同 managed 0-error build 已通過。LowLevel 三張 client 都係純白，run-winforge 三次亦拒絕空白 fallback；無效圖同指定 process／desktop 已清理，冇改 canonical 截圖，visual 係 `capture-blocked`。

## Buttons And Controls · 按鈕與控制項

| Button · 按鈕 | Type · 類型 | XAML name · 名稱 | Handler · 處理函式 |
|---|---|---|---|
| [CopyBtn](../../buttons/text-tools/wordfreq/001-copybtn.md) | `Button` | `CopyBtn` | `Copy_Click` |