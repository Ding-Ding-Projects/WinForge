# Aspect Ratio · 長寬比計算

**EN —** Feature reference generated from the WinForge module registry, navigation map, and page XAML.
**粵語 —** 呢份功能參考由 WinForge 模組登記、導覽地圖同頁面 XAML 生成。

| Field · 欄位 | Value · 值 |
|---|---|
| Tag · 標籤 | <code>module.aspectratio</code> |
| Deep-link alias · 深層連結別名 | <code>aspectratio</code> |
| Category · 分類 | Calculators & Numbers · 計算與數字 |
| Page class · 頁面類別 | <code>AspectRatioModule</code> |
| Page XAML · 頁面 XAML | <code>Pages/AspectRatioModule.xaml</code> |
| Button docs · 按鈕文件 | 1 |

## What It Covers · 功能範圍

**EN —** Aspect Ratio is registered in WinForge search and navigation with these keywords: <code>aspect ratio resolution 16:9 scale gcd megapixels dimensions widescreen 長寬比 解析度 比例 縮放 像素 闊高 畫面比</code>.

**粵語 —** 長寬比計算 已登記喺 WinForge 搜尋同導覽，關鍵字包括：<code>aspect ratio resolution 16:9 scale gcd megapixels dimensions widescreen 長寬比 解析度 比例 縮放 像素 闊高 畫面比</code>。

## Native C++/WinRT Status · 原生 C++/WinRT 狀態

**EN —** `aspect`, `aspectratio`, and `module.aspectratio` now open a dedicated genuine native calculator backed by standard-C++ ratio logic. It simplifies dimensions, reports decimal ratio and megapixels, supports the nine managed presets, and scales either target width or target height without changing the source dimensions. Design Tools passes **94/94** focused contracts; Debug and Release each pass **560/560** overall, the three-utility focused UI Automation shell passes **39/39**, and the full owned shell remains **300/300**. A deterministic one-million-finite-double differential against .NET 11 produced zero display-format mismatches. Renderer accounting is **21/346 fixed routes**, with **325 fixed routes** pending; the inventory additionally includes five dynamic route families. It performs no OS mutation, and only an explicit Copy writes the clipboard.

**粵語 —** `aspect`、`aspectratio` 同 `module.aspectratio` 而家會開專用真正原生計算機，由標準 C++ 比例邏輯支援。佢會化簡尺寸、顯示小數比例同百萬像素、支援受控版九個 preset，亦可以按目標闊度或高度縮放而唔改原始尺寸。Design Tools 通過 **94/94** 個專項合約；Debug 同 Release 整體各自 **560/560**，三項工具專項 UI Automation shell 通過 **39/39**，完整自有 shell 亦保持 **300/300**。一百萬個有限 double 對 .NET 11 嘅確定性比對係零個顯示格式差異。Renderer 計數係 **21/346 條固定 route**，另外 **325 條固定 route** 待完成；清單亦包括五組動態 route 家族。佢唔會改 OS，只有明確 Copy 先會寫剪貼簿。

**Visual evidence · 視覺證據：** LowLevel MCP and the repository driver launched this native page, but both produced a uniformly white client because WinUI composition is unavailable in this desktop session. The blank images were discarded; no canonical image was replaced, so this route is honestly `capture-blocked`. · LowLevel MCP 同 repository driver 都開過呢個原生頁，但呢個 desktop session 冇 WinUI composition，兩邊 client 都係一致白色。空白圖已丟棄，冇替換 canonical 圖；呢條 route 如實係 `capture-blocked`。

## Buttons And Controls · 按鈕與控制項

| Button · 按鈕 | Type · 類型 | XAML name · 名稱 | Handler · 處理函式 |
|---|---|---|---|
| [CopyBtn](../../buttons/calculators-numbers/aspectratio/001-copybtn.md) | `Button` | `CopyBtn` | `Copy_Click` |