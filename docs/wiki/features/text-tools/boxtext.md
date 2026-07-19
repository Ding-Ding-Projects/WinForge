# Box & Banner Text · 文字方框 / 橫幅

**EN —** Feature reference generated from the WinForge module registry, navigation map, and page XAML.
**粵語 —** 呢份功能參考由 WinForge 模組登記、導覽地圖同頁面 XAML 生成。

| Field · 欄位 | Value · 值 |
|---|---|
| Tag · 標籤 | <code>module.boxtext</code> |
| Deep-link alias · 深層連結別名 | <code>boxtext</code> |
| Category · 分類 | Text Tools · 文字工具 |
| Page class · 頁面類別 | <code>BoxTextModule</code> |
| Page XAML · 頁面 XAML | <code>Pages/BoxTextModule.xaml</code> |
| Button docs · 按鈕文件 | 1 |

## What It Covers · 功能範圍

**EN —** Box & Banner Text is registered in WinForge search and navigation with these keywords: <code>box banner ascii border frame comment block banner text wrap 文字方框 橫幅 邊框 框框 註解 ASCII 標題</code>.

**粵語 —** 文字方框 / 橫幅 已登記喺 WinForge 搜尋同導覽，關鍵字包括：<code>box banner ascii border frame comment block banner text wrap 文字方框 橫幅 邊框 框框 註解 ASCII 標題</code>。

## Native C++/WinRT Status · 原生 C++/WinRT 狀態

**EN —** `boxtext` and `module.boxtext` now open a dedicated native Box & Banner Text page backed by the standard-C++ `ReferenceText` core. It preserves eight managed border/comment styles, left/center/right alignment, bounded padding, optional titles, multiline and tab-aware layout, Unicode display width, live output, and explicit-only clipboard Copy.

**粵語 —** `boxtext` 同 `module.boxtext` 而家會開專用原生文字方框／橫幅頁，由標準 C++ `ReferenceText` core 支援。佢保留 managed 版八種邊框／註解風格、左／中／右對齊、有界 padding、可選標題、多行同 tab-aware 排版、Unicode 顯示闊度、即時輸出，同只限明確 Copy 嘅剪貼簿操作。

**Current local evidence · 目前本機證據：** renderer accounting is **30/346 fixed routes**, leaving **316 fixed routes** plus five dynamic families; native Debug and Release x64 solution builds each exit 0 with 0 errors; Debug and Release core executables each pass **759/759**; catalog parity passes all 346 fixed routes plus five dynamic families; focused reference-text UI Automation passes **29/29** across all eight aliases; and the fresh full native shell smoke passes **417/417 (417 passed, 0 failed)**. LowLevel Computer Use MCP is not callable in this Codex session. A fresh post-integration `htmlentities` repository-driver retry found `CopyFromScreen` unavailable and rejected a blank/near-uniform `PrintWindow` client. No PNG was created or retained, no WinForge process remained, and no canonical screenshot changed, so visual evidence is honestly `capture-blocked`. · Renderer 計 **30/346**，仲有 **316** 條固定 route 同五組動態家族；Debug／Release build 各 0 errors、兩個 core 各 **759/759**、catalog parity 同八個 alias UIA **29/29** 已通過；最新完整 native shell smoke 通過 **417/417（417 passed、0 failed）**。今次 session 冇 LowLevel MCP；整合後最新 `htmlentities` repository-driver 重試發現 `CopyFromScreen` 唔可用，並拒絕空白／近乎單色 `PrintWindow` client。冇建立或保留 PNG、冇殘留 WinForge process、冇改 canonical 截圖，所以 visual 如實係 `capture-blocked`。

## Buttons And Controls · 按鈕與控制項

| Button · 按鈕 | Type · 類型 | XAML name · 名稱 | Handler · 處理函式 |
|---|---|---|---|
| [CopyBtn](../../buttons/text-tools/boxtext/001-copybtn.md) | `Button` | `CopyBtn` | `Copy_Click` |