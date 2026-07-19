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

**Evidence · 證據：** renderer accounting remains **27/346 fixed routes**, leaving **319 fixed routes** plus five dynamic families; Debug and Release core each pass **717/717**, including Text Analysis **87/87**; focused text-analysis UI Automation passes **40/40** across seven aliases; the full native shell passes **388/388 (388 passed, 0 failed)**; and catalog parity covers 346 fixed routes plus five families. The post-c6 `wordfreq` retry remains `capture-blocked`: the repo-local LowLevel checkout was present but not callable in this Codex session, and the repository driver rejected its blank/near-uniform fallback. No PNG was retained or substituted. Only Word Frequency had this fresh retry; Text Statistics and String Compare retain their historical `capture-blocked` records. · Renderer 計數維持 **27/346** 條固定 route，仲有 **319** 條固定 route 同五組動態家族；Debug 同 Release core 各自通過 **717/717**，包括 Text Analysis **87/87**；專項文字分析 UI Automation 喺七個 alias 通過 **40/40**；完整 native shell 通過 **388/388（388 passed、0 failed）**；catalog parity 覆蓋 346 條固定 route 加五組動態家族。c6 後嘅 `wordfreq` 重試仍然係 `capture-blocked`：repo 本機 LowLevel checkout 喺度但目前 Codex session 冇可呼叫工具，而 repository driver 拒絕咗空白／近乎單色 fallback。冇保留或替代 PNG。只有詞頻統計有呢次最新重試；文字統計同字串相似度保留歷史 `capture-blocked` 紀錄。

## Buttons And Controls · 按鈕與控制項

| Button · 按鈕 | Type · 類型 | XAML name · 名稱 | Handler · 處理函式 |
|---|---|---|---|
| [CopyButton](../../buttons/text-tools/stringcompare/001-copybutton.md) | `Button` | `CopyButton` | `Copy_Click` |