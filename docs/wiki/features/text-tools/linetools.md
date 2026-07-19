# Line Tools · 行工具

**EN —** Feature reference generated from the WinForge module registry, navigation map, and page XAML.
**粵語 —** 呢份功能參考由 WinForge 模組登記、導覽地圖同頁面 XAML 生成。

| Field · 欄位 | Value · 值 |
|---|---|
| Tag · 標籤 | <code>module.linetools</code> |
| Deep-link alias · 深層連結別名 | <code>linetools</code> |
| Category · 分類 | Text Tools · 文字工具 |
| Page class · 頁面類別 | <code>LineToolsModule</code> |
| Page XAML · 頁面 XAML | <code>Pages/LineToolsModule.xaml</code> |
| Button docs · 按鈕文件 | 16 |

## What It Covers · 功能範圍

**EN —** Line Tools is registered in WinForge search and navigation with these keywords: <code>line tools text lines number prefix suffix quotes join split reverse sort dedupe deduplicate trim shuffle 行工具 文字 行 編號 前綴 後綴 引號 合併 拆分 反轉 排序 去重 修剪 打亂</code>.

**粵語 —** 行工具 已登記喺 WinForge 搜尋同導覽，關鍵字包括：<code>line tools text lines number prefix suffix quotes join split reverse sort dedupe deduplicate trim shuffle 行工具 文字 行 編號 前綴 後綴 引號 合併 拆分 反轉 排序 去重 修剪 打亂</code>。

## Native C++/WinRT Status · 原生 C++/WinRT 狀態

**Evidence-count note · 證據計數備註：** The managed build's 161 warnings are recorded only as historical, non-contract output; the durable gate is exit 0 with 0 errors. · Managed build 嘅 161 個 warning 只記錄做歷史、非合約輸出；長期 gate 係 exit 0 同 0 errors。

**EN —** Native source now contains a dedicated Line Tools renderer backed by the shared standard-C++ Line Processing core. It preserves managed-compatible empty and trailing lines, UTF-16 character counts, ASCII word boundaries, both numbering formats, Unicode decimal-number removal, prefix/suffix/quotes, literal join/split, code-unit reversal, ordinal-ignore-case sorting and first-win deduplication, Unicode whitespace cleanup, and cryptographic Fisher–Yates shuffle. Native Debug and Release builds each exit 0; both core executables pass **630/630**, including **70/70** Line Processing contracts. Renderer accounting is **24/346 fixed routes**, with **322 fixed routes** pending plus five dynamic route families.

**粵語 —** 原生 source 而家有專用 Line Tools renderer，由共用標準 C++ Line Processing core 支援。佢保留 managed 相容嘅空行／尾空行、UTF-16 字元統計、ASCII 字詞邊界、兩種編號、Unicode 十進位數字編號移除、前綴／後綴／引號、literal 合併／拆分、code-unit 反轉、ordinal-ignore-case 排序、保留第一項去重、Unicode 空白清理，同密碼學 Fisher–Yates 打亂。原生 Debug／Release build 各 exit 0，兩個 core executable 各 **630/630**，包括 Line Processing **70/70**。Renderer 計數係 **24/346 條固定 route**，仲有 **322 條固定 route** 同五組動態 route 家族待完成。

