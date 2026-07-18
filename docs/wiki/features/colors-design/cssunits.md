# CSS Unit Converter · CSS 單位換算

**EN —** Feature reference generated from the WinForge module registry, navigation map, and page XAML.
**粵語 —** 呢份功能參考由 WinForge 模組登記、導覽地圖同頁面 XAML 生成。

| Field · 欄位 | Value · 值 |
|---|---|
| Tag · 標籤 | <code>module.cssunits</code> |
| Deep-link alias · 深層連結別名 | <code>cssunits</code> |
| Category · 分類 | Colors & Design · 色彩與設計 |
| Page class · 頁面類別 | <code>CssUnitsModule</code> |
| Page XAML · 頁面 XAML | <code>Pages/CssUnitsModule.xaml</code> |
| Button docs · 按鈕文件 | 0 |

## What It Covers · 功能範圍

**EN —** CSS Unit Converter is registered in WinForge search and navigation with these keywords: <code>css units px em rem pt vw vh percent convert web design root font size 單位 換算 網頁 設計 字級</code>.

**粵語 —** CSS 單位換算 已登記喺 WinForge 搜尋同導覽，關鍵字包括：<code>css units px em rem pt vw vh percent convert web design root font size 單位 換算 網頁 設計 字級</code>。

## Native C++/WinRT Status · 原生 C++/WinRT 狀態

**EN —** `cssunits` and `module.cssunits` now open a dedicated genuine native converter backed by standard-C++ CSS unit logic. It converts among `px`, `em`, `rem`, `pt`, `pc`, `%`, `vw`, `vh`, `cm`, `mm`, and `in` using explicit root-font, element-font, viewport, and container contexts, and reports the other ten units in managed order. Design Tools passes **94/94** focused contracts; Debug and Release each pass **560/560** overall, the three-utility focused UI Automation shell passes **39/39**, and the full owned shell remains **300/300**. Renderer accounting is **21/346 fixed routes**, with **325 fixed routes** pending; the inventory additionally includes five dynamic route families. Conversion is local and never mutates the OS; only an explicit result Copy writes the clipboard.

**粵語 —** `cssunits` 同 `module.cssunits` 而家會開專用真正原生換算器，由標準 C++ CSS 單位邏輯支援。佢會用明確 root 字級、元素字級、viewport 同 container context，喺 `px`、`em`、`rem`、`pt`、`pc`、`%`、`vw`、`vh`、`cm`、`mm` 同 `in` 之間換算，並按受控版次序顯示另外十個單位。Design Tools 通過 **94/94** 個專項合約；Debug 同 Release 整體各自 **560/560**，三項工具專項 UI Automation shell 通過 **39/39**，完整自有 shell 亦保持 **300/300**。Renderer 計數係 **21/346 條固定 route**，另外 **325 條固定 route** 待完成；清單亦包括五組動態 route 家族。換算只喺本機做、唔會改 OS；只有明確 Copy 某項結果先會寫剪貼簿。

**Visual evidence · 視覺證據：** LowLevel MCP and the repository driver launched this native page, but both produced a uniformly white client because WinUI composition is unavailable in this desktop session. The blank images were discarded; no canonical image was replaced, so this route is honestly `capture-blocked`. · LowLevel MCP 同 repository driver 都開過呢個原生頁，但呢個 desktop session 冇 WinUI composition，兩邊 client 都係一致白色。空白圖已丟棄，冇替換 canonical 圖；呢條 route 如實係 `capture-blocked`。

## Buttons And Controls · 按鈕與控制項

| Button · 按鈕 | Type · 類型 | XAML name · 名稱 | Handler · 處理函式 |
|---|---|---|---|
| None detected from XAML · XAML 未偵測到 |  |  |  |