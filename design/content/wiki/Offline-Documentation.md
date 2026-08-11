# Offline documentation browser · 離線文件瀏覽器

WinForge bundles the feature and wiki Markdown corpus and renders it in the `Offline Documentation`
destination. Search is plain text by default with the shared regex builder available beside the
field; internal article links remain inside the local browser and do not require network access.
· WinForge 將功能同 wiki Markdown corpus 捆綁入 `Offline Documentation` 目的地。搜尋預設係純文字，
欄位旁邊有共用 regex builder；文章內部連結會留喺本機瀏覽器，唔需要網絡。

The build-time inventory rejects an empty source corpus and verifies that the expected bundled
articles are present. The renderer uses bounded local content and the `winforge-doc:///` scheme;
ordinary external navigation is blocked. · Build 時 inventory 會拒絕空白 source corpus，亦會驗證預期
捆綁文章存在。renderer 只用有界本機內容同 `winforge-doc:///` scheme；普通 external navigation 會封鎖。

Source: `Services/OfflineDocumentationService.cs`, `Pages/OfflineDocsPage.xaml`; tests:
`tests/OfflineDocs.Tests/Program.cs` (`5/5`). · 來源：`Services/OfflineDocumentationService.cs`、
`Pages/OfflineDocsPage.xaml`；測試：`tests/OfflineDocs.Tests/Program.cs`（`5/5`）。

![Offline documentation article rendered inside the built WinForge application](https://raw.githubusercontent.com/Ding-Ding-Projects/WinForge/main/docs/screenshot-offline-documentation-2026-08-11.png)
