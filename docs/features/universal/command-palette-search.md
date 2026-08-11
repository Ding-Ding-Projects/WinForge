# Command Palette search · 指令面板搜尋

## Behavior · 行為

The code-built Command Palette uses the shared `SearchPatternBox`. Plain text remains the default and keeps the existing provider-owned fuzzy ranking. When the user deliberately enables regex mode, the palette compiles the complete `.NET` pattern and supported flags once per refresh, then applies that bounded matcher to each rendered result's title, subtitle, and provider label. Provider actions and provider-specific discovery remain unchanged. · 用 code-built 方式建立嘅 Command Palette 而家用共用 `SearchPatternBox`。純文字繼續係預設，保留原有 provider 自己控制嘅模糊排名。用戶明確開 regex mode 後，palette 每次 refresh 只編譯一次完整 `.NET` pattern 同支援旗標，再將有界 matcher 套落每個結果嘅標題、副題同 provider 標籤；provider action 同 provider 自己嘅 discovery 行為保持不變。

The real query editor receives focus when the palette opens. Enter from that editor launches the selected result (or the first result); Enter in the raw-pattern, guided-builder, or sample fields does not launch a command. Arrow navigation, `Ctrl+P`, and Escape remain available through the palette's keyboard surface. · Palette 開啟時會將 focus 放到真正 query editor。喺 query editor 按 Enter 會啟動已揀結果（或者第一項）；喺 raw pattern、guided builder 或 sample 欄位按 Enter 唔會啟動 command。上下方向鍵、`Ctrl+P` 同 Escape 繼續由 palette 鍵盤介面處理。

## Configuration and persistence · 設定同保存

The query, regex mode, flags, raw pattern, builder sample, and preview are session-only. The palette does not write them to tab-session state, local history, exports, telemetry, or network requests. The shared control exposes a localized accessible name and stable nested automation identifiers for the query, regex mode, builder, validation, and status surfaces. · Query、regex mode、旗標、raw pattern、builder sample 同 preview 只限今次 session。Palette 唔會將佢哋寫入 tab-session 狀態、本機歷史、export、telemetry 或 network request。共用控制會提供本地化 accessible name，同埋為 query、regex mode、builder、validation 同 status 介面提供穩定 nested automation identifier。

## Failure modes and security · 失敗處理同安全

- Invalid `.NET` syntax is shown by the shared builder and leaves the result list empty; the palette never silently falls back to fuzzy search.
- A valid pattern with no matching rendered result shows an explicit no-result status rather than a blank list with no explanation.
- A bounded matcher error, oversized candidate, or timeout fails closed for that refresh and is shown as a localized search error.
- Plain text is not converted into regex until the user enables regex mode. Patterns remain local and are never treated as commands, file paths, process arguments, or network URLs.

## Verification · 驗證

`tests/RegexBuilder.Tests` includes a source contract for the Command Palette's shared control, query-only Enter path, compiled matcher, error/no-result status, real-query focus, and language-refresh wiring. The generated inventory records `Services/CommandPaletteWindow.cs#_search` as `SearchPatternBox` / `integrated-core`. The solution build remains the compile check. A fresh visual capture is not claimed by this bounded lane when the first-run consent surface prevents reaching the palette without user action. · `tests/RegexBuilder.Tests` 包含 Command Palette 共用控制、只限 query 嘅 Enter 路徑、compiled matcher、error／no-result status、真正 query focus 同 language-refresh wiring 嘅 source contract。生成清單會將 `Services/CommandPaletteWindow.cs#_search` 記錄成 `SearchPatternBox`／`integrated-core`。Solution build 繼續係 compile check。呢條有限 lane 如果因為首次啟動 consent surface 要用戶操作而未能到達 palette，就唔會冒充有新鮮 visual capture。

## Suggested articles · 建議文章

- [Regex builder](../developer-tooling/regex-builder.md) — the shared pattern engine, builder controls, bounds, and timeout behavior. · 共用 pattern engine、builder 控制、上限同 timeout 行為。
- [New-tab picker search](new-tab-picker-search.md) — the other code-built picker using the same query-only Enter contract. · 另一個用同一套只限 query Enter 合約嘅 code-built picker。
- [Offline changelog](offline-changelog.md) — a page-local search surface using the same bounded matcher contract. · 用同一套有界 matcher 合約嘅頁面搜尋介面。
