# Managed Delivery and Updates · 正式發佈同更新

WinForge's installer, portable archive, updater, launcher, and GitHub release publisher share the pure [`ManagedReleaseContract`](https://github.com/Ding-Ding-Projects/WinForge/blob/main/Services/ManagedReleaseContract.cs). The canonical repository is [`Ding-Ding-Projects/WinForge`](https://github.com/Ding-Ding-Projects/WinForge); the experimental [`codingmachineedge/WinForge-Native`](https://github.com/codingmachineedge/WinForge-Native) project remains separate. · WinForge installer、可攜 ZIP、更新器、launcher 同 GitHub 發佈器共用純合約；正式 repo 係 Ding-Ding-Projects/WinForge，實驗 WinForge-Native 繼續獨立。

## Release contract · Release 合約

| Item · 項目 | Contract · 合約 |
|---|---|
| Version · 版本 | `v1.1.<1..65535>` |
| Installer · 安裝程式 | `Setup.exe` (unsigned Squirrel.Windows installer) |
| Update index · 更新索引 | `RELEASES` |
| Full package · 完整 package | `WinForge-<version>-full.nupkg` |
| Delta package · 增量 package | `WinForge-<version>-delta.nupkg` when Squirrel can produce one |
| Portable · 可攜版 | `WinForge-portable-x64-<version>.zip` |
| Runtime · 執行內容 | app + single-file launcher + side-by-side updater |
| Manifest · 清單 | `WinForge.release.json` binds repository, commit, version, tag, assets, and paths |
| Integrity · 完整性 | local SHA-256 must equal GitHub's `sha256:` digest |

Every push or manual dispatch builds and publishes one unique release after packaging and provenance checks. GitHub Actions deliberately does not run tests or lint; those remain local checks, while packaging and publication are the workflow failure conditions. A branch build is a prerelease; only the exact latest remote `main` commit can be stable/Latest. Existing tags are never recycled. The workflow reads the published release back and verifies the Squirrel asset set, sizes, URLs, digests, tag target, and channel. · 每次 push／手動 dispatch 都會建置同發佈一個唯一 release，先做 package 同 provenance 核對。GitHub Actions 刻意唔跑 tests／lint；嗰啲係本機檢查，workflow 只會因 package 或 publication 失敗。分支只係 prerelease，只有準確最新 remote `main` 可以係穩定 Latest；舊 tag 絕不重用。發佈後會讀返 release，驗證 Squirrel 資產、大小、URL、digest、tag target 同 channel。

## Safe update sequence · 安全更新次序

1. The app reads the public Latest release only from `api.github.com/repos/Ding-Ding-Projects/WinForge`. · App 只讀正式 repo 嘅公開 Latest。
2. It rejects drafts/prereleases, invalid versions, missing/extra assets, wrong URLs, empty/oversized files, and missing digests. · draft／prerelease、版本／資產／URL／大小／digest 唔啱全部拒絕。
3. The visible normal-integrity updater downloads to `%LOCALAPPDATA%\WinForge\updates`, checks Content-Length and SHA-256, and waits for WinForge to close. · 一般權限可見更新器下載到有界 updates 目錄，驗長度／SHA-256，再等 app 關閉。
4. A copied single-file helper validates the install/staging/log boundaries, holds the verified unsigned Squirrel setup against replacement, runs the per-user Squirrel update with persistent logs, then relaunches through the in-root launcher only after the user chooses restart. · 複製 helper 再驗安裝／staging／log 界線、鎖住冇簽名 Squirrel setup 防換檔，等用戶揀 restart 後先套用 update 同寫持久 log，最後經 root 內 launcher 重開。

Development checkouts and elevated launches do not auto-update. `app.autoupdate.enabled` is persisted and defaults on; checks run after startup and every six hours. Installer failures keep diagnostic logs and never count as success. · 開發 checkout／提權啟動唔會自動更新；設定預設開並持久化，啟動後同每六小時檢查。Installer 失敗會保留診斷，絕不扮成功。

## Delivery footprints · 交付 footprint

The unsigned Squirrel.Windows `Setup.exe` is per-user, needs no elevation, creates the standard `RELEASES`/package layout, and ships the complete self-contained runtime. Windows may show an unknown-publisher or SmartScreen warning because signing is prohibited. The portable archive contains the same runtime and manifest without requiring initial setup; application state and update diagnostics still use the user's WinForge LocalAppData directory. · 冇簽名 Squirrel.Windows `Setup.exe` 係每用戶、唔提權，會建立標準 `RELEASES`／package layout，並帶齊自包含 runtime。因為禁止簽名，Windows 可能顯示 unknown-publisher 或 SmartScreen 警告。可攜 ZIP 有同一 runtime 同清單，首次唔使 setup；app 狀態同更新診斷仍然用用戶 WinForge LocalAppData。

## Verification · 驗證

`dotnet run --project tests\ManagedReleaseContract.Tests -c Debug` exercises the pure/static contract without changing the host. Full solution/helper builds run locally; hosted CI owns final Release publication, Squirrel archive inspection, unsigned `Setup.exe` verification, and GitHub digest/provenance proof. Detailed behavior, failure modes, and security notes are in [the categorized feature guide](https://github.com/Ding-Ding-Projects/WinForge/blob/main/docs/features/delivery/managed-release-contract.md). · 專測唔改 host；完整 build 本機跑，最後 Release／Squirrel archive／冇簽名 `Setup.exe`／GitHub digest 同 provenance 由 hosted CI 證明。詳細內容見分類指南。

[← Home · 返主頁](Home) · [Roadmap · 路線圖](https://github.com/Ding-Ding-Projects/WinForge/blob/main/ROADMAP.md)
