# Managed Delivery and Updates · 正式發佈同更新

WinForge's installer, portable archive, updater, launcher, and GitHub publisher share one tested contract. The canonical repository is `Ding-Ding-Projects/WinForge`; the experimental `codingmachineedge/WinForge-Native` project remains separate. · Installer、可攜 ZIP、更新器、launcher 同 GitHub 發佈器共用一份已測合約；正式 repo 係 Ding-Ding-Projects/WinForge，實驗 WinForge-Native 繼續獨立。

| Item · 項目 | Contract · 合約 |
|---|---|
| Version · 版本 | `v1.1.<1..65535>` |
| Installer · 安裝程式 | `WinForge-Setup.exe` |
| Portable · 可攜版 | `WinForge-portable-x64-<version>.zip` |
| Manifest · 清單 | `WinForge.release.json` binds repository, commit, version, tag, assets, and runtime paths |
| Integrity · 完整性 | local SHA-256 equals GitHub's `sha256:` digest |

Every push or manual dispatch tests first. Branch builds are prereleases; only the exact latest remote `main` commit can be stable/Latest, and existing tags are immutable. The workflow verifies the published asset set, sizes, canonical URLs, digests, tag target, channel, executable versions, portable footprint, and Inno metadata. · 每次 push／手動 dispatch 都先測試；分支只係 prerelease，只有準確最新 remote `main` 可以穩定，舊 tag 唔重用。Workflow 會驗資產、大小、URL、digest、tag、頻道、exe 版本、可攜 footprint 同 Inno metadata。

## Safe updater · 安全更新器

The app accepts only a newer stable `v1.1.x` release containing exactly the setup and versioned portable assets at canonical HTTPS tag paths with valid GitHub digests. The visible updater runs at normal integrity, bounds and length-checks its download, verifies SHA-256, validates direct-child app/launcher paths, and hands off to a copied helper under LocalAppData. The helper serializes updates, contains setup/log paths, prevents verified-installer replacement, runs per-user Inno Setup with persistent logs, and relaunches only through the validated in-root executable. · App 只接受新過目前嘅穩定 `v1.1.x`，資產／正式 HTTPS tag 路徑／GitHub digest 要全啱。可見更新器保持一般權限，限制下載、驗長度／SHA-256／app 路徑，再交俾 LocalAppData helper；helper 會串行化、限制 setup／log、防止驗證後換檔，用 per-user Inno 同持久 log，最後只經已驗證 root 內 exe 重開。

Development/elevated runs fail closed. The installer defaults to `%LOCALAPPDATA%\Programs\WinForge`; the portable ZIP needs no initial setup but still uses WinForge LocalAppData for state and update diagnostics. Focused pure/static verification is `dotnet run --project tests\ManagedReleaseContract.Tests -c Debug`. · 開發／提權執行會 fail closed。Installer 預設裝 LocalAppData；可攜 ZIP 首次唔使 setup，但狀態／更新診斷仍用 WinForge LocalAppData。專測 command 如上。
