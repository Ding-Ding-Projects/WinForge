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

**EN —** `wordfreq` and `module.wordfreq` now open a dedicated native Word Frequency page backed by standard-C++ `TextAnalysis`. It preserves word, bigram, and Unicode-scalar modes; case, punctuation, number, and managed stop-word options; current-culture ordering; exact count, proportional bar, and percentage rows; and managed-compatible CSV escaping/output. Its `CurrentCultureIgnoreCase` comparer applies .NET Runtime width/kana tailoring, folds fullwidth case while keeping width and kana significant, keeps canonical-equivalent terms stably ordered, and shares an initialized collator without per-comparison serialization. The named total remains queryable to assistive technology without a per-edit polite live announcement; MIT attribution is in `THIRD-PARTY-NOTICES.txt`. Analysis is local, and only the explicit Copy action writes the clipboard.

**粵語 —** `wordfreq` 同 `module.wordfreq` 而家會開專用原生詞頻統計頁，由標準 C++ `TextAnalysis` 支援。佢保留單詞、bigram 同 Unicode scalar 模式、大小寫／標點／數字／managed 停用詞選項、依目前文化排序、準確數量／比例條形／百分比列，同 managed 相容 CSV escape／輸出。佢嘅 `CurrentCultureIgnoreCase` comparer 採用 .NET Runtime 闊度／假名 tailoring：fullwidth case 會折疊，但闊度同假名仍然有分別；canonical-equivalent 詞語保持穩定次序，而且共用已初始化嘅 collator，唔會每次比較都序列化。命名 total 仍可畀輔助科技查詢，但每次修改唔再發 polite live announcement；MIT 歸屬資料喺 `THIRD-PARTY-NOTICES.txt`。分析只喺本機做，只有明確 Copy 動作會寫剪貼簿。

**Evidence · 證據：** renderer accounting remains **27/346 fixed routes**, leaving **319 fixed routes** plus five dynamic families; Debug and Release core each pass **717/717**, including Text Analysis **87/87**; focused text-analysis UI Automation passes **40/40** across seven aliases; the full native shell passes **388/388 (388 passed, 0 failed)**; and catalog parity covers 346 fixed routes plus five families. The post-c6 `wordfreq` retry remains `capture-blocked`: the repo-local LowLevel checkout was present but not callable in this Codex session, and the repository driver rejected its blank/near-uniform fallback. No PNG was retained or substituted. Only Word Frequency had this fresh retry; Text Statistics and String Compare retain their historical `capture-blocked` records. · Renderer 計數維持 **27/346** 條固定 route，仲有 **319** 條固定 route 同五組動態家族；Debug 同 Release core 各自通過 **717/717**，包括 Text Analysis **87/87**；專項文字分析 UI Automation 喺七個 alias 通過 **40/40**；完整 native shell 通過 **388/388（388 passed、0 failed）**；catalog parity 覆蓋 346 條固定 route 加五組動態家族。c6 後嘅 `wordfreq` 重試仍然係 `capture-blocked`：repo 本機 LowLevel checkout 喺度但目前 Codex session 冇可呼叫工具，而 repository driver 拒絕咗空白／近乎單色 fallback。冇保留或替代 PNG。只有詞頻統計有呢次最新重試；文字統計同字串相似度保留歷史 `capture-blocked` 紀錄。

## Buttons And Controls · 按鈕與控制項

| Button · 按鈕 | Type · 類型 | XAML name · 名稱 | Handler · 處理函式 |
|---|---|---|---|
| [CopyBtn](../../buttons/text-tools/wordfreq/001-copybtn.md) | `Button` | `CopyBtn` | `Copy_Click` |