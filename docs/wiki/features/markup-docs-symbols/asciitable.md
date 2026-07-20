# ASCII Table · ASCII 表

**EN —** Feature reference generated from the WinForge module registry, navigation map, and page XAML.
**粵語 —** 呢份功能參考由 WinForge 模組登記、導覽地圖同頁面 XAML 生成。

| Field · 欄位 | Value · 值 |
|---|---|
| Tag · 標籤 | <code>module.asciitable</code> |
| Deep-link alias · 深層連結別名 | <code>asciitable</code> |
| Category · 分類 | Markup, Docs & Symbols · 標記文件與符號 |
| Page class · 頁面類別 | <code>AsciiTableModule</code> |
| Page XAML · 頁面 XAML | <code>Pages/AsciiTableModule.xaml</code> |
| Button docs · 按鈕文件 | 0 |

## What It Covers · 功能範圍

**EN —** ASCII Table is registered in WinForge search and navigation with these keywords: <code>ascii table character codes control codes hex octal binary latin-1 charset reference 字元 字元碼 控制碼 十六進 八進 二進 參考表</code>.

**粵語 —** ASCII 表 已登記喺 WinForge 搜尋同導覽，關鍵字包括：<code>ascii table character codes control codes hex octal binary latin-1 charset reference 字元 字元碼 控制碼 十六進 八進 二進 參考表</code>。

## Native C++/WinRT Status · 原生 C++/WinRT 狀態

**EN —** `ascii`, `asciitable`, and `module.asciitable` now open a genuine native ASCII Table backed by the dependency-free standard-C++ `AsciiTable` core. Its local reference defaults to inclusive code points 0–127 and extends to 255 only after the explicit Latin-1 choice. It preserves C0, space, DEL, C1, and NBSP distinctions; decimal, hexadecimal, octal, and eight-bit binary columns; invariant search across displayed fields; virtualized rows; language-state retention; fresh-route reset; and explicit-only raw-character clipboard Copy.

**粵語 —** `ascii`、`asciitable` 同 `module.asciitable` 而家會開真正原生 ASCII 表，由唔靠依賴嘅標準 C++ `AsciiTable` core 支援。佢本機參考預設包括 0–127，只有明確揀 Latin-1 先擴到 255；保留 C0、空格、DEL、C1 同 NBSP 分別、十進／十六進／八進／八位元二進欄、搜尋全部顯示欄位、虛擬化列、轉語言保留狀態、新 route 重設，同埋只限明確 Copy 原始字元去剪貼簿。

**Controlled integration evidence · 受控整合證據：** renderer accounting is **37/346 fixed routes** (`37 in-progress / 309 not-started`, plus five dynamic families). Fresh Debug and Release x64 native solution builds exit 0 with 0 errors; both core suites pass **878/878**, including ASCII Table **21/21**; focused `-AsciiTableRoutesOnly -AllowClipboardMutation` UI Automation passes **16/16** across all aliases and language states; catalog parity covers 346 fixed routes plus five dynamic families, 319 registry records, and 346 ledger rows; and the native installer contract passes. A broader aggregate invocation was stopped after the observed pre-existing `wordfreq` launch stalled, so it is not claimed as a passing full-shell result. LowLevel Computer Use MCP is not callable in this session. The required driver found `CopyFromScreen` unavailable and rejected a blank/near-uniform `PrintWindow` frame; no PNG was retained and no root WinForge process remained. Visual evidence is honestly `capture-blocked`. · Renderer 而家係 **37/346** 條固定 route（`37 in-progress / 309 not-started`，另加五組動態家族）。最新 Debug／Release x64 solution build 各 0 errors；兩個 core 各 **878/878**，包括 ASCII Table **21/21**；`-AsciiTableRoutesOnly -AllowClipboardMutation` 專項 UI Automation 喺全部 alias 同語言狀態通過 **16/16**；catalog parity 覆蓋 346 固定 route 加五組動態家族、319 registry 同 346 ledger；native installer contract 亦通過。較廣 aggregate 喺觀察到既有 `wordfreq` launch 卡住後已停止，所以唔會聲稱 full-shell pass。今個 session 冇可呼叫 LowLevel MCP；必需 driver 發現 `CopyFromScreen` 唔可用，並拒絕空白／近乎單色 `PrintWindow` 畫面，冇保留 PNG 同冇殘留 root WinForge process；visual 如實係 `capture-blocked`。

## Buttons And Controls · 按鈕與控制項

| Button · 按鈕 | Type · 類型 | XAML name · 名稱 | Handler · 處理函式 |
|---|---|---|---|
| None detected from XAML · XAML 未偵測到 |  |  |  |