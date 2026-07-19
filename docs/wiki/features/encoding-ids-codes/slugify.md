# Slugify · 網址別名

**EN —** Feature reference generated from the WinForge module registry, navigation map, and page XAML.
**粵語 —** 呢份功能參考由 WinForge 模組登記、導覽地圖同頁面 XAML 生成。

| Field · 欄位 | Value · 值 |
|---|---|
| Tag · 標籤 | <code>module.slugify</code> |
| Deep-link alias · 深層連結別名 | <code>slugify</code> |
| Category · 分類 | Encoding, IDs & Codes · 編碼識別碼與條碼 |
| Page class · 頁面類別 | <code>SlugifyModule</code> |
| Page XAML · 頁面 XAML | <code>Pages/SlugifyModule.xaml</code> |
| Button docs · 按鈕文件 | 1 |

## What It Covers · 功能範圍

**EN —** Slugify is registered in WinForge search and navigation with these keywords: <code>slug slugify url permalink kebab hyphen diacritics transliterate case seo 網址 別名 短網址 連字號 去重音 大小寫</code>.

**粵語 —** 網址別名 已登記喺 WinForge 搜尋同導覽，關鍵字包括：<code>slug slugify url permalink kebab hyphen diacritics transliterate case seo 網址 別名 短網址 連字號 去重音 大小寫</code>。

## Native C++/WinRT Status · 原生 C++/WinRT 狀態

**EN —** `slug`, `slugify`, and `module.slugify` now open a dedicated native Slugify page backed by the standard-C++ `Slugify` core. It preserves managed CRLF/LF/CR block behavior, ignores blank input lines, strips combining diacritics when enabled, uses ASCII letters and digits by default, admits Unicode letters and decimal digits only when explicitly enabled, and applies the selected hyphen/underscore/dot separator, case, and UTF-16 max-length trimming. The page keeps the first nonblank-line before/after preview live, resets managed defaults on a fresh route entry, preserves local state across a language rerender, and writes the clipboard only after explicit Copy.

**粵語 —** `slug`、`slugify` 同 `module.slugify` 而家會開專用原生網址別名頁，由標準 C++ `Slugify` core 支援。佢保留 managed 版 CRLF／LF／CR 分行行為、略過空白輸入行、可選去除 combining 重音、預設只保留 ASCII 字母同數字、只有明確開啟先保留 Unicode 字母同十進位數字，並會套用連字號／底線／點分隔符、大小寫同 UTF-16 最長長度修剪。第一個非空白行嘅前後預覽會即時更新；新 route entry 會重設 managed 預設，語言重繪會保留本機狀態，而且只會喺明確 Copy 後先寫剪貼簿。

**Current local evidence · 目前本機證據：** renderer accounting is **31/346 fixed routes**, leaving **315 fixed routes** plus five dynamic families. Native Debug and Release x64 solution builds each exit 0 with 0 errors; Debug and Release core each pass **777/777**, including **18/18** Slugify contracts; catalog parity covers all 346 fixed routes plus five dynamic families; and focused `-SlugifyRoutesOnly` UI Automation passes **12/12** across all three aliases, including explicit Clipboard Copy. LowLevel Computer Use MCP is not callable in this Codex session. The required repository-driver attempt found `CopyFromScreen` unavailable and rejected a blank or near-uniform `PrintWindow` fallback; no PNG was created, no worktree WinForge process remained, and no canonical screenshot changed. The route is therefore honestly `capture-blocked` and `in-progress`. · Renderer 計 **31/346** 條固定 route，仲有 **315** 條固定 route 同五組動態家族。原生 Debug／Release x64 solution build 各自 exit 0、0 errors；兩個 core 各 **777/777**，包括 Slugify **18/18**；catalog parity 覆蓋 346 條固定 route 加五組動態家族；而且 `-SlugifyRoutesOnly` 專項 UI Automation 喺三個 alias（包括明確 Clipboard Copy）通過 **12/12**。今次 Codex session 冇可呼叫 LowLevel MCP；必需 repository-driver 嘗試發現 `CopyFromScreen` 唔可用，並拒絕空白／近乎單色 `PrintWindow` fallback。冇建立 PNG、冇 worktree WinForge process、冇改 canonical 截圖，所以呢條 route 如實係 `capture-blocked` 同 `in-progress`。

## Buttons And Controls · 按鈕與控制項

| Button · 按鈕 | Type · 類型 | XAML name · 名稱 | Handler · 處理函式 |
|---|---|---|---|
| [CopyBtn](../../buttons/encoding-ids-codes/slugify/001-copybtn.md) | `Button` | `CopyBtn` | `Copy_Click` |