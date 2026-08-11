# Managed Release Contract · 正式發佈合約

## Outcome · 結果

The managed app, visible updater, single-file launcher, unsigned Squirrel.Windows package, portable archive, and GitHub Actions publisher now share one audited contract. The canonical repository is `Ding-Ding-Projects/WinForge`; the retired managed coordinate is rejected, while `codingmachineedge/WinForge-Native` remains the separate experimental native project. · 正式 app、可見更新器、單檔 launcher、冇簽名 Squirrel.Windows package、可攜壓縮檔同 GitHub Actions 而家共用一份已審核合約。正式 repository 係 `Ding-Ding-Projects/WinForge`；舊 managed 座標會被拒絕，而 `codingmachineedge/WinForge-Native` 繼續係獨立實驗原生 project。

| Contract field · 合約欄位 | Required value · 必須值 |
|---|---|
| Stable version line · 穩定版本線 | `v1.1.<1..65535>` |
| Installer asset · 安裝資產 | `Setup.exe` (unsigned Squirrel.Windows) |
| Update index · 更新索引 | `RELEASES` |
| Full / delta package · Full／delta package | `WinForge-<version>-full.nupkg` / `WinForge-<version>-delta.nupkg` |
| Portable asset · 可攜資產 | `WinForge-portable-x64-<version>.zip` |
| Packaged manifest · 內附清單 | `WinForge.release.json` |
| App / launcher / updater · 主程式／啟動器／更新器 | `WinForge.exe`, `WinForgeLauncher.exe`, `updater-runtime/WinForgeUpdater.exe` |
| Integrity · 完整性 | GitHub `sha256:<64 hex>` digest matching the locally built file |
| Stable channel · 穩定頻道 | non-draft, non-prerelease release whose tag targets the exact current remote `main` commit |

## Build and publication · 建置同發佈

Every push and manual dispatch builds the managed release before publication. GitHub Actions deliberately does not run tests or lint; those remain local checks and do not block the build workflow. The workflow resolves one immutable source SHA and one unique `1.1.x` version, publishes all three executables with the same ProductVersion/FileVersion, writes a provenance manifest, checks the required runtime footprint, builds the versioned portable ZIP and unsigned Squirrel.Windows assets, and then creates exactly one new release. Existing tags are immutable and cause a failure instead of reuse. Branch tips publish prereleases; only the exact current remote `main` tip can become stable and GitHub Latest. · 每次 push 同手動 dispatch 都會先建置 managed release。GitHub Actions 刻意唔跑 tests／lint；嗰啲係本機檢查，唔會阻擋 build workflow。Workflow 會鎖定一個 source SHA 同唯一 `1.1.x` version，將三個 exe 寫成一致 ProductVersion／FileVersion、產生 provenance manifest、檢查 runtime footprint、整 versioned portable ZIP 同冇簽名 Squirrel.Windows 資產，之後建立一個新 release。舊 tag 唔會重用；分支只出 prerelease，只有準確最新 remote `main` tip 可以成為穩定 Latest。

The release workflow scopes its push trigger to branches, so the immutable tag created by publication cannot trigger a second release or a tag-to-tag automation loop. · Release workflow 嘅 push trigger 只限 branch，咁 publication 建立嘅 immutable tag 就唔會再觸發第二次 release，亦唔會形成 tag-to-tag automation loop。

After upload, the workflow reads the release back from GitHub and proves exact Squirrel asset names, nonzero sizes, canonical HTTPS download paths, local-to-GitHub SHA-256 equality, tag-to-commit provenance, channel, and Latest status when applicable. Failed packaging or failed post-upload proof does not report success. · 上載後 workflow 會由 GitHub 讀返 release，逐項證明 Squirrel 資產名、大小、正式 HTTPS 路徑、本機／GitHub SHA-256、tag／commit、頻道，同適用時嘅 Latest 狀態；package 或上載後證明失敗都唔會扮成功。

## Installed and portable footprints · 安裝同可攜 footprint

- Unsigned Squirrel.Windows `Setup.exe` installs per user, requires no elevation, creates the standard `RELEASES`/package layout, and ships the complete self-contained runtime. Windows may show an unknown-publisher or SmartScreen warning because signing is prohibited. · 冇簽名 Squirrel.Windows `Setup.exe` 會每用戶安裝，唔使提權，建立標準 `RELEASES`／package layout，並帶齊自包含 runtime。因為禁止簽名，Windows 可能顯示 unknown-publisher 或 SmartScreen 警告。
- The portable ZIP contains the same app, launcher, updater runtime, assets, and `WinForge.release.json`, but requires no initial setup. “Portable” describes distribution; WinForge settings and update diagnostics remain under the user's WinForge LocalAppData directory. · 可攜 ZIP 有同一套 app、launcher、更新器 runtime、資產同清單，首次使用唔使 setup。「可攜」指派發方式；設定同更新診斷仍然放喺用戶 WinForge LocalAppData。
- The manifest binds repository, source SHA, version, tag, Squirrel asset names, portable asset name, and runtime entry paths. It is packaged inside both delivery forms; GitHub's release JSON remains the network update manifest and supplies the independently verified asset digests. · 內附清單綁實 repository、source SHA、版本、tag、Squirrel 資產名、portable 資產名同 runtime 路徑；兩種交付都有。網絡更新清單仍然係 GitHub release JSON，並提供獨立驗證嘅 asset digest。

