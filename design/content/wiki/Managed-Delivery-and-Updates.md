# Managed Delivery and Updates · 正式發佈同更新

WinForge's installer, portable archive, updater, launcher, and GitHub publisher share one tested contract. The canonical repository is `Ding-Ding-Projects/WinForge`; the experimental `codingmachineedge/WinForge-Native` project remains separate. · Installer、可攜 ZIP、更新器、launcher 同 GitHub 發佈器共用一份已測合約；正式 repo 係 Ding-Ding-Projects/WinForge，實驗 WinForge-Native 繼續獨立。

| Item · 項目 | Contract · 合約 |
|---|---|
| Version · 版本 | `v1.1.<1..65535>` |
| Installer · 安裝程式 | `Setup.exe` (unsigned Squirrel.Windows) |
| Update index · 更新索引 | `RELEASES` |
| Full / delta package · Full／delta package | `WinForge-<version>-full.nupkg` / `WinForge-<version>-delta.nupkg` |
| Portable · 可攜版 | `WinForge-portable-x64-<version>.zip` |
| Manifest · 清單 | `WinForge.release.json` binds repository, commit, version, tag, assets, and runtime paths |
| Integrity · 完整性 | local SHA-256 equals GitHub's `sha256:` digest |

Every push or manual dispatch builds and publishes one unique release. GitHub Actions deliberately does not run tests or lint; those remain local checks and never block packaging. Branch builds are prereleases; only the exact latest remote `main` commit can be stable/Latest, and existing tags are immutable. The workflow verifies the published Squirrel asset set, sizes, canonical URLs, digests, tag target, channel, executable versions, portable footprint, provenance manifest, and unsigned setup. · 每次 push／手動 dispatch 都會建置同發佈一個唯一 release。GitHub Actions 刻意唔跑 tests／lint；嗰啲係本機檢查，唔會阻擋 package。分支只係 prerelease，只有準確最新 remote `main` 可以穩定，舊 tag 唔重用。Workflow 會驗 Squirrel 資產、大小、URL、digest、tag、頻道、exe 版本、可攜 footprint、provenance manifest 同冇簽名 setup。

## Safe updater · 安全更新器

The app accepts only a newer stable `v1.1.x` release containing `Setup.exe`, `RELEASES`, the versioned full package, and the versioned portable archive, with optional current-version delta packages. The visible updater runs at normal integrity, bounds and length-checks its download, verifies SHA-256, validates direct-child app/launcher paths, and stages the unsigned setup under LocalAppData. A non-blocking notification offers **Restart to install update** or **Later**; active work is not interrupted and no install occurs without the user's choice. · App 只接受新過目前嘅穩定 `v1.1.x`，資產包括 `Setup.exe`、`RELEASES`、versioned full package 同 portable archive，亦可有 current-version delta。可見更新器保持一般權限，限制下載、驗長度／SHA-256／app path，將冇簽名 setup stage 喺 LocalAppData。唔阻塞通知會畀用戶揀「重新啟動並安裝更新」或者「稍後」；有工作進行時唔會打斷，未揀之前唔會安裝。

Development/elevated runs fail closed. The unsigned Squirrel setup needs no initial admin elevation, but Windows may show an unknown-publisher or SmartScreen warning because code signing is prohibited. The portable ZIP needs no initial setup but still uses WinForge LocalAppData for state and update diagnostics. Focused pure/static verification is `dotnet run --project tests\ManagedReleaseContract.Tests -c Debug`. · 開發／提權執行會 fail closed。冇簽名 Squirrel setup 唔需要初始管理員提權，但因為禁止 code signing，Windows 可能顯示 unknown-publisher 或 SmartScreen 警告。可攜 ZIP 首次唔使 setup，但狀態／更新診斷仍用 WinForge LocalAppData。專測 command 如上。
