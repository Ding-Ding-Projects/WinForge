# New-tab picker search · 新分頁選擇器搜尋

## Behavior · 行為

The Ctrl+T and Add-tab picker uses the shared `SearchPatternBox` rather than a second plain text implementation. Plain text remains the default. The adjacent regex control opens the complete bounded .NET builder for literals, character classes, anchors, groups, alternation, quantifiers, raw patterns, supported flags, a local sample preview, syntax feedback, capture results, and copy. · Ctrl+T 同 Add-tab picker 而家用共用 `SearchPatternBox`，唔再另起一套純文字搜尋。純文字繼續係預設；旁邊嘅 regex 控制會開啟完整、有界嘅 .NET builder，支援字面文字、字元類、錨點、群組、二選一、量詞、原始 pattern、支援旗標、本機 sample 預覽、語法提示、capture 結果同複製。

The query searches each entry's title, bilingual subtitle, key, and category labels. Category filtering composes with the query. When the query is empty, the picker keeps its frequent, suggested, and category sections. A no-match or matcher-error state remains visible and named instead of leaving an empty panel. When results exist, Enter from the real query editor opens the first result; Enter in the raw-pattern or sample fields does not close the picker, and each result remains a real keyboard-focusable button. · 搜尋會查每個項目嘅標題、雙語副標題、key 同分類標籤；分類篩選會同 query 一齊生效。query 空白時，picker 保留常用、建議同分類分段。冇結果或者 matcher error 時會保留具名狀態，唔會得返一塊空白。真正 query editor 按 Enter 會開第一項；raw-pattern 或 sample 欄位按 Enter 唔會關閉 picker，而每個結果都係真正可用鍵盤聚焦嘅 button。

The category dropdown is a `SearchablePickerBox` with its own anchored `SearchPatternBox`. Its plain-text-first query matches the category's stable id and both localized labels; regex mode uses the same bounded .NET matcher, flags, validation, and error handling as the main picker search. Arrow keys move through the filtered list, Enter commits the highlighted category, Escape closes it, and closing returns focus to the category button. A named live status reports invalid patterns, matcher failures, and no matching categories without altering the selected category. · 分類 dropdown 係 `SearchablePickerBox`，自己擁有 anchored `SearchPatternBox`。佢嘅純文字預設 query 會查分類穩定 id 同兩種本地化標籤；regex mode 用同主 picker 搜尋一樣嘅有界 .NET matcher、旗標、驗證同 error handling。方向鍵會喺篩選後清單移動，Enter 確認 highlight 嘅分類，Escape 關閉，而關閉後 focus 會返分類按鈕。具名 live status 會報告無效 pattern、matcher failure 同搵唔到分類，唔會改走已揀分類。

The query field owns the flyout keyboard contract: Down/Up move into the current filtered option list, Enter commits the highlighted option, and Escape clears an active query before closing on the next Escape. Language and both persisted funny-level settings refresh the flyout label, placeholder, status copy, accessible names, and help text live; category names and IDs remain factual. The flyout width and option-list height are calculated from the live `XamlRoot` viewport at opening, and the nested builder receives the same width cap. This viewport calculation is runtime-only; source contracts cover the fallback and sizing path, while a real narrow-layout screenshot requires the repository's off-screen driver and a user-approved first-run state. · query field 自己擁有 flyout 鍵盤合約：Down／Up 入到目前篩選後嘅選項清單，Enter 確認 highlight 嘅選項，而 Escape 先清除 active query，再撳一次先關閉。語言同兩個持久 funny-level settings 會即時更新 flyout label、placeholder、status copy、accessible names 同 help text；分類名稱同 ID 保持事實原樣。Flyout 寬度同選項清單高度會喺開啟時按 live `XamlRoot` viewport 計算，nested builder 亦收到同一個 width cap。呢個 viewport 計算係 runtime-only；source contracts 會守住 fallback 同 sizing path，但真正窄版 screenshot 需要 repo off-screen driver 同使用者批准嘅 first-run state。

## Configuration and persistence · 設定同保存

