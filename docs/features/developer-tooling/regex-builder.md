# Managed guided Regex Builder · 正式受管理引導式正則砌法

## Behavior · 行為

`module.regextester` uses the real .NET 11 `System.Text.RegularExpressions` dialect. The raw pattern editor stays authoritative. Its guided builder inserts escaped literal text, escaped or negated character classes, six anchors, numbered/named/non-capturing groups, alternation, and bounded quantifiers at the current selection. Five supported flags (`IgnoreCase`, `Multiline`, `Singleline`, `IgnorePatternWhitespace`, and `ExplicitCapture`) feed the same live evaluator. Test text produces indexed matches and capture groups; replacement text produces a preview. Pattern and match copying are explicit user actions. · `module.regextester` 用真正 .NET 11 `System.Text.RegularExpressions` 方言，原始 pattern 編輯器係權威來源。引導砌法會喺目前 selection 插入已跳脫字面文字、已跳脫／反轉字元類、六種錨點、編號／具名／唔擷取群組、二選一同有界量詞。五個支援旗標會餵入同一個即時 evaluator。測試文字會顯示有位置嘅配對同擷取群組；替換文字會產生預覽。複製 pattern 或配對一定要用戶明確操作。

The page is bilingual through `Loc.I.Pick`. Builder fields stack vertically, option controls use visible keyboard focus, and the page scrolls instead of truncating the long bilingual surface. Open it with `WinForge.exe --page regextester`. · 呢頁經 `Loc.I.Pick` 提供雙語；builder 欄位直排、選項控制有清楚鍵盤 focus，而長雙語內容會滾動，唔會硬截。用 `WinForge.exe --page regextester` 開啟。

## Configuration and engine rules · 設定同引擎規則

- Engine/dialect: .NET 11 `System.Text.RegularExpressions`; backslash escaping follows .NET rules. · 引擎／方言：.NET 11 `System.Text.RegularExpressions`；反斜線按 .NET 規則跳脫。
- Plain text is still the default in the shared `SearchPatternService`; regex requires an explicit `UseRegex` state. · 共用 `SearchPatternService` 仍然預設純文字；regex 要明確開 `UseRegex`。
- Patterns, samples, replacements, and results are not transmitted or persisted by this page. Clipboard output occurs only after a Copy action. · 呢頁唔會傳送或保存 pattern、sample、replacement 或結果；只會喺明確按 Copy 後寫剪貼簿。
- The project-wide direct builder handoff and bidirectional flag synchronization for every search bar remains an open integration. This foundation does not mislabel those surfaces as complete. · 每個搜尋欄直接開 builder 同雙向同步旗標仍然係開放整合；呢個基礎唔會扮嗰啲 surface 已完成。

## Bounds, failure modes, and security · 上限、故障模式同安全

Patterns are limited to 4,096 characters, samples to 1,000,000, replacements to 65,536, and displayed matches to 2,000. Matching and replacement use a one-second timeout; replacement preview also has a conservative 8,000,000-unit work guard. Zero-width matches advance through `.NET Match.NextMatch()`. Invalid syntax, oversized data, unsafe replacement work, and timeouts fail closed with bilingual feedback; no pattern becomes code, a command argument, a process launch, or a network request. · Pattern 上限 4,096 字元、sample 1,000,000、replacement 65,536，顯示配對最多 2,000。配對／替換有一秒超時，替換預覽另有保守 8,000,000 工作量閘。零寬度配對經 `.NET Match.NextMatch()` 安全推進。語法錯、資料過大、替換工作量唔安全或者超時都會 fail closed 並顯示雙語提示；pattern 永遠唔會變成 code、命令參數、程序啟動或者網絡要求。

## Verification · 驗證

`dotnet run --project tests/RegexBuilder.Tests -c Debug` passes **13/13**: literal/class escaping, every guided family, invalid inputs, Unicode, captures, syntax error, no-match, multiline, zero-width, result caps, size caps, catastrophic-backtracking timeout, and plain-text-versus-regex semantics. `dotnet build WinForge.sln -c Debug -p:Platform=x64` passes with zero warnings and zero errors for this change. Fresh app-owned and LowLevel headless frames were inspected at approximately 1049×646; the vertical flag layout has no horizontal clipping, the builder is keyboard reachable with a visible focus outline, and the owned process/desktop were closed. · 專項 harness **13/13** 全過，涵蓋跳脫、全部引導類別、錯誤輸入、Unicode、擷取、語法錯、無配對、多行、零寬度、結果／大小上限、災難性回溯超時，同純文字對 regex 語意。Solution x64 build 零 warning／零 error。已檢視約 1049×646 app-owned 同 LowLevel headless 圖；直排旗標冇水平裁切、builder 可以用鍵盤到達兼有可見 focus，而自家 process／desktop 已關閉。
