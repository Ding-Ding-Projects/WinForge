# New-tab picker search · 新分頁選擇器搜尋

## Behavior · 行為

The Ctrl+T and Add-tab picker uses the shared `SearchPatternBox` rather than a second plain text implementation. Plain text remains the default. The adjacent regex control opens the complete bounded .NET builder for literals, character classes, anchors, groups, alternation, quantifiers, raw patterns, supported flags, a local sample preview, syntax feedback, capture results, and copy. · Ctrl+T 同 Add-tab picker 而家用共用 `SearchPatternBox`，唔再另起一套純文字搜尋。純文字繼續係預設；旁邊嘅 regex 控制會開啟完整、有界嘅 .NET builder，支援字面文字、字元類、錨點、群組、二選一、量詞、原始 pattern、支援旗標、本機 sample 預覽、語法提示、capture 結果同複製。

The query searches each entry's title, bilingual subtitle, key, and category labels. Category filtering composes with the query. When the query is empty, the picker keeps its frequent, suggested, and category sections. When results exist, Enter opens the first result and each result remains a real keyboard-focusable button. · 搜尋會查每個項目嘅標題、雙語副標題、key 同分類標籤；分類篩選會同 query 一齊生效。query 空白時，picker 保留常用、建議同分類分段；有結果時按 Enter 會開第一項，而每個結果都係真正可用鍵盤聚焦嘅 button。

## Configuration and persistence · 設定同保存

The search query, regex mode, pattern, flags, and sample text are session-only. The selected category is a view filter for the current picker. No query, sample, or pattern is written to the tab-session JSON, local Git history, exports, telemetry, or network requests. · 搜尋 query、regex mode、pattern、旗標同 sample text 只留喺今次 session；所選分類係今次 picker 嘅 view filter。任何 query、sample 或 pattern 都唔會寫入 tab-session JSON、本機 Git history、export、telemetry 或 network request。

## Failure modes and security · 失敗處理同安全

- An invalid .NET pattern remains visible in the builder's validation surface and produces no result buttons; the dialog does not silently fall back to a different pattern.
- Patterns and candidate labels are bounded by the shared regex services. A timeout or oversized candidate fails closed for the refresh.
- Plain text escapes no user input into regex because the matcher keeps `UseRegex=false` until the user deliberately enables it.
- Category filtering never changes an item's action or opens a hidden destination; an empty result is an honest empty result.
- Evaluation is local and uses the .NET `System.Text.RegularExpressions` dialect with the shared timeout; the picker performs no network access.

## Verification · 驗證

The runtime wiring is in `MainWindow.xaml.cs`: `SearchPatternBox`, `search.PatternChanged`, `SearchPatternService.Spec`, and one compiled `SearchPatternService.Matcher` feed the real picker result buttons. `tests/ManagedReleaseContract.Tests` keeps a source guard for the shared control, event handoff, matcher type, and result handoff. The shared regex suite covers plain text versus regex, flags, invalid syntax, Unicode, zero-width matches, capture groups, adversarial timeout, and size bounds. · runtime wiring 喺 `MainWindow.xaml.cs`：`SearchPatternBox`、`search.PatternChanged`、`SearchPatternService.Spec` 同一個 compiled `SearchPatternService.Matcher` 會餵入真正 picker result buttons。`tests/ManagedReleaseContract.Tests` 有 source guard，檢查共用 control、event handoff、matcher type 同 result handoff。共用 regex suite 覆蓋純文字／regex、旗標、無效語法、Unicode、零寬配對、capture groups、對抗式 timeout 同大小上限。

## Suggested articles · 建議文章

- [Regex builder](../developer-tooling/regex-builder.md) — shared construction and safety rules. · 共用砌法同安全規則。
- [Pinned tabs and session persistence](pinned-tabs.md) — the tab session that remains separate from picker search. · 同 picker search 分開嘅 tab session。
- [Offline documentation](offline-documentation.md) — the in-app article browser that uses the same local search contract. · 同樣用本機搜尋合約嘅 app 內文件瀏覽器。
