# Text Statistics · 文字統計

**EN —** Feature reference generated from the WinForge module registry, navigation map, and page XAML.
**粵語 —** 呢份功能參考由 WinForge 模組登記、導覽地圖同頁面 XAML 生成。

| Field · 欄位 | Value · 值 |
|---|---|
| Tag · 標籤 | <code>module.textstats</code> |
| Deep-link alias · 深層連結別名 | <code>textstats</code> |
| Category · 分類 | Text Tools · 文字工具 |
| Page class · 頁面類別 | <code>TextStatsModule</code> |
| Page XAML · 頁面 XAML | <code>Pages/TextStatsModule.xaml</code> |
| Button docs · 按鈕文件 | 0 |

## What It Covers · 功能範圍

**EN —** Text Statistics is registered in WinForge search and navigation with these keywords: <code>text statistics readability word count characters sentences paragraphs reading time speaking time flesch kincaid grade syllables frequency 文字統計 可讀性 字數 字元 句數 段落 閱讀時間 朗讀時間 易讀度 年級 音節 字頻</code>.

**粵語 —** 文字統計 已登記喺 WinForge 搜尋同導覽，關鍵字包括：<code>text statistics readability word count characters sentences paragraphs reading time speaking time flesch kincaid grade syllables frequency 文字統計 可讀性 字數 字元 句數 段落 閱讀時間 朗讀時間 易讀度 年級 音節 字頻</code>。

## Native C++/WinRT Status · 原生 C++/WinRT 狀態

**EN —** `textstats` and `module.textstats` now open a dedicated native Text Statistics page backed by the standard-C++ `TextAnalysis` core. It preserves managed UTF-16 and CJK-aware word counting, sentence and paragraph counts, average word/sentence lengths, reading and speaking durations, Flesch readability, and the top ten words. Analysis is entirely local and has no clipboard, file, network, process, or operating-system side effect.

**粵語 —** `textstats` 同 `module.textstats` 而家會開專用原生文字統計頁，由標準 C++ `TextAnalysis` core 支援。佢保留 managed 版 UTF-16 同 CJK-aware 字詞統計、句子／段落數量、平均字詞／句子長度、閱讀／朗讀時間、Flesch 可讀性同頭十個常用詞。所有分析只喺本機運算，不會改剪貼簿、檔案、網絡、process 或作業系統。

**Evidence · 證據：** renderer accounting is **27/346 fixed routes**, leaving **319 fixed routes** plus five dynamic families; Debug and Release core each pass **714/714**, including Text Analysis **84/84**; managed Structured Text Tools passes **7/7**; focused text-analysis UI Automation passes **40/40** across seven aliases; the DPI-aware utility regression passes **39/39**; the final exhaustive native shell passes **388/388 (388 passed, 0 failed)**; catalog parity covers 346 fixed routes plus five families; and the managed build exits 0 with 0 errors. LowLevel MCP 1.28.1 resolved a 1980×1320 native window for each canonical page, but every inspected 1958×1264 client was exactly one white RGB color; run-winforge independently rejected all three blank fallbacks with exit 1. Six invalid PNGs were removed, exact processes/desktops were cleaned, and no canonical screenshot changed, so visual evidence is `capture-blocked`. · Renderer 計 **27/346**，仲有 **319** 條固定 route 同五組動態家族；714/714、84/84、7/7、40/40、39/39、完整 shell **388/388（388 passed、0 failed）**、catalog parity 同 managed 0-error build 已通過。LowLevel 三張 client 都係純白，run-winforge 三次亦拒絕空白 fallback；無效圖同指定 process／desktop 已清理，冇改 canonical 截圖，visual 係 `capture-blocked`。

## Buttons And Controls · 按鈕與控制項

| Button · 按鈕 | Type · 類型 | XAML name · 名稱 | Handler · 處理函式 |
|---|---|---|---|
| None detected from XAML · XAML 未偵測到 |  |  |  |