# HTML Entities · HTML 實體

**EN —** Feature reference generated from the WinForge module registry, navigation map, and page XAML.
**粵語 —** 呢份功能參考由 WinForge 模組登記、導覽地圖同頁面 XAML 生成。

| Field · 欄位 | Value · 值 |
|---|---|
| Tag · 標籤 | <code>module.htmlentities</code> |
| Deep-link alias · 深層連結別名 | <code>htmlentities</code> |
| Category · 分類 | Markup, Docs & Symbols · 標記文件與符號 |
| Page class · 頁面類別 | <code>HtmlEntitiesModule</code> |
| Page XAML · 頁面 XAML | <code>Pages/HtmlEntitiesModule.xaml</code> |
| Button docs · 按鈕文件 | 1 |

## What It Covers · 功能範圍

**EN —** HTML Entities is registered in WinForge search and navigation with these keywords: <code>html entities encode decode escape named numeric nbsp copy 實體 編碼 解碼 跳脫 具名 數字</code>.

**粵語 —** HTML 實體 已登記喺 WinForge 搜尋同導覽，關鍵字包括：<code>html entities encode decode escape named numeric nbsp copy 實體 編碼 解碼 跳脫 具名 數字</code>。

## Native C++/WinRT Status · 原生 C++/WinRT 狀態

**EN —** `entities`, `htmlentities`, and `module.htmlentities` now open a dedicated native HTML Entities page backed by the standard-C++ `ReferenceText` core. It preserves must-escape and optional non-ASCII encoding, named and decimal/hex numeric decoding, Unicode-scalar handling with invalid UTF-16 safety, managed-compatible scan behavior, a local 50-row bilingual entity reference, live counts, and explicit-only clipboard Copy.

**粵語 —** `entities`、`htmlentities` 同 `module.htmlentities` 而家會開專用原生 HTML 實體頁，由標準 C++ `ReferenceText` core 支援。佢保留必需 escape 同可選非 ASCII 編碼、具名／十進位／十六進位數字解碼、Unicode scalar 同無效 UTF-16 安全處理、managed 相容掃描行為、本機 50 項雙語實體參考、即時統計，同只限明確 Copy 嘅剪貼簿操作。

**Current local evidence · 目前本機證據：** renderer accounting is **30/346 fixed routes**, leaving **316 fixed routes** plus five dynamic families; native Debug and Release x64 solution builds each exit 0 with 0 errors; Debug and Release core executables each pass **759/759**; catalog parity passes all 346 fixed routes plus five dynamic families; focused reference-text UI Automation passes **29/29** across all eight aliases; and the fresh full native shell smoke passes **417/417 (417 passed, 0 failed)**. LowLevel Computer Use MCP is not callable in this Codex session. A fresh post-integration `htmlentities` repository-driver retry found `CopyFromScreen` unavailable and rejected a blank/near-uniform `PrintWindow` client. No PNG was created or retained, no WinForge process remained, and no canonical screenshot changed, so visual evidence is honestly `capture-blocked`. · Renderer 計 **30/346**，仲有 **316** 條固定 route 同五組動態家族；Debug／Release build 各 0 errors、兩個 core 各 **759/759**、catalog parity 同八個 alias UIA **29/29** 已通過；最新完整 native shell smoke 通過 **417/417（417 passed、0 failed）**。今次 session 冇 LowLevel MCP；整合後最新 `htmlentities` repository-driver 重試發現 `CopyFromScreen` 唔可用，並拒絕空白／近乎單色 `PrintWindow` client。冇建立或保留 PNG、冇殘留 WinForge process、冇改 canonical 截圖，所以 visual 如實係 `capture-blocked`。

## Buttons And Controls · 按鈕與控制項

| Button · 按鈕 | Type · 類型 | XAML name · 名稱 | Handler · 處理函式 |
|---|---|---|---|
| [CopyBtn](../../buttons/markup-docs-symbols/htmlentities/001-copybtn.md) | `Button` | `CopyBtn` | `Copy_Click` |