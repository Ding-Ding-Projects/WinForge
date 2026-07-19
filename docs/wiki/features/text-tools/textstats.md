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

**Evidence · 證據：** renderer accounting remains **27/346 fixed routes**, leaving **319 fixed routes** plus five dynamic families; Debug and Release core each pass **717/717**, including Text Analysis **87/87**; focused text-analysis UI Automation passes **40/40** across seven aliases; the full native shell passes **388/388 (388 passed, 0 failed)**; and catalog parity covers 346 fixed routes plus five families. The post-c6 `wordfreq` retry remains `capture-blocked`: the repo-local LowLevel checkout was present but not callable in this Codex session, and the repository driver rejected its blank/near-uniform fallback. No PNG was retained or substituted. Only Word Frequency had this fresh retry; Text Statistics and String Compare retain their historical `capture-blocked` records. · Renderer 計數維持 **27/346** 條固定 route，仲有 **319** 條固定 route 同五組動態家族；Debug 同 Release core 各自通過 **717/717**，包括 Text Analysis **87/87**；專項文字分析 UI Automation 喺七個 alias 通過 **40/40**；完整 native shell 通過 **388/388（388 passed、0 failed）**；catalog parity 覆蓋 346 條固定 route 加五組動態家族。c6 後嘅 `wordfreq` 重試仍然係 `capture-blocked`：repo 本機 LowLevel checkout 喺度但目前 Codex session 冇可呼叫工具，而 repository driver 拒絕咗空白／近乎單色 fallback。冇保留或替代 PNG。只有詞頻統計有呢次最新重試；文字統計同字串相似度保留歷史 `capture-blocked` 紀錄。

## Buttons And Controls · 按鈕與控制項

| Button · 按鈕 | Type · 類型 | XAML name · 名稱 | Handler · 處理函式 |
|---|---|---|---|
| None detected from XAML · XAML 未偵測到 |  |  |  |