The search query, regex mode, pattern, flags, and sample text are session-only. The selected category is a view filter for the current picker. No query, sample, or pattern is written to the tab-session JSON, local Git history, exports, telemetry, or network requests. · 搜尋 query、regex mode、pattern、旗標同 sample text 只留喺今次 session；所選分類係今次 picker 嘅 view filter。任何 query、sample 或 pattern 都唔會寫入 tab-session JSON、本機 Git history、export、telemetry 或 network request。

## Failure modes and security · 失敗處理同安全

- An invalid .NET pattern remains visible in the builder's validation surface and produces no result buttons; the dialog does not silently fall back to a different pattern.
- Patterns and candidate labels are bounded by the shared regex services. A timeout or oversized candidate fails closed for the refresh.
- Plain text escapes no user input into regex because the matcher keeps `UseRegex=false` until the user deliberately enables it.
- Category filtering never changes an item's action or opens a hidden destination; an empty result is an honest empty result.
- Evaluation is local and uses the .NET `System.Text.RegularExpressions` dialect with the shared timeout; the picker performs no network access.

## Verification · 驗證

The runtime wiring is in `MainWindow.xaml.cs`: `SearchPatternBox` and `SearchablePickerBox` are separate owned controls. The category control keeps the original `PickerCategory` objects and selection event, composes its own `SearchPatternBox` matcher with category metadata, exposes stable automation IDs, and returns focus from its flyout. `tests/ManagedReleaseContract.Tests` and `tests/ShellAllAppsRoute.Tests` guard these seams; the shared regex suite covers plain text versus regex, flags, invalid syntax, Unicode, zero-width matches, capture groups, adversarial timeout, and size bounds. · runtime wiring 喺 `MainWindow.xaml.cs`：`SearchPatternBox` 同 `SearchablePickerBox` 係兩個各自擁有嘅 control。分類 control 保留原本 `PickerCategory` object 同 selection event，用自己嘅 `SearchPatternBox` matcher 同分類 metadata 接合，提供穩定 automation ID，並由 flyout 關閉後返 focus。`tests/ManagedReleaseContract.Tests` 同 `tests/ShellAllAppsRoute.Tests` 守住呢啲 seam；共用 regex suite 覆蓋純文字／regex、旗標、無效語法、Unicode、零寬配對、capture groups、對抗式 timeout 同大小上限。

## Live voice and verification boundary · 即時語氣同驗證邊界

Category labels, the selected-value button, and the result section title pass through the persisted English and Cantonese funny-level styles; the nested search control refreshes its accessible names when either tone changes. The final category status automation ID is `NewTabPickerCategoryBox_Status`, and the regex descendants are namespaced below `NewTabPickerCategorySearchBox`. `ShellAllAppsRoute.Tests` provides a pure source contract for these seams; real WinUI focus, flyout, viewport, automation-tree, and rendered-copy behavior still requires the built artifact. The first-run Terms & Conditions surface prevents a new screenshot from being claimed without user consent. · 分類標籤、已揀值按鈕同結果分段標題會經持久英文／廣東話 funny-level style；nested search control 會喺任何一個語氣改變時更新 accessible names。分類 status 最終 automation ID 係 `NewTabPickerCategoryBox_Status`，regex descendants 就 namespace 喺 `NewTabPickerCategorySearchBox` 下面。`ShellAllAppsRoute.Tests` 提供呢啲 seam 嘅純 source contract；真正 WinUI focus、flyout、viewport、automation tree 同顯示文案仍然要由 built artifact 驗證。首次 Terms & Conditions 畫面未有用戶同意，所以今次唔會聲稱有新 screenshot。

## Suggested articles · 建議文章

- [Regex builder](../developer-tooling/regex-builder.md) — shared construction and safety rules. · 共用砌法同安全規則。
- [Pinned tabs and session persistence](pinned-tabs.md) — the tab session that remains separate from picker search. · 同 picker search 分開嘅 tab session。
- [Offline documentation](offline-documentation.md) — the in-app article browser that uses the same local search contract. · 同樣用本機搜尋合約嘅 app 內文件瀏覽器。