## Updater behavior and configuration · 更新器行為同設定

`app.autoupdate.enabled` defaults on. A normal-integrity installed or extracted release checks GitHub after the startup delay and then every six hours. Development checkouts and elevated launches fail closed. A candidate must be stable, newer, in the managed version line, contain `Setup.exe`, `RELEASES`, the versioned full package, and the portable archive (plus an optional current-version delta), use canonical tag-bound HTTPS URLs, stay inside size limits, and provide valid digests. The app stages the update and shows a non-blocking ready-to-restart action; it never exits or installs without the user's explicit restart choice. · `app.autoupdate.enabled` 預設開。一般權限 release 會喺啟動延遲後檢查，之後每六小時一次；開發 checkout 同提權啟動會 fail closed。候選版本要係穩定、新過目前版本、符合版本線，準確有 `Setup.exe`、`RELEASES`、versioned full package、portable archive（再加可選 current-version delta），用綁定 tag 嘅正式 HTTPS 路徑、大小有界兼有有效 digest。App 會 stage update 再顯示唔阻塞嘅 restart-ready action；冇用戶明確揀 restart 就唔會自動退出或安裝。

The updater validates the installation root and requires the launcher/executable to be direct expected children. Downloads stay directly below `%LOCALAPPDATA%\WinForge\updates`, use a bounded temporary file, must match Content-Length, and are SHA-256 checked. The copied helper runs at normal integrity, serializes through a per-user mutex, waits for owned processes, holds the verified setup open without write/delete sharing, writes contained persistent logs, and clears the pending flag only after success or an honest failure path. · 更新器會驗證安裝 root，launcher／exe 必須係預期直接子檔。下載只放 updates 目錄、用有界 temp、要同 Content-Length 一致兼驗 SHA-256。複製 helper 保持一般權限，用每用戶 mutex、等待自家 process、鎖住已驗證 setup 防止 hash 後被換檔、寫有界持久記錄，最後先按真實結果清 pending flag。

## Failure modes and recovery · 失敗模式同復原

- Missing/wrong assets, tags, URLs, sizes, or digests are rejected before download or execution. · 資產、tag、URL、大小或 digest 唔啱，下載／執行前就拒絕。
- A truncated download deletes its temporary file and retries on a later update cycle. · 截短下載會刪 temp，之後週期再試。
- Elevation, an invalid target layout, an escaped staging/log path, another live helper, a process timeout, or a Squirrel packaging/install failure stops safely and retains diagnostics. · 提權、target 越界、staging／log 走出目錄、另一 helper、等待逾時或 Squirrel package／安裝失敗都會安全停低並保留診斷。
- Installer logs live beside updater logs under LocalAppData. A failed apply relaunches the existing app only through the validated in-root launcher/executable. · Installer log 同 updater log 一齊放 LocalAppData；套用失敗只會經已驗證 root 內 launcher／exe 重開舊 app。

## Security considerations · 安全考量

The flow never asks for administrator rights, never accepts a release URL outside the canonical GitHub path, never executes an unsigned setup without a GitHub digest that matches the locally hashed file, and never lets handoff paths select arbitrary launcher, app, setup, or log locations. Download sizes, waits, helper concurrency, retry cadence, and version components are bounded. Persistent logs contain paths and operational errors, not credentials; canonical asset URLs forbid userinfo, query strings, and fragments. · 流程唔會要求管理員權限、唔接受正式 GitHub 路徑以外 URL、冇 GitHub digest 同本機 hash 對唔上就絕不執行，handoff 路徑亦唔可以任揀 launcher／app／setup／log。下載大小、等待、helper 數量、重試同版本都有上限；log 只記路徑／操作錯誤，正式資產 URL 禁止 userinfo、query 同 fragment。

## Verification · 驗證

Run the focused no-mutation suite:

```powershell
dotnet run --project tests\ManagedReleaseContract.Tests -c Debug
```

The suite covers repository coordinates, versions, immutable Squirrel asset names, SHA-256 normalization/equality, exact stable-release selection, URL and size rejection, install/staging/log containment, portable footprint, workflow/installer/runtime wiring, stale-link removal, and preservation of WinForge-Native coordinates. Build the solution and both helper projects after changing delivery code; CI additionally owns Release-mode publication, Squirrel archive inspection, unsigned `Setup.exe` verification, and hosted release proof. · 專測覆蓋 repo、版本、Squirrel 資產名、SHA-256、穩定 release 揀選、URL／大小拒絕、安裝／staging／log 界線、可攜 footprint、workflow／installer/runtime wiring、舊連結清理，同保留 WinForge-Native 座標。改 delivery code 後要 build solution 同兩個 helper；CI 另外負責 Release 發佈、Squirrel archive 檢查、冇簽名 `Setup.exe` 驗證同 hosted release 證明。