**Focused validation green; visual capture blocked · 專項驗證通過；視覺擷取受阻：** Catalog parity passes all 346 fixed routes plus five dynamic families, focused `-LineRoutesOnly` UI Automation passes **42/42** across all seven registered aliases, the full owned native shell smoke passes **348/348** with exit 0, and the managed Debug x64 solution build exits 0 with 0 errors (its 161 warnings are historical, non-contract output). LowLevel Computer Use MCP 1.28.1 launched `--page lines`, `--page textsort`, and `--page textwrap` on private desktops; the first pass resolved **852×880** windows at PIDs 29740, 19176, and 25196, and inspected `rendered_ok` `PrintWindow` frames were composition-white/invalid. A compositor retry started PIDs 21696, 23644, and 22048, but `show_headless_desktop` failed for all three with Win32 error 5, Access denied, before monitor capture. All exact processes and desktops were cleaned, including Text Sort's brief 0×0 ghost. Required run-winforge attempts used `-Native -WaitMs 16000`; `CopyFromScreen` was unavailable, blank/near-uniform fallbacks were rejected, and all three exited 1. The artifact directory is empty and no canonical image was replaced, so visual evidence is conclusively `capture-blocked`. Hosted release-per-push and Git integration/remote proof remain pending. · Catalog parity、全部七個 alias 嘅專項 UIA **42/42**、完整自有原生 shell smoke **348/348**，同 managed 0-error build 已通過；161 個 warning 只係歷史、非合約輸出。LowLevel MCP 準確開過三個 canonical command；第一輪解析 **852×880** window，PID 29740／19176／25196，`rendered_ok` 圖檢查後係 composition-white 無效畫面。Compositor retry 開過 PID 21696／23644／22048，但 monitor capture 前已因 Win32 error 5 Access denied 失敗；全部指定 process 同 desktop 已清理，包括 Text Sort 短暫嘅 0×0 ghost。Run-winforge 用 `-Native -WaitMs 16000` 試過三頁，因 `CopyFromScreen` 唔可用同空白 fallback 被拒而全部 exit 1。Artifact directory 已清空，冇替換 canonical 圖，所以 visual 最終係 `capture-blocked`；release 同 Git 證明仍待完成。

## Buttons And Controls · 按鈕與控制項

| Button · 按鈕 | Type · 類型 | XAML name · 名稱 | Handler · 處理函式 |
|---|---|---|---|
| [BtnNumberDot](../../buttons/text-tools/linetools/001-btnnumberdot.md) | `Button` | `BtnNumberDot` | `NumberDot_Click` |
| [BtnNumberParen](../../buttons/text-tools/linetools/002-btnnumberparen.md) | `Button` | `BtnNumberParen` | `NumberParen_Click` |
| [BtnRemoveNums](../../buttons/text-tools/linetools/003-btnremovenums.md) | `Button` | `BtnRemoveNums` | `RemoveNums_Click` |
| [BtnQuotes](../../buttons/text-tools/linetools/004-btnquotes.md) | `Button` | `BtnQuotes` | `Quotes_Click` |
| [BtnPrefix](../../buttons/text-tools/linetools/005-btnprefix.md) | `Button` | `BtnPrefix` | `Prefix_Click` |
| [BtnSuffix](../../buttons/text-tools/linetools/006-btnsuffix.md) | `Button` | `BtnSuffix` | `Suffix_Click` |
| [BtnJoin](../../buttons/text-tools/linetools/007-btnjoin.md) | `Button` | `BtnJoin` | `Join_Click` |
| [BtnSplit](../../buttons/text-tools/linetools/008-btnsplit.md) | `Button` | `BtnSplit` | `Split_Click` |
| [BtnReverseChars](../../buttons/text-tools/linetools/009-btnreversechars.md) | `Button` | `BtnReverseChars` | `ReverseChars_Click` |
| [BtnSort](../../buttons/text-tools/linetools/010-btnsort.md) | `Button` | `BtnSort` | `Sort_Click` |
| [BtnReverseOrder](../../buttons/text-tools/linetools/011-btnreverseorder.md) | `Button` | `BtnReverseOrder` | `ReverseOrder_Click` |
| [BtnShuffle](../../buttons/text-tools/linetools/012-btnshuffle.md) | `Button` | `BtnShuffle` | `Shuffle_Click` |
| [BtnDedupe](../../buttons/text-tools/linetools/013-btndedupe.md) | `Button` | `BtnDedupe` | `Dedupe_Click` |
| [BtnRemoveEmpty](../../buttons/text-tools/linetools/014-btnremoveempty.md) | `Button` | `BtnRemoveEmpty` | `RemoveEmpty_Click` |
| [BtnTrim](../../buttons/text-tools/linetools/015-btntrim.md) | `Button` | `BtnTrim` | `Trim_Click` |
| [CopyBtn](../../buttons/text-tools/linetools/016-copybtn.md) | `Button` | `CopyBtn` | `Copy_Click` |