# HTML Table Convert · HTML 表格轉換

**EN —** Feature reference generated from the WinForge module registry, navigation map, and page XAML.
**粵語 —** 呢份功能參考由 WinForge 模組登記、導覽地圖同頁面 XAML 生成。

| Field · 欄位 | Value · 值 |
|---|---|
| Tag · 標籤 | <code>module.htmltable</code> |
| Deep-link alias · 深層連結別名 | <code>htmltable</code> |
| Category · 分類 | JSON & Data · JSON 與資料 |
| Page class · 頁面類別 | <code>HtmlTableModule</code> |
| Page XAML · 頁面 XAML | <code>Pages/HtmlTableModule.xaml</code> |
| Button docs · 按鈕文件 | 1 |

## What It Covers · 功能範圍

**EN —** HTML Table Convert is registered in WinForge search and navigation with these keywords: <code>html table csv tsv markdown convert thead tbody tr td parse generate 表格 轉換 逗號 標記 解析 產生</code>.

**粵語 —** HTML 表格轉換 已登記喺 WinForge 搜尋同導覽，關鍵字包括：<code>html table csv tsv markdown convert thead tbody tr td parse generate 表格 轉換 逗號 標記 解析 產生</code>。

## Startup Reliability · 啟動可靠性

**EN —** The self-contained runtime reproducibly rejected direct
`ToggleSwitch.IsOn` Boolean literals while constructing this page. Header-row
and cell-escaping defaults now initialize in managed code after
`InitializeComponent`, under the existing event-suppression guard. The visible
defaults and conversion behavior are unchanged.

**粵語 —** self-contained runtime 喺建立呢頁時可以重現地拒絕 direct
`ToggleSwitch.IsOn` Boolean literal。標題行同儲存格轉義預設而家喺
`InitializeComponent` 後、既有 event-suppression guard 入面用 managed code 初始化；
畫面預設同轉換行為不變。

**Verification · 驗證 —** `--page htmltable` first reproduced the parser crash;
a fresh self-contained launch passed after the repair. Screenshot capture is
still blocked in this desktop session because `CopyFromScreen` returns
`The handle is invalid`; this is launch evidence, not a visual-pass claim.

## Buttons And Controls · 按鈕與控制項

| Button · 按鈕 | Type · 類型 | XAML name · 名稱 | Handler · 處理函式 |
|---|---|---|---|
| [CopyButton](../../buttons/json-data-json/htmltable/001-copybutton.md) | `Button` | `CopyButton` | `Copy_Click` |
