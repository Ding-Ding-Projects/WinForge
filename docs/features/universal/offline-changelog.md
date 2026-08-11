# Offline changelog viewer · 離線變更紀錄檢視器

## Behavior · 行為

`CHANGELOG.md` is copied into the application output and parsed by `Services/ChangelogService`. The About surface exposes the entries without a network request. It shows the recorded heading, a readable plain-text rendering of the body, date provenance, and a commit action when the source entry contains a commit SHA. Missing dates and missing commit links are stated instead of guessed. · `CHANGELOG.md` 會複製到 app output，由 `Services/ChangelogService` 解析。About 介面唔使上網就可以睇 entries，顯示標題、可讀嘅純文字 body、日期來源，同埋來源有 commit SHA 時嘅 commit action。冇日期或者冇 commit link 會直接講明，唔會亂估。

The search field is plain-text first and is the owning field for its full .NET regex builder. Regex mode supports the same bounded flags and 250 ms evaluation limit as the shared search control. Optional date pickers accept the platform date control's input and filter out entries with no recorded date when a date range is active. · 搜尋欄預設純文字，隔籬完整 .NET 正則 builder 屬於自己個欄位。Regex mode 支援共用 search control 相同嘅 bounded flags 同 250 ms 評估上限。可選日期 picker 用平台日期控制輸入；開啟日期範圍時，冇日期記錄嘅 entry 會排除。

The filtered view can be exported to Markdown or text through the normal WinForge file-dialog path. The export records the active date range and keeps the source entry's body; it does not silently drop fields. · 已篩選畫面可以經 WinForge 正常 file-dialog path 匯出 Markdown 或 text。匯出檔會記錄日期範圍同來源 body，唔會靜靜雞漏欄位。

## Security and failure modes · 安全同失敗處理

- The viewer reads only the bundled file and cannot turn a release-note link into a privileged action.
- A missing output file produces an explicit unavailable state rather than a blank loading panel.
- Invalid or slow regex patterns produce inline feedback and no matches; they never block the UI thread.
- The source file is authored repository content, not untrusted provider text. If remote-authored Markdown is added later, it must use the shared isolated renderer before display.

## Verification · 驗證

The pure parser/filter/export contract is in `Services/ChangelogService.cs`; the About integration is in `Pages/AboutPage.xaml.cs`; `CHANGELOG.md` is an explicit content item in `WinForge.csproj`. · 純 parser/filter/export 合約喺 `Services/ChangelogService.cs`；About integration 喺 `Pages/AboutPage.xaml.cs`；`CHANGELOG.md` 喺 `WinForge.csproj` 明確列為 content。

## Suggested articles · 建議文章

- [Shared settings](shared-settings.md) — language and message preferences.
- [Regex builder](../developer-tooling/regex-builder.md) — construction and bounded evaluation.
- [Local Git operations](../git-github/git.branch-list.md) — repository-backed user state where available.
