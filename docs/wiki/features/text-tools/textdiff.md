# Text Diff · 文字差異比對

**EN —** Feature reference generated from the WinForge module registry, navigation map, and page XAML.
**粵語 —** 呢份功能參考由 WinForge 模組登記、導覽地圖同頁面 XAML 生成。

| Field · 欄位 | Value · 值 |
|---|---|
| Tag · 標籤 | <code>module.textdiff</code> |
| Deep-link alias · 深層連結別名 | <code>textdiff</code> |
| Category · 分類 | Text Tools · 文字工具 |
| Page class · 頁面類別 | <code>TextDiffModule</code> |
| Page XAML · 頁面 XAML | <code>Pages/TextDiffModule.xaml</code> |
| Button docs · 按鈕文件 | 1 |

## What It Covers · 功能範圍

**EN —** Text Diff is registered in WinForge search and navigation with these keywords: <code>diff compare text lines lcs unified merge changes 文字 差異 比較 對比 逐行 合併</code>.

**粵語 —** 文字差異比對 已登記喺 WinForge 搜尋同導覽，關鍵字包括：<code>diff compare text lines lcs unified merge changes 文字 差異 比較 對比 逐行 合併</code>。

## Native C++/WinRT Status · 原生 C++/WinRT 狀態

**EN —** `textdiff` and `module.textdiff` now open a dedicated genuine native page backed by a standard-C++ line-diff core. It normalizes line endings, offers explicit whitespace and invariant-Unicode case options, reports added/removed/unchanged counts, and produces a local unified diff. Its **27/27** focused contracts include the exact 6,000,000-cell LCS guard and deterministic over-budget fallback. The broader Debug and Release core suites each pass **560/560**; Design Tools passes **94/94**, the three-utility focused UI Automation shell passes **39/39**, and the full owned shell remains **300/300**. Renderer accounting is **21/346 fixed routes**, with **325 fixed routes** pending; the inventory additionally includes five dynamic route families. It never mutates Windows; explicit Copy of the unified result is its only side effect.

**粵語 —** `textdiff` 同 `module.textdiff` 而家會開專用真正原生頁面，由標準 C++ 逐行 diff core 支援。佢會統一換行、提供明確忽略空白同 invariant-Unicode 大小寫選項、報告新增／刪除／不變數量，並喺本機產生 unified diff。**27/27** 個專項合約包括準確 6,000,000-cell LCS 上限同超出上限時嘅確定性 fallback。Debug 同 Release 較廣 core suite 各自 **560/560**；Design Tools 通過 **94/94**，三項工具專項 UI Automation shell 通過 **39/39**，完整自有 shell 亦保持 **300/300**。Renderer 計數係 **21/346 條固定 route**，另外 **325 條固定 route** 待完成；清單亦包括五組動態 route 家族。佢唔會改 Windows；唯一副作用係操作員明確 Copy unified 結果。

**Visual evidence · 視覺證據：** LowLevel MCP and the repository driver launched this native page, but both produced a uniformly white client because WinUI composition is unavailable in this desktop session. The blank images were discarded; no canonical image was replaced, so this route is honestly `capture-blocked`. · LowLevel MCP 同 repository driver 都開過呢個原生頁，但呢個 desktop session 冇 WinUI composition，兩邊 client 都係一致白色。空白圖已丟棄，冇替換 canonical 圖；呢條 route 如實係 `capture-blocked`。

## Buttons And Controls · 按鈕與控制項

| Button · 按鈕 | Type · 類型 | XAML name · 名稱 | Handler · 處理函式 |
|---|---|---|---|
| [CopyButton](../../buttons/text-tools/textdiff/001-copybutton.md) | `Button` | `CopyButton` | `Copy_Click` |