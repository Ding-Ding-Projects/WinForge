# Text Wrap · 文字換行

**EN —** Feature reference generated from the WinForge module registry, navigation map, and page XAML.
**粵語 —** 呢份功能參考由 WinForge 模組登記、導覽地圖同頁面 XAML 生成。

| Field · 欄位 | Value · 值 |
|---|---|
| Tag · 標籤 | <code>module.textwrap</code> |
| Deep-link alias · 深層連結別名 | <code>textwrap</code> |
| Category · 分類 | Text Tools · 文字工具 |
| Page class · 頁面類別 | <code>TextWrapModule</code> |
| Page XAML · 頁面 XAML | <code>Pages/TextWrapModule.xaml</code> |
| Button docs · 按鈕文件 | 6 |

## What It Covers · 功能範圍

**EN —** Text Wrap is registered in WinForge search and navigation with these keywords: <code>text wrap reflow rewrap unwrap column width word boundary hanging indent prefix comment commit message readme 文字 換行 重排 拉直 縮排 前綴 闊度 段落 註解</code>.

**粵語 —** 文字換行 已登記喺 WinForge 搜尋同導覽，關鍵字包括：<code>text wrap reflow rewrap unwrap column width word boundary hanging indent prefix comment commit message readme 文字 換行 重排 拉直 縮排 前綴 闊度 段落 註解</code>。

## Native C++/WinRT Status · 原生 C++/WinRT 狀態

**Evidence-count note · 證據計數備註：** The managed build's 161 warnings are recorded only as historical, non-contract output; the durable gate is exit 0 with 0 errors. · Managed build 嘅 161 個 warning 只記錄做歷史、非合約輸出；長期 gate 係 exit 0 同 0 errors。

**EN —** Native source now contains a dedicated Text Wrap renderer backed by the shared standard-C++ Line Processing core. It preserves the managed 72-column default and 1–2000 range, optional long-word chunking, paragraph-aware hard wrap, unwrap and two-pass reflow, output-chaining prefix and hanging-indent actions, UTF-16 readout metrics, and explicit-only clipboard Copy. Native Debug and Release builds each exit 0; both core executables pass **630/630**, including **70/70** Line Processing contracts. Renderer accounting is **24/346 fixed routes**, with **322 fixed routes** pending plus five dynamic route families.

**粵語 —** 原生 source 而家有專用 Text Wrap renderer，由共用標準 C++ Line Processing core 支援。佢保留 managed 版 72 字元預設同 1–2000 範圍、可選長字拆段、按段落硬換行、拉直同兩階段重排、由現有輸出繼續做前綴／懸掛縮排、UTF-16 readout 統計，同只限明確 Copy 嘅剪貼簿操作。原生 Debug／Release build 各 exit 0，兩個 core executable 各 **630/630**，包括 Line Processing **70/70**。Renderer 計數係 **24/346 條固定 route**，仲有 **322 條固定 route** 同五組動態 route 家族待完成。

**Focused validation green; visual capture blocked · 專項驗證通過；視覺擷取受阻：** Catalog parity passes all 346 fixed routes plus five dynamic families, focused `-LineRoutesOnly` UI Automation passes **42/42** across all seven registered aliases, the full owned native shell smoke passes **348/348** with exit 0, and the managed Debug x64 solution build exits 0 with 0 errors (its 161 warnings are historical, non-contract output). LowLevel Computer Use MCP 1.28.1 launched `--page lines`, `--page textsort`, and `--page textwrap` on private desktops; the first pass resolved **852×880** windows at PIDs 29740, 19176, and 25196, and inspected `rendered_ok` `PrintWindow` frames were composition-white/invalid. A compositor retry started PIDs 21696, 23644, and 22048, but `show_headless_desktop` failed for all three with Win32 error 5, Access denied, before monitor capture. All exact processes and desktops were cleaned, including Text Sort's brief 0×0 ghost. Required run-winforge attempts used `-Native -WaitMs 16000`; `CopyFromScreen` was unavailable, blank/near-uniform fallbacks were rejected, and all three exited 1. The artifact directory is empty and no canonical image was replaced, so visual evidence is conclusively `capture-blocked`. Hosted release-per-push and Git integration/remote proof remain pending. · Catalog parity、全部七個 alias 嘅專項 UIA **42/42**、完整自有原生 shell smoke **348/348**，同 managed 0-error build 已通過；161 個 warning 只係歷史、非合約輸出。LowLevel MCP 準確開過三個 canonical command；第一輪解析 **852×880** window，PID 29740／19176／25196，`rendered_ok` 圖檢查後係 composition-white 無效畫面。Compositor retry 開過 PID 21696／23644／22048，但 monitor capture 前已因 Win32 error 5 Access denied 失敗；全部指定 process 同 desktop 已清理，包括 Text Sort 短暫嘅 0×0 ghost。Run-winforge 用 `-Native -WaitMs 16000` 試過三頁，因 `CopyFromScreen` 唔可用同空白 fallback 被拒而全部 exit 1。Artifact directory 已清空，冇替換 canonical 圖，所以 visual 最終係 `capture-blocked`；release 同 Git 證明仍待完成。

## Buttons And Controls · 按鈕與控制項

| Button · 按鈕 | Type · 類型 | XAML name · 名稱 | Handler · 處理函式 |
|---|---|---|---|
| [WrapBtn](../../buttons/text-tools/textwrap/001-wrapbtn.md) | `Button` | `WrapBtn` | `Wrap_Click` |
| [UnwrapBtn](../../buttons/text-tools/textwrap/002-unwrapbtn.md) | `Button` | `UnwrapBtn` | `Unwrap_Click` |
| [ReflowBtn](../../buttons/text-tools/textwrap/003-reflowbtn.md) | `Button` | `ReflowBtn` | `Reflow_Click` |
| [PrefixBtn](../../buttons/text-tools/textwrap/004-prefixbtn.md) | `Button` | `PrefixBtn` | `Prefix_Click` |
| [IndentBtn](../../buttons/text-tools/textwrap/005-indentbtn.md) | `Button` | `IndentBtn` | `Indent_Click` |
| [CopyBtn](../../buttons/text-tools/textwrap/006-copybtn.md) | `Button` | `CopyBtn` | `Copy_Click` |