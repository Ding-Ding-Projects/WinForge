# Native Symbols Palette · 特殊符號調色盤

The native C++/WinRT route module.symbols replaces its pending page with an immutable, local C++ catalog of 226 glyph entries in nine bilingual categories: Arrows, Math, Currency, Punctuation, Greek, Box Drawing, Stars & Bullets, Fractions, and Super/Subscript.

特殊符號調色盤而家係原生 C++/WinRT 頁面。純 C++ 本機目錄有 226 個符號、九個雙語分類；唔會讀檔、唔會開 process、唔會上網。

## Behavior · 行為

- All aliases resolve natively: symbols, glyphs, and module.symbols.
- Literal filtering is case-insensitive and searches glyph, English name, and Cantonese name. An unknown category falls back to All; whitespace-only query stays unfiltered.
- Regex is opt-in only and is bounded PCRE2 over the static local catalog. An invalid expression retains the last valid visible results.
- The full Regex Builder returns verified pattern and flags to this target.
- A glyph reaches the clipboard only after one explicit Copy action. Copy count, category, query, and regex state survive language rerendering.

- 三個 alias 都會去原生頁面：symbols、glyphs 同 module.symbols。
- 預設係唔分大細楷嘅本機文字搜尋，會查符號、英文名同粵語名；未知分類等於全部，淨空白唔會篩走結果。
- Regex 係明確先開、有嚴格限制嘅 PCRE2，只查靜態本機目錄；錯 pattern 會保留最後有效結果。
- 完整 Regex Builder 可以將已驗證嘅 pattern 同 flags 帶返呢頁。
- 只有明確撳 Copy 先會寫入剪貼簿；Copy 次數、分類、query 同 Regex 狀態喺轉語言後仍然保留。

## Evidence · 證據

- Debug native core: **411/411** passing, including eight Symbols catalog/filter parity checks.
- Owned isolated LowLevel MCP headless UI Automation: **238/238** passing, including aliases, local literal/regex filtering, invalid-pattern retention, explicit Copy, builder handoff, accessibility, and horizontal clipping.
- Visual: capture-blocked. The native driver reported unavailable CopyFromScreen; its process-owned PrintWindow fallback was blank or near-uniform, so it was rejected and no image was retained. No stale, synthetic, blank, or managed image is used as native evidence.

Debug core **411/411** 同隔離 LowLevel MCP UI Automation **238/238** 已通過。圖像 capture 正確係 capture-blocked：CopyFromScreen 唔可用，而 PrintWindow fallback 係空白／近乎單色，所以冇保留 PNG。
