# Universal experience · 共用體驗

This category documents the cross-surface experience contract that WinForge implements locally. Each page names the persisted state, the user-visible behavior, the failure path, the privacy boundary, and the verification evidence. · 呢個分類記錄 WinForge 喺各介面共用嘅體驗合約。每頁都會講清楚保存狀態、用戶見到嘅行為、失敗處理、私隱界線同驗證證據。

| Feature · 功能 | Documentation · 文件 |
|---|---|
| Shared settings, School mode, emoji switch · 共用設定、School mode、emoji 開關 | [Shared settings](shared-settings.md) |
| Offline changelog viewer · 離線變更紀錄檢視器 | [Offline changelog](offline-changelog.md) |
| Pinned tabs and session persistence · 釘選分頁同工作階段保存 | [Pinned tabs](pinned-tabs.md) |
| Local OTP QR pairing · 本機 OTP QR 配對 | [Authenticator QR pairing](authenticator-qr.md) |

## Scope and remaining work · 範圍同未完成項目

The current implementation covers the shared settings record, live School-mode language forcing, vault-backed unlock verification, the emoji-message preference, opt-in serialized event narration, an offline changelog surface, pinned tabs, and local QR generation for a TOTP registration draft. The complete universal contract still requires a full multi-entry authenticator, a bundled dim-sum image surprise, per-element appearance locks, all menu/dropdown regex fields, support tickets, and the remaining bulk/export surfaces. Those are tracked as explicit work rather than presented as shipped behavior. · 目前實作包括共用設定記錄、School mode 即時強制英文、credential vault 解鎖驗證、emoji 訊息偏好、選擇性序列化事件旁白、離線變更紀錄、釘選分頁，同埋 TOTP 登記草稿嘅本機 QR 產生。完整共用合約仍然需要多項 authenticator、內置點心圖片驚喜、逐元素外觀鎖、所有 menu/dropdown 正則欄位、support tickets 同其餘 bulk/export 介面；未完成項目會清楚列出，唔會扮成已經出貨。

## Verification · 驗證

- `dotnet build WinForge.sln -c Debug -p:Platform=x64` verifies compilation.
- `tests/ManagedReleaseContract.Tests` checks the release/update source contract.
- `.agents/skills/winforge-exhaustive-smoke/scripts/Invoke-WinForgeAllTests.ps1` is the repository's exhaustive local harness.
- The real self-contained build is driven through `build.bat` and `build-installer.bat`; release packaging uses Squirrel.Windows and remains unsigned.

## Suggested articles · 建議文章

- [Managed release contract](../delivery/managed-release-contract.md) — installer and update evidence.
- [Regex builder](../developer-tooling/regex-builder.md) — the shared search pattern control.
- [Settings and control surfaces](../windows-11-advanced/w11p.settingslinks.about.md) — the existing settings entry point.
