# Command Palette Search · 指令面板搜尋

The code-built Command Palette now uses the shared `SearchPatternBox`. Plain text remains the default and preserves provider-owned fuzzy ranking. Explicit regex mode compiles the bounded `.NET` matcher once per refresh and applies it to the rendered result title, subtitle, and provider label. · Code-built Command Palette 而家用共用 `SearchPatternBox`；純文字係預設，保留 provider 自己嘅模糊排名。明確開 regex mode 後，每次 refresh 只編譯一次有界 `.NET` matcher，再套落結果標題、副題同 provider 標籤。

The actual query editor is focused on open. Query-only Enter launches a selected or first result, while Enter in the raw pattern, guided builder, or sample fields stays inside the builder. Invalid patterns, no matches, matcher errors, and language-refreshed accessible names remain visible and named. · 開啟時會 focus 真正 query editor。只限 query 嘅 Enter 會啟動已選／第一項結果；raw pattern、guided builder 或 sample 欄位嘅 Enter 會留喺 builder 入面。無效 pattern、冇配對、matcher error 同 language-refresh accessible name 都會保持可見兼有名。

Verification is recorded by the focused regex-builder contract and generated search-surface inventory. The remaining ordinary searches, specialized dialects, and menu/dropdown/picker migration are still explicit follow-up work. · 驗證由專項 regex-builder contract 同生成搜尋介面清單記錄。其餘一般搜尋、專用方言，同 menu／dropdown／picker 遷移仍然係明確後續工作。
