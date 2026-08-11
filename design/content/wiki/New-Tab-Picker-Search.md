# New-tab picker search · 新分頁選擇器搜尋

The Ctrl+T and Add-tab picker uses WinForge's shared `SearchPatternBox`. Plain text is the default; the adjacent regex action opens the full bounded .NET builder with raw pattern editing, guided pieces, flags, syntax feedback, local preview, captures, and copy. · Ctrl+T 同 Add-tab picker 用 WinForge 共用 `SearchPatternBox`。純文字係預設；旁邊嘅 regex action 會開完整、有界 .NET builder，支援原始 pattern、引導組件、旗標、語法提示、本機預覽、capture 同複製。

The query searches the bilingual title, subtitle, key, and category labels. Category filtering composes with search. Empty queries preserve the normal frequent/suggested/category sections; Enter opens the first result, and each result is a real keyboard-focusable button. Invalid patterns show validation and no results rather than silently changing the query. · 搜尋會查雙語標題、副標題、key 同分類標籤；分類篩選會同搜尋一齊生效。空 query 保留正常常用／建議／分類分段；Enter 開第一個結果，每個結果都係真正可用鍵盤聚焦嘅 button。無效 pattern 會顯示驗證提示同零結果，唔會靜靜雞改走 query。

Patterns and samples stay in the current picker session. Local .NET matching uses the shared bounded timeout and size limits; no search text or sample is written to session exports, local history, telemetry, or network requests. · Pattern 同 sample 只留喺今次 picker session；本機 .NET matching 用共用有界 timeout 同大小限制；搜尋文字或 sample 唔會寫入 session export、本機 history、telemetry 或 network request。

Verification: `MainWindow.xaml.cs` wires `SearchPatternBox`, `PatternChanged`, `SearchPatternService.Spec`, and one compiled matcher into the live picker; the managed release contract guards those seams, while the shared regex suite covers plain text, flags, invalid syntax, Unicode, captures, zero-width matches, timeout, and limits. · 驗證：`MainWindow.xaml.cs` 將 `SearchPatternBox`、`PatternChanged`、`SearchPatternService.Spec` 同一個 compiled matcher 接入真正 picker；managed release contract 守住呢啲 seam，而共用 regex suite 覆蓋純文字、旗標、無效語法、Unicode、capture、零寬配對、timeout 同限制。

See also: [Regex-Builder](Regex-Builder), [Pinned-Tabs](Pinned-Tabs), and [Offline-Documentation](Offline-Documentation). · 另見：[Regex-Builder](Regex-Builder)、[Pinned-Tabs](Pinned-Tabs) 同 [Offline-Documentation](Offline-Documentation)。
