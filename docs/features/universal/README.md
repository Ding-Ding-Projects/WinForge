# Universal experience · 共用體驗

This category documents the cross-surface experience contract that WinForge implements locally. Each page names the persisted state, the user-visible behavior, the failure path, the privacy boundary, and the verification evidence. · 呢個分類記錄 WinForge 喺各介面共用嘅體驗合約。每頁都會講清楚保存狀態、用戶見到嘅行為、失敗處理、私隱界線同驗證證據。

| Feature · 功能 | Documentation · 文件 |
|---|---|
| Shared settings, School mode, emoji switch · 共用設定、School mode、emoji 開關 | [Shared settings](shared-settings.md) |
| Offline changelog viewer · 離線變更紀錄檢視器 | [Offline changelog](offline-changelog.md) |
| Public-catalog dim-sum startup surprise · 公開 catalog 啟動點心驚喜 | [Dim-sum surprise](dim-sum-surprise.md) |
| Bundled offline documentation browser · 捆綁離線文件瀏覽器 | [Offline documentation](offline-documentation.md) |
| New-tab and category picker search and regex builder · 新分頁同分類選擇器搜尋同 regex builder | [New-tab picker search](new-tab-picker-search.md) |
| Pinned tabs and session persistence · 釘選分頁同工作階段保存 | [Pinned tabs](pinned-tabs.md) |
| Local OTP QR pairing · 本機 OTP QR 配對 | [Authenticator QR pairing](authenticator-qr.md) |
| Multi-entry local authenticator vault · 多項本機驗證器 vault | [Authenticator vault](authenticator-vault.md) |
| Scheduled settings and validated external sources · 排程設定同驗證外部來源 | [Scheduled settings](scheduled-settings.md) |
| Local Support Tickets recovery desk · 本機支援工單復原台 | [Support Tickets](support-tickets.md) |
| Destructive-action super confirmation · 破壞性動作超級確認 | [Destructive confirmation](destructive-confirmation.md) |

## Scope and remaining work · 範圍同未完成項目

The current implementation covers the shared settings record, live School-mode language forcing, vault-backed unlock verification, the emoji-message preference, opt-in serialized event narration, offline changelog and documentation browsers, the public-catalog dim-sum startup surprise with first-usable-layout timing and three verified `catalog-v1*` partitions, a pinned-tab surface, a scheduled-settings editor and resolver with local/API/Home Assistant sources, a local Support Tickets recovery desk, a multi-entry vault-backed authenticator, local QR generation for a TOTP registration draft, and the shared destructive-confirmation surface on the audited callers. The remaining universal contract is tracked explicitly: complete menu/dropdown regex coverage, tab docking/locking/group discovery and bulk-close behavior, Word-depth per-element appearance editing and locks, QR image/camera ingestion, broader destructive-action migration, and the remaining app-wide export and bulk-action surfaces. · 目前實作包括共用設定記錄、School mode 即時強制英文、credential vault 解鎖驗證、emoji 訊息偏好、選擇性序列化事件旁白、離線變更紀錄同文件瀏覽器、public-catalog 啟動點心驚喜（第一個可用 layout timing 同三個已驗證 `catalog-v1*` partition）、釘選分頁介面、支援本機／API／Home Assistant 來源嘅排程設定編輯器同 resolver、本機支援工單復原台、多項 vault 驗證器、本機 TOTP 登記 QR，同埋已審核 caller 使用嘅共用破壞性確認介面。其餘共用合約會清楚列出：完整 menu／dropdown regex、分頁 docking／鎖定／group discovery／批量關閉、Word 深度逐元素外觀編輯同鎖定、QR 圖片／相機匯入、更廣泛破壞性動作遷移，同其餘全 app export／bulk action 介面；未完成項目唔會扮成已出貨。

## Verification · 驗證

- `dotnet build WinForge.sln -c Debug -p:Platform=x64` verifies compilation.
- `tests/ManagedReleaseContract.Tests` checks the release/update source contract.
- `.agents/skills/winforge-exhaustive-smoke/scripts/Invoke-WinForgeAllTests.ps1` is the repository's exhaustive local harness.
- `Pages/OfflineDocsPage.xaml` renders the bundled feature and wiki corpus through the local markdown path; its inventory is checked at build time and internal article links stay inside the app.
- The real self-contained build is driven through `build.bat` and `build-installer.bat`; release packaging uses Squirrel.Windows and remains unsigned.

## Suggested articles · 建議文章

- [Managed release contract](../delivery/managed-release-contract.md) — installer and update evidence.
- [Regex builder](../developer-tooling/regex-builder.md) — the shared search pattern control.
- [Settings and control surfaces](../windows-11-advanced/w11p.settingslinks.about.md) — the existing settings entry point.
