# WinForge Full Development Handoff

## 2026-08-17 RustDesk installer catalog recovery — final handoff · RustDesk 安裝 catalog 復原 — 最終交接

- **Scope / 範圍:** The RustDesk page now tries the exact WinGet package `RustDesk.RustDesk`, then recovers only from the precise `No package found matching input criteria.` catalog result by reading the official latest RustDesk release. The fallback accepts only the official HTTPS repository path and Windows x64 asset, checks the bounded size, `MZ` PE header, and release SHA-256, then runs the existing elevated `--silent-install` path. A configuration directory alone no longer makes RustDesk appear installed without `rustdesk.exe`. · RustDesk 頁面而家會先試準確 WinGet package `RustDesk.RustDesk`；只有準確 `No package found matching input criteria.` catalog 結果先會讀官方最新 RustDesk release。後備路徑只接受官方 HTTPS repository path 同 Windows x64 asset，會驗證有限大小、`MZ` PE header 同 release SHA-256，之後先行現有提權 `--silent-install` 路徑。淨係有設定資料夾、冇 `rustdesk.exe` 唔再當成已安裝。
- **Changed files / 改動檔案:** `Pages/RustDeskModule.xaml.cs`, `Services/RustDeskRelease.cs`, `Services/RustDeskService.cs`, `tests/RustDeskInstaller.Tests/`, `WinForge.sln`, `README.md`, `Services/ManualContent.Apps1.cs`, `docs/ROADMAP.md`, `docs/features/git-github/`, `CHANGELOG.md`, and the fresh RustDesk captures under `docs/` and `docs/wiki/images/`. · 改動包括 RustDesk page、release validator、service、專項測試、solution、README、manual、roadmap、分類文件、changelog，同 `docs/`／`docs/wiki/images/` 最新 RustDesk capture。
- **Verification / 驗證:** `tests/RustDeskInstaller.Tests` passed **5/5**. `dotnet build WinForge.sln -c Debug -p:Platform=x64 --no-restore -m:1 /nr:false` passed with **0 errors** and **1 existing `NU1903` SSH.NET advisory warning**. The repository driver published a self-contained build and captured the RustDesk page at **1558×878** on its owned off-screen desktop; the inspected capture SHA-256 is `A2BEC2774105E210961107959443DAE1FACEF7B3DFFED6FE7A8187C1CB2B726B` and is copied identically to both canonical image paths. No live RustDesk installation was performed during verification. · `tests/RustDeskInstaller.Tests` **5/5**；solution build **0 errors**，保留 **1 個現有 `NU1903` SSH.NET advisory warning。Repository driver 用自包含 build 喺自家 off-screen desktop capture RustDesk 頁面，尺寸 **1558×878**；已檢視 capture SHA-256 係 `A2BEC2774105E210961107959443DAE1FACEF7B3DFFED6FE7A8187C1CB2B726B`，兩個 canonical image path 完全相同。驗證期間冇真實安裝 RustDesk。
- **Delivery / 交付:** Implementation commit `32ad8ec3a47a649d96c69e7871e02b73dfb893d0`, documentation/changelog commit `d51e34fcf3fd281d201265214297bdb37b19ad48`, and merge commit `1dc0d0db034e625d17b5f19168639446eca1f230` are all present on `origin/main`; `git merge-base --is-ancestor` passed for both task commits. The LowLevel MCP binding was not exposed in this Slop Machine tool list, so the repository driver supplied the required off-screen visual evidence. · Implementation commit、文件／changelog commit 同 merge commit 全部已喺 `origin/main`；兩個 task commit 嘅 `git merge-base --is-ancestor` 都通過。今次 Slop Machine tool list 冇暴露 LowLevel MCP binding，所以由 repository driver 提供所需 off-screen 視覺證據。
- **Remaining boundary / 尚餘界線:** The public WinGet catalog remains outside WinForge's control. The fallback depends on the official RustDesk release API and its published asset digest; malformed, unavailable, or mismatched official metadata remains a visible install failure rather than an unverified download. · 公開 WinGet catalog 唔係 WinForge 控制範圍；後備路徑依賴官方 RustDesk release API 同已發佈 asset digest。官方 metadata 壞咗、唔得或者唔一致時，會如實顯示安裝失敗，唔會下載未驗證檔案。

## 2026-08-11 Command Palette search lane · 2026-08-11 Command Palette 搜尋工作

- **Scope / 範圍:** The isolated `codex/command-palette-search` checkout upgrades `Services/CommandPaletteWindow.cs` from a plain `TextBox` to the shared `SearchPatternBox`. It adds query-only Enter activation, real-query focus, bounded regex validation and result matching, explicit no-result/error status, language-refreshable accessible names, and the minimum inventory, source-contract, feature, wiki, Pages, README, and roadmap updates for this surface. No release files, external GitHub surfaces, or the primary checkout were changed.
- **Verification / 驗證:** `dotnet run --project tests/RegexBuilder.Tests -c Debug` passed **36/36**; `dotnet build WinForge.csproj -c Debug -p:Platform=x64 --no-restore -m:1 /nr:false` passed **0 warnings / 0 errors**; `tools/New-SearchSurfaceInventory.ps1` passed **102 controls / 83 files** with **15 integrated-core** and **66 plain-text-later**; `git diff --check` passed. The solution-level build was attempted but this isolated checkout lacks four ignored, solution-referenced generated test projects (`SupportTickets.Tests`, `ScheduledSettings.Tests`, `TotpAuthenticator.Tests`, and `OfflineDocs.Tests`), so that aggregate result is not claimed here.
- **Visual evidence / 視覺證據:** No fresh Command Palette capture is claimed. The first-run consent surface requires a user decision, and this bounded lane did not accept legal terms on the user's behalf.
- **Delivery boundary / 交付界線:** Changes remain local to the isolated checkout for the parent integration agent. No external publication or remote update was performed by this lane.

## Current 2026-08-11 Squirrel.Windows delivery and universal experience pass · 2026-08-11 Squirrel.Windows 交付同共用體驗工作

- **Scope / 範圍：** the active Inno Setup path is replaced by unsigned Squirrel.Windows packaging. The repository now carries `build.bat`, `build-installer.bat`, a self-contained x64 publish path, `Setup.exe`, `RELEASES`, the versioned full package, optional deltas, a portable archive, a provenance manifest, and a committed line counter. · 目前 Inno Setup 路徑已換成 unsigned Squirrel.Windows；repository 而家有 `build.bat`、`build-installer.bat`、自包含 x64 publish、`Setup.exe`、`RELEASES`、有版本號 full package、可選 delta、portable archive、provenance manifest 同 committed line counter。
- **Universal experience subset / 共用體驗子集：** shared settings, renamed School mode with vault-backed local unlock, emoji message control, narrator controls, `Ctrl+Shift+F` command palette default, offline changelog search/date/export, pinned-tab persistence, and in-process OTP pairing QR generation are implemented and documented. · Shared settings、改名 School mode 同 vault unlock、emoji 訊息設定、narrator、`Ctrl+Shift+F` command palette、offline changelog 搜尋／日期／export、pinned tab 持久化同本機 OTP pairing QR 已實作並有文件。
- **Local verification / 本機驗證：** the Debug solution build is **0 warnings / 0 errors**; the focused managed release contract is **26/26**; the exhaustive local harness is **39/39**; the Status Hub audit is **58/58** plus integration; workflow structure validation is green; and real built-surface captures were inspected for Settings, About/changelog, and TOTP. · Debug solution build 零 warning／零 error；managed release contract **26/26**；完整 local harness **39/39**；Status Hub **58/58** 加 integration；workflow structure validation 通過；Settings、About／changelog 同 TOTP real built surface capture 已檢視。
- **Artifact evidence / 資產證據：** hosted run [31472375758](https://github.com/Ding-Ding-Projects/WinForge/actions/runs/31472375758) produced the stable `v1.1.348` release from source `4bb276ddd4da7dfb36af2240247ba32a223b3f49`. The release contains unsigned `Setup.exe` (SHA-256 `25b34f2b0c653b2d55ab80984abb6ce07d19dfbdec1ad99ad06ec42109475a3c`), `RELEASES` (`4ba41c392cc3c1353450368505d9db5754a4c0ad20b6e08441fa12473fddd88d`), `WinForge-1.1.348-full.nupkg` (`3d1fed67c5b9a227b79446b3178e7f4e6242a8f720fec3b7694f0aa6f07ad974`), and `WinForge-portable-x64-1.1.348.zip` (`60358603aff2d43a67f47451223b14ea59f6d750d83cdd08c29c5c7059f1b12c`). Workflow duration is `00:12:04`; the release line-count table reports 5,483 tracked files, 1,053,798 total lines, and 986,646 non-blank lines. · Hosted run [31472375758](https://github.com/Ding-Ding-Projects/WinForge/actions/runs/31472375758) 由 source `4bb276ddd4da7dfb36af2240247ba32a223b3f49` 產生 stable `v1.1.348` release。Release 包含 unsigned `Setup.exe`、`RELEASES`、`WinForge-1.1.348-full.nupkg` 同 `WinForge-portable-x64-1.1.348.zip`；每項 digest、workflow `00:12:04` 同 line-count table 已由 release notes 記錄。
- **Final repair-tip evidence / 最終修正 tip 證據：** hosted run [31474585860](https://github.com/Ding-Ding-Projects/WinForge/actions/runs/31474585860) produced stable `v1.1.361` from `c1d7a1c37ad88d96dfb228427f017c38d95b936a`. It contains unsigned `Setup.exe` (SHA-256 `e1d3bfc182a077d7e79e6ede81b856e6132d219f61c69b0fa5c170495dafe558`), `RELEASES` (`675d70e2bfffeeb317d1ce0c8c617b01136f79d5ce2f12668ce78fb54892dbd5`), `WinForge-1.1.361-full.nupkg` (`d867b7ef7048ca65be63d22ba9a8d9c6d71a09fc5472f7c6d89909a10d7db0a7`), and `WinForge-portable-x64-1.1.361.zip` (`ce045482152a7abbaa2390b44379afaa4ae95fa80cafd3998d3bd7e01e02717c`). Workflow duration is `00:10:38`; the line-count table reports 5,483 tracked files, 1,053,804 total lines, and 986,651 non-blank lines; the dim-sum code name is `Fish Maw Siu Mai · 花膠燒賣`. The branch-only push trigger was exercised by this release: the Actions inventory contains one run for `c1d7a1c3` and no tag-trigger follow-up. · Hosted run [31474585860](https://github.com/Ding-Ding-Projects/WinForge/actions/runs/31474585860) 由 `c1d7a1c37ad88d96dfb228427f017c38d95b936a` 產生 stable `v1.1.361`；四項 unsigned Squirrel.Windows asset、`00:10:38`、line-count table、`Fish Maw Siu Mai · 花膠燒賣` 同 no tag-trigger follow-up 已驗證。
- **Remaining universal contracts / 尚餘共用合約：** startup dim-sum image surprise, complete regex/menu coverage, full tab locking/docking/bulk actions, Word-depth appearance editing and locks, complete offline documentation browser, destructive super-confirmation, support tickets, and the complete multi-entry authenticator import/list flow remain explicitly documented as unimplemented. · Startup dim-sum image surprise、完整 regex／menu coverage、tab locking／docking／bulk actions、Word-depth appearance editor 同 locks、完整 offline documentation browser、destructive super-confirmation、support tickets 同 complete multi-entry authenticator import／list flow 仍然如實記錄為未實作。
- **External evidence boundary / 外部證據界線：** the WinForge task issue is #16 and the rolling General Discussion is #17. The green GitHub Actions run, stable release record, Pages deployment, remote `main` ancestry proof, and authorized cleanup are verified. GitHub Wiki synchronization remains externally blocked: the GitHub API returned `404 Not Found` for `repos/Ding-Ding-Projects/WinForge/wiki/pages`, so the tracked `docs/wiki/` mirror and Pages source were updated instead. The permitted low-level headless binding was unavailable; the repository driver provided inspected off-screen captures for Settings, About/changelog, and the TOTP registration surface, while the generated-QR state was not promoted as visual evidence. · WinForge task issue 係 #16，rolling General Discussion 係 #17。綠色 GitHub Actions run、stable release record、Pages deployment、remote `main` ancestry proof 同 authorized cleanup 已驗證。GitHub Wiki API 回覆 `404 Not Found`，所以 wiki 受限；已更新 tracked `docs/wiki/` mirror 同 Pages source。許可嘅 low-level headless binding 不可用；repo driver 檢視咗 Settings、About／changelog 同 TOTP 登記介面，但冇將 generated QR state 冒充成視覺證據。
- **Git state and preserved release records / Git 狀態同保留 release 記錄：** after ancestry proof, the repository has one primary checkout, one local `main`, one remote `main`, no linked worktrees, no stashes, and no task branches. The `v1.1.344`–`v1.1.362` records created during this pass remain immutable audit history; `v1.1.361` is the verified repair-tip stable record, while later records from already-started historical runs are preserved without rewriting. · 完成 ancestry proof 後，repository 只剩一個 primary checkout、一個 local `main`、一個 remote `main`，冇 linked worktree、stash 或 task branch。今次工作期間建立嘅 `v1.1.344`–`v1.1.362` 記錄全部保留做 immutable audit history；`v1.1.361` 係已驗證嘅 repair-tip stable record，之前已開始嘅 historical run 產生嘅後續 record 亦唔會改寫。

## 2026-07-24 roadmap-completion stop checkpoint · 路線圖補完停止 checkpoint

- **Scope / 範圍：** task was "continue and finish all roadmaps, test, and leave no local worktrees once merged". User then requested an immediate stop + handoff + push. This entry records the verified baseline and the read-only audit findings gathered before the stop. · 任務係「繼續完成所有路線圖、測試、合併後唔留 worktree」；用戶要求即時停止、交接、push。呢度記低已驗證基線同停止前嘅唯讀審核結果。
- **Verified baseline / 已驗證基線：** `dotnet build WinForge.sln -c Debug -p:Platform=x64` finished with **0 warnings / 0 errors** (3:06); `dotnet run --project tests/ReactorSim.Tests -c Debug` passed **65/65** exit 0; `Test-WinForgeXamlLiteralSafety.ps1` passed (17 protected ToggleSwitch defaults, 2 IsChecked, 10 NumberBox). Local `main` was fast-forwarded to `origin/main` `e696c1b4d` (site-data auto-regen); working tree clean; **zero** extra worktrees, branches, or stashes. · solution build 零 warning／零 error；reactor **65/65**；XAML safety 通過；本機 main 已快進到 `e696c1b4d`；工作樹乾淨；冇多餘 worktree／branch／stash。
- **Read-only audit findings (evidence-backed, not yet written to roadmap) / 唯讀審核結果（有證據，未寫入路線圖）：** of the 133 unchecked `docs/ROADMAP.md` items, these are already shipped and can be ticked with evidence in the continuation: all 12 iteration-1 Windows/Maintenance tweaks (Reserved Storage `Win11ProTweaks.cs:269`, Win32PrioritySeparation `:281`, USB selective suspend `:288`, network profile Private `:295`, Hyper-V `:299-305`, lodctr `:309`, memory compression `:315-320`, HAGS `:325`, minidump `:330`, SysMain `:339`, TdrDelay `:346`, search-index rebuild `:351`+`SystemDoctors.cs:367`); Annoyances covers Copilot/Recall/Bing/SearchHighlights/Spotlight/ClickToDo/RemoveMicrosoftCopilotApp/consumer-features/SettingsPageVisibility (`AnnoyanceTweaks.cs:20-197`); VerboseStatus (`SystemTweaks.cs:44`), LongPathsEnabled (`SystemTweaks.cs:19`), EnableLinkedConnections (`SystemTweaks.cs:51`), StartupDelayInMSec (`PerformanceTweaks.cs:67`), InitialKeyboardIndicators (`SystemTweaks.cs:113+`), HiberbootEnabled (`PerformanceTweaks.cs:31`), IrisService shell recovery (`SystemDoctors.cs:327`), SeparateProcess (`ExplorerTweaks.cs:73`); NativeUtilities service+page ship Wi-Fi passwords/scanner, SMB auditor, session logoff/disconnect, cert viewer, PDH counters, process modules, Bluetooth (`NativeUtilitiesService.cs`, `NativeUtilitiesModule.xaml.cs`); folder hash-diff exists (`DiffService.CompareFoldersAsync`, used by DiffMergeModule); spooler rescue, winsock/int-ip reset, icon-cache rebuild, sfc runner all in `SystemDoctors.cs`/MaintenanceTweaks. · 133 個未剔項入面，以上項目已交付，之後可以連證據剔走。
- **Confirmed still open (gaps to build next) / 確認未做（下一步要起嘅）：** Color Picker HSV+loupe (HSV exists only in ColorToolsModule, picker lacks loupe); Do-Not-Disturb `ToastEnabled`; `OemPreInstalledAppsEnabled`; DoH `DnsOverHttpsMode` toggle; hosts ad-block list import; OneDrive KFM un-hijacker (`KFMBlockOptIn`); persistent process-priority rules (no watcher in SystemMonitorService); WindowManager tile/cascade (no TileWindows/CascadeWindows); empty-folder cleaner; treemap visualisation; ISO create (oscdimg); network speed test; power-plan switcher UI (PowerSetActiveScheme only inside ReactorSystemLinkService); audio-with-screen recording; GIF capture; provisioned-app remove/reinstall; pointer scheme/touchpad panels; batch-rename case transforms/auto-number; take-ownership doctor; quick restore point + 24h bypass UI; boot-time display; Compatibility-Appraiser registry key; "Ask Copilot" context-verb removal; recipes redesign/undo; custom program modifier; full export/import incl. clipboard git repo; Docker config sync; docs/CLI.md; UniGetUI parity review continuation. · 以上項目確認未交付，係下一步嘅建置清單。
- **Stop boundary / 停止界線：** no source, roadmap, doc, or screenshot was modified; nothing claimed beyond the verified baseline above. Continuation should re-verify the audit list, tick the stale items with this evidence, build the confirmed-open gaps in batches (catalog tweaks first, then module features), run the full gates per batch, refresh docs/wiki/site data, capture fresh page evidence for any UI change, then merge + push and remove all temporary branches/worktrees. · 冇改任何 source／roadmap／文件／截圖；只聲稱上面已驗證嘅基線。下一步要先覆核審核清單、按證據剔走過時項、分批補建確認缺口（先 catalog 後模組）、逐批跑完整 gate、更新文件同 site data、UI 改動要补新截圖，最後 merge＋push 同清走全部臨時 branch／worktree。

## WIP 2026-07-24 managed delivery alignment checkpoint · 正式發佈對齊 WIP 檢查點

- **Implemented but not integrated / 已實作但未整合：** a shared `ManagedReleaseContract` now binds `Ding-Ding-Projects/WinForge`, the `v1.1.x` version line, exact installer/portable names, GitHub SHA-256 metadata, canonical asset URLs, runtime/portable footprint, and update path boundaries across the app, updater, launcher, Inno script, and release workflow. Current-repository documentation links were migrated while the independent `codingmachineedge/WinForge-Native` coordinates were preserved. · 共用合約已綁實正式 repo、版本線、資產名、digest、URL、runtime／可攜 footprint 同更新路徑界線；現行文件連結已遷移，獨立 Native repo 座標保留。
- **Verification / 驗證：** `ManagedReleaseContract.Tests` passes **23/23**; launcher, updater, and main WinUI project Debug builds each complete with **0 warnings / 0 errors**. The workflow/installer were statically covered, but no local Inno compilation, portable archive build, full solution/aggregate suite, hosted workflow, or release is claimed. · 專測 **23/23**，launcher、updater 同主 WinUI project build 全部零 warning／零 error；未聲稱本機 Inno／ZIP、完整 solution／aggregate、hosted workflow 或 release。
- **Stop boundary / 停止界線：** work stopped at the user's request before README/ROADMAP/home indexes and final completion documentation were reconciled. The branch is a recoverable WIP checkpoint for parent coordination; it must not be called roadmap-complete until those records, full validation, hosted release proof, and final main integration finish. · 按用戶要求即時停止；README／ROADMAP／首頁索引同最終文件未對帳。呢個只係可恢復 WIP，未完成完整驗證、hosted release 證明同 main 整合前唔可以當路線圖完成。

## Current 2026-07-24 guided Windows/System and Maintenance completion — verified branch · 2026-07-24 Windows／System 同 Maintenance 補完 — 分支驗證

- **Scope and outcome / 範圍同結果：** System Doctors closes all three audited Windows/System gaps and five Maintenance gaps: Storage Sense policy, complete live Filter Keys, DISM association templates, bounded Update pause/resume, backup-gated driver rollback/restore, broad read-only Autoruns audit, irreversible ResetBase, and selected Store-app repair. Combined with Browser and Media on main, the strict matrix is **104/115 shipped** with **11 factual gaps**. · 「系統醫生」補齊 Windows／System 三項同 Maintenance 五項；連同 main 嘅 Browser／Media，matrix 係 **104/115**，如實保留 **11** 個缺口。
- **Safety / 安全：** inputs are bounded and validated before registry/process/PowerShell boundaries; external tools receive real argument vectors; elevation fails closed; rollback requires the exact in-session backup and never adds force/reboot; destructive/irreversible paths require explicit decisions. The focused harness is pure and mutates no host state. · 輸入先驗證、process 用真參數清單、提權 fail closed；回復要準確即場備份兼冇 force／reboot，破壞／不可逆操作有明確閘；專測唔改 host。
- **Verification / 驗證：** **22/22** focused contracts, **104/115 + 11** roadmap gate, XAML safety, detailed source audit (**337 XAML**, **2,918/2,918 handlers**, **1,961/1,961 direct actions**, zero lifecycle/actionable findings), exact 11-project x64 build (**0 warnings / 0 errors**), repeated successful self-contained publishes, and real site data (**322 modules / 22 categories / 1,217 features / 2,326 wiki pages**). · 專測、roadmap、XAML、source audit、完整 build、publish 同 site data 全過。
- **Visual evidence / 視覺證據：** final combined binary captured at normal 1049×646 (`70A06EFD3CDD87EE8AC9A02F361083BA755E2EB28C844EFB0BA50E56C9ED632C`), narrow 760×720 (`EA7F865C8309FDFC59CE78D07606C4CEE6A29C8763CE6CDA3947425CAB5EAA32`), expanded Storage Sense (`52141DF041D14766C2CC2209B8BC73439B254E54CC469FE5A034D9044A08F0BB`), and expanded ResetBase warning (`05C26B2DBC1630D9C4927D84F93B24422D1CDA5E8826A48202AD16A7E3871233`). Frames were inspected for contrast, wrapping, clipping, and overlap. No action button or OS mutation ran; final PID `19736` / HWND `96273438` was terminated gracefully, the desktop returned zero windows, and every dedicated LowLevel desktop closed. · 四張最終圖已檢視，冇撳 action／改系統；最終指定 PID／HWND 安全關閉，desktop 零視窗，全部專用 desktop 已關閉。
- **Git state / Git 狀態：** implementation checkpoint `f5be71aa`, current-main merge `2c4dfbdb`, and final safety/docs/evidence commit `5cd5b83219a40f3a477f09b1efcaf77d660e7ce2` preserve Browser and Media from main `664fd4b453`. This memory commit follows that audited source tip; delivery remains on `codex/system-maintenance-gaps` for parent integration, and this lane does not merge `main`. · 功能、main 合併同最終安全／文件／證據 commit 如上；呢個 memory commit 跟住已審核 source tip，分支保留俾 parent 整合，呢條 lane 唔 merge main。

## 2026-07-24 Developer, HA, and Archives stop checkpoint · 開發、HA 同壓縮檔停止 checkpoint

- Implemented the six Developer & Terminal gaps, the exact Home Assistant `check_config` restart gate, and four Archives gaps (arbitrary in-archive masks, integrity-before-source-removal, include/exclude masks, and the complete NTFS time/access switch set). · 已實作開發六項、HA 準確驗證重啟安全閘，同壓縮檔四項缺口。
- Stop-time evidence: focused contracts **44/44**; roadmap **107/115** on this branch; XAML safety pass; source audit **2,919/2,919 handlers**, **1,962/1,962 direct actions**, zero lifecycle/actionable findings; solution build **0 warnings / 0 errors**; self-contained publish exit 0. · 停止前所有離線驗證如實通過。
- Inspected LowLevel frames cover Developer 800×720, Archives 852×720 and 852×1200, and Home Assistant 852×900. No live mutation/network/restart action ran. Every exact owned PID exited and every dedicated desktop closed at zero windows. Local ignored evidence was not promoted to canonical docs before the stop. · 已檢視三頁新鮮證據，冇真實副作用，所有 PID／desktop 已清零；停止前未及升格正式圖。
- Stop boundary: branch base `664fd4b453c4c10196891d6dac63a2d646985b3b`; fetched `origin/main` `b0828ada5d0ac501fc1f33f42c3135961675517d`. Canonical driver captures, site-data regeneration, current-main integration/revalidation, and hosted completion remain unfinished. · 正式截圖、site data、current-main 合併重驗同 hosted 完成仍未做，唔可以當已完成。

## WIP checkpoint: central notification centre — 2026-07-24 · 中央通知中心 WIP 交接

- **Implemented / 已實作：** bounded four-card bottom-right host, 200-entry local history, severity-aware lifetimes, stable keyed replacement, stale-timer protection, unread/clear behavior, bounded actions and safe HTTP(S) links, three-language rerendering, accessible live regions, and persistence opt-out. App-update and package-manager notifications use the shared bus; broader page-local InfoBar/dialog migration remains open.
- **Verified / 已驗證：** `NotificationCenter.Tests` passes **16/16**; XAML safety passes; the detailed source audit resolves **2,918/2,918** handlers and **1,961/1,961** direct actions with zero mismatch/markers; the full 11-project solution build completed with **0 warnings / 0 errors**; and self-contained publish passed.
- **Visual disposition / 畫面處置：** fresh LowLevel normal/narrow/history captures were queued but not reached before the user-requested stop. No screenshot or no-clipping claim is made, so this branch is a factual WIP checkpoint and must not be merged as visually complete.
- **Continuation / 繼續做：** merge current `origin/main`, run fresh normal/narrow/history shell captures, fix any overlap/clipping found, regenerate site data, extend migration beyond update/package producers, update this record, then perform final build/tests before integration.

## Current 2026-07-24 Browser Control roadmap completion — verified branch · 2026-07-24 瀏覽器控制路線圖完成 — 分支驗證

- **Scope and outcome / 範圍同結果：** the Browser Control category now embeds an accessible, bilingual parameterized workbench above its existing quick-action catalog. It closes all eleven audited gaps: configurable app/kiosk URL launch, real `Local State` profile selection, installed-PWA discovery/launch, flags and policy pages, browser-closed selected-profile cache cleanup, isolated proxy/bypass and throwaway sessions, validated feature enable/disable, loopback remote debugging, and exact-ID winget install/update. The evidence matrix advances Browser Control from 3/14 to **14/14**, and the eight-section aggregate from 74/115 to **85/115** with 30 factual gaps retained elsewhere. · 瀏覽器控制分類而家喺原有快捷目錄上面加入無障礙雙語參數工作台，十一個審核缺口全部補齊；Browser Control 由 3/14 變成 **14/14**，八節總數由 74/115 變成 **85/115**，其餘 30 個真實缺口繼續如實保留。
- **Safety and lifecycle / 安全同 lifecycle：** browser values leave WinForge only through discrete `ProcessStartInfo.ArgumentList` entries. HTTP(S), proxy/bypass, feature, profile, PWA, and port inputs are bounded; embedded URL/proxy credentials are rejected and URL/proxy fields are session-only. Browser launches fail closed while WinForge is elevated. GUID-scoped sessions reject reparse-point roots, bind debugging to `127.0.0.1`, track owned process exit, and retry contained cleanup. Cache deletion requires a decision, a fully closed selected browser, profile containment, and reparse-point rejection, and touches only `Cache` plus `Code Cache`. · 全部值只經獨立 `ArgumentList` 參數離開 WinForge；輸入有界，內嵌憑證被拒絕，網址／Proxy 只留今次 session。WinForge 提權時瀏覽器 fail closed；GUID session、防 reparse、loopback 除錯、自家 process cleanup、明確確認同只刪兩個快取資料夾嘅界線全部已落實。
- **Verification / 驗證：** `BrowserControl.Tests` passes **23/23** disposable-fixture contracts. The final post-Regex x64 `WinForge.sln` build completes in **6:43.04** with **0 warnings / 0 errors** across nine projects. `Test-RoadmapCoreAudit.ps1` passes Browser 14/14 and aggregate 85/115; XAML literal safety passes; the detailed audit resolves **2,898/2,898** handlers and **1,941/1,941** direct actions across 337 XAML files, with 322 feature docs, 1,924 button docs, zero language-subscription mismatches, and zero actionable markers. Full merged app/site generation and the final docs-only merge refreshes exit 0; the combined payload writes **322 modules, 22 categories, 1,217 features, and 2,304 wiki pages**. · 專項 **23/23**、最終 post-Regex solution build 零 warning／零 error、roadmap gate、XAML safety、完整 source audit 同 site-data refresh 全過；handler／direct action 全 resolve，零 mismatch／marker。
- **Visual evidence and side-effect disposition / 視覺證據同副作用處置：** the process-owned driver rendered the live Browser route on dedicated LowLevel desktops. Inspected canonical evidence is 1033×637, SHA-256 `400AF4B89FE16B6A22023BE1259442D8D1A0BF88C39C0445C9A7E7DFE161FB3C`; inspected narrow evidence is 784×691, SHA-256 `BDB186204A24F1AFFF927F1347E315A77FBCCD218D8B09D7423C7E4282DF94B3`. Bilingual labels wrap without clipping/overlap and controls expose 48-pixel minimum targets plus automation names. Each desktop reached zero retained windows and closed successfully. No real browser launch, cache deletion, package mutation, or remote-debug session was invoked. · 正式同窄版圖已檢視，雙語換行冇裁切／重疊，控制最少 48 像素兼有 automation name；每個專用 desktop 都零殘留視窗後成功關閉，亦冇真係開瀏覽器、刪快取、改套件或者開遠端除錯。
- **Delivery boundary / 交付界線：** feature commit `24447657f` and all current-main merges through the final guided-Regex site baseline `00880308` are retained on `codex/browser-control-roadmap`, with fetched `origin/main` `1cc761492` as an ancestor and zero commits behind at final validation. This final factual memory commit follows on the same bounded branch. The coordinating parent owns final `main` integration, remote-main proof, hosted workflow/release observation, GitHub Discussion, and cleanup; this subtask intentionally does not merge or mutate `main`. · 功能 commit 同直至 Regex 最終 site baseline `00880308` 嘅最新 main merge 全部保留喺專用 branch；最後驗證時 `origin/main` `1cc761492` 已係祖先兼 behind 0。呢個最終 factual memory commit 跟住同一 branch；最終 main 整合、remote proof、workflow／release、Discussion 同清理由統籌 parent 負責，呢個子任務刻意唔掂 main。

## Current 2026-07-24 independent funny-level settings — verified branch · 2026-07-24 英粵分開搞笑等級 — 分支驗證完成

- **Scope and behavior / 範圍同行為：** `FunnyLevelSettings` persists independent English and Cantonese values from 1–5 (`tone.englishFunnyLevel`, default 2; `tone.cantoneseFunnyLevel`, default 3). Settings exposes exact-step accessible sliders, visible value summaries, and a polite live preview; import reloads the cached values. `PlayfulText` plus the separate `PlayfulCopy` catalog makes tone variation explicit, and the Dashboard hero updates live in all three language modes. · `FunnyLevelSettings` 會分開保存英文 1–5 級（預設 2）同粵語 1–5 級（預設 3）。設定頁有準確步進無障礙 slider、可見數值摘要同 polite 即時預覽；匯入亦會重載 cache。`PlayfulText` 同獨立 `PlayfulCopy` catalog 將可變語氣邊界寫清楚，Dashboard 首頁句子會喺三種語言模式即時更新。
- **Safety, persistence, and lifecycle / 安全、持久化同 lifecycle：** only explicitly authored non-operational copy can use the funny-level path. Errors, security/financial copy, destructive actions, accessibility wording, and operational instructions remain ordinary exact `LocalizedText`. Invalid stored values fall back independently, out-of-range writes fail before persistence, and no-op writes raise no duplicate event. Dashboard and Settings use balanced named subscriptions with zero audit mismatches; theme-aware surfaces fix the dark-mode white-on-light and black-secondary-text defects found during live inspection. · 只有明確寫好嘅非操作文案可以用搞笑等級；錯誤、安全／金融、破壞性操作、無障礙同操作指示繼續用準確 `LocalizedText`。無效值各自回退，越界寫入喺保存前拒絕，同值唔會重複發 event。Dashboard／Settings subscription 已平衡，audit 零 mismatch；即時檢視發現嘅 dark mode 白字淺底同黑色次要文字問題亦已用 theme-aware surface 修好。
- **Verification / 驗證：** `FunnyLevelSettings.Tests` passes **6/6** across malformed/default values, independent persistence, all three language modes, import reload, bounds, and unchanged safety-sensitive localization. Exact `dotnet build WinForge.sln -c Debug -p:Platform=x64 -p:UseSharedCompilation=false -m:1` completes in 8:02 with **0 warnings / 0 errors**. The combined self-contained site generation/publish exits 0 and writes **322 modules, 22 categories, 1,217 features, and 2,298 wiki pages**. XAML literal safety passes; the detailed source audit resolves **2,893/2,893** handlers and **1,937/1,937** direct actions across 337 XAML files, with 322 feature docs, 1,920 button docs, zero language-subscription mismatches, and zero actionable markers. · 專項測試 **6/6**；完整 solution build 8:02 零 warning／零 error；合併後 self-contained publish／site generation 成功；XAML safety 同詳細 source audit 全過，handler／direct action 全 resolve，零 subscription mismatch／actionable marker。
- **Visual evidence / 視覺證據：** the exact task binary ran on dedicated LowLevel desktop `WinForgeFunnyLevel20260724`. Inspected captures are default 1049×646 (`95D708E05CADE15AC8094BE1F5E6151CC26400A1A67CACEE06F250CD61EE976E`), independent live English 5 / Cantonese 1 at 1049×820 (`FAE92C3345D99E2999AFB79DBF92E4B12E20C1E96AB9FB32F75E19C3E3A6F100`), and narrow 720×646 (`462FCE6FBD03C1F46093D18527530FA31CAAFD74C87E968D7E8A3670E64ABC9A`). UI Automation changed the real sliders and visible preview independently; the original persisted 2 / 3 values were confirmed restored before owned PID 18092 closed. LowLevel then proved zero windows and closed the desktop. A final repository-driver `settings` launch-only check ran inside fresh LowLevel desktop `WinForgeFunnyDriver20260724`; the owned process exited, zero windows/processes remained, and the desktop closed. · 正式 default、英 5／粵 1 即時控制同 720 像素窄畫面圖全部已檢視；UI Automation 真正改動兩個 slider／預覽，關閉 PID 18092 前已確認原本 2／3 持久值還原。兩個專用 desktop 最後都零視窗、冇殘留 process 並已關閉。
- **Delivery boundary / 交付界線：** feature commit `3718f27aeef0e3e30d60aa6dc43e16032228b25b`, UI/docs/evidence commit `b0fd49cc`, and latest-main merge `0a0fc4a7e` are retained on `codex/funny-level-settings`, with fetched `origin/main` `a7fba982cbc1184b704bdab89b035672c4eaeed0` as an ancestor. This final lifecycle/handoff record follows on the same bounded branch; the coordinating parent owns final `main` integration, remote-main proof, hosted workflow/release observation, GitHub Discussion, and cleanup. · 功能 commit、UI／文件／證據 commit 同最新 main merge 全部保留喺專用 branch，最新 `origin/main` 係 ancestor。呢段最後 lifecycle／handoff 記錄跟住同一 branch；最終 main 整合、remote-main proof、hosted workflow／release、GitHub Discussion 同清理由統籌 parent 負責。

## Current 2026-07-24 Media roadmap completion — branch handoff · 2026-07-24 Media 路線圖補完 — 分支交接

- **Scope / 範圍：** all eleven audited Media gaps are now implemented through reachable bilingual WinUI controls and the pure `MediaWorkflowExecutor`: measured two-pass EBU R128, start/end/internal silence trimming, two-pass vidstab, cropdetect-to-crop, ordered concat-demuxer stream copy, hardware-probed `h264_nvenc`/`hevc_nvenc`/`av1_nvenc`, duration-aware two-pass target-size x264, SRT/ASS burn-in and soft mux, ffprobe chapter read/split, bounded HEIC/HEIF/JXL batch conversion, and EXIF/GPS/XMP metadata stripping. · 11 個 Media 審核缺口而家全部有可達雙語 WinUI 控制同純 `MediaWorkflowExecutor` 實作。
- **Safety / 安全：** every path is one `ProcessStartInfo.ArgumentList` item, filter and concat syntaxes receive dedicated escaping, outputs are staged beside the destination and promoted only after success/existence checks, existing destinations survive failure/cancellation, batches are capped at 500, chapters at 200, and GUID-owned transforms/lists/passlogs are cleaned in `finally`. Cancel uses the shared runner's bounded process-tree termination. Internal-gap silence removal rejects video inputs so collapsing audio cannot desynchronize picture, and crop detection starts at frame zero so short clips are supported. · 每條路徑都係獨立 argument；輸出成功先升格；失敗／取消保留舊檔；批次／章節有上限；自家 transform／list／passlog 必定清理。中間靜音清理會拒絕影片，避免畫面聲音甩 sync；crop 偵測由第一格開始，短片都支援。
- **Accessibility/localization / 無障礙同本地化：** four vertically stacked cards keep bilingual labels wrapping at narrow widths; workflow buttons stretch with 40-pixel minimum targets, inputs expose bilingual automation names, NumberBox defaults are assigned after `InitializeComponent`, and the named language subscription remains balanced. · 四張直向卡喺窄畫面保持雙語換行，按鈕全闊最少 40px，輸入有雙語 automation name，NumberBox 預設喺 code-behind 設定，語言 lifecycle 平衡。
- **Verification / 驗證：** `MediaWorkflowCore.Tests` passes **17/17**; the exact-source `WinForge.sln` x64 Debug build exits 0 with **0 warnings / 0 errors**; XAML literal safety passes; the detailed source audit resolves **2,913/2,913** declared handlers and **1,957/1,957** direct actions across 337 XAML files, with 322 feature docs, 1,940 button docs, and zero lifecycle mismatches/markers. After Browser and Media integration, the core roadmap audit is **96/115 shipped, 19 retained gaps**; the final Pages refresh writes 322 modules, 1,217 features, and 2,318 wiki pages. · 專項 **17/17**、完整 solution build 零 warning／零 error、XAML safety、handler／direct action 全 resolve；Browser 同 Media 整合後核心 roadmap 係 **96/115 已交付、19 個缺口保留**，Pages bundle 亦已同步。
- **Visual evidence / 視覺證據：** LowLevel MCP headless tools were not callable in this session. The repository driver self-contained-published `--page media`, used its DEBUG live WinUI visual-tree capture, and produced an inspected 1033×637 image with clean bilingual wrapping, contained card geometry, 40-pixel controls, and scroll-not-clip behavior. Both canonical images now have SHA-256 `F89886CE200DA522E8C956B67B363A847E8E9DC0AC2926DFF382E9E52B870900`; no raw desktop capture was used. · LowLevel 工具今次不可呼叫；repo driver 用自家 live visual tree 擷取 1033×637，已檢查雙語換行、卡片幾何、控制尺寸同捲動；冇用 raw desktop capture。
- **Delivery boundary / 交付界線：** implementation, tests, documentation, generated references, and visual evidence are recorded in commit `41870b04a`; the isolated branch was pushed and exact-tip-proved for the coordinating parent. Main integration and cleanup are recorded in the newer combined handoff entry. · 實作、測試、文件、生成 reference 同視覺證據已記錄喺 `41870b04a`；專用 branch 已 push 同證明 exact tip，main 整合同清理會記喺更新嘅合併交接記錄。

## Current 2026-07-24 Screen Recorder lifecycle repair — branch verification · 2026-07-24 螢幕錄影 lifecycle 修復 — 分支驗證

- **Base reproduction / Base 重現：** branch `codex/fix-screen-recorder-lifecycle` was created from fetched `origin/main` `ec7c4bcb89523c2efc353a15d3b981fcada946f8`. The focused unchanged `ScreenRecorderLifecycle.Tests` fixture initially passed, but the normal 29-project aggregate runner failed only that fixture with `Stop did not report the fixture as saved`; a captured-output `--no-build` stress loop then failed **5/12** unchanged runs. · 分支由已 fetch 嘅 `origin/main` `ec7c4bcb8` 建立。未改 fixture focused 最初通過，但正常 29-project aggregate 只係佢報 `not saved`；captured-output stress loop 同一個未改 fixture 有 **5/12** 失敗。
- **Root cause and fix / 根因同修復：** production inefficiently decoded stderr and dispatched an empty callback per progress line, so it now bulk-copies raw bytes to `Stream.Null`. The aggregate failure itself remained reproducible under heavier load because the synthetic child ran 10,000 separate `cmd.exe echo` commands before reading `q`, measuring shell scheduling inside the eight-second encoder deadline. It now self-hosts and emits the same 10,000 newline-rich records in one efficient raw write. Production command/grace/forced deadlines, cleanup ownership, and truthful failure mapping remain unchanged. · Production 舊版逐行解碼 stderr 再派空 callback，依家整批複製 raw byte 去 `Stream.Null`。但 aggregate 根因仲包括 synthetic child 喺讀 `q` 前逐個跑 10,000 次 `cmd.exe echo`，將 shell 排程誤計入八秒 encoder deadline；依家 self-host 並用一次高效 raw write 寫同樣 10,000 行。Production 時限、ownership 同如實失敗對應全部不變。
- **Verification / 驗證：** process-free recorder/registry seam **10/10**; deterministic self-hosted Windows fixture **1/1**; captured-output stress improved from **7/12 pass** on the original base fixture to **12/12 pass** under concurrent host load. The final merged x64 solution build completed in 2:15 with **0 warnings / 0 errors**. XAML literal safety passed; the detailed audit resolved **2,893/2,893** handlers and **1,937/1,937** direct actions across 337 XAML files with zero mismatches/markers; the new roadmap verifier also passed **74/115 + 41 factual gaps**. The exact aggregate that failed base passes **all 31 projects** in 15:16, including Recorder **10/10**, Screen Recorder **1/1**, Reactor **65/65**, and Package Manager **30/30**; latest main changed only docs/site data plus the static roadmap verifier, leaving those aggregate production/test inputs byte-identical. · Process-free seam **10/10**、deterministic self-hosted fixture **1/1**；stress 由 base **7/12** 變成 concurrent load **12/12**。最終 merged x64 build 2:15 零 warning／零 error；XAML safety、2,893/2,893 handler、1,937/1,937 direct action、roadmap 74/115 + 41 gaps 全過；原本失敗 aggregate 15:16 **31 個 project 全過**。最新 main 只改 docs／site data／靜態 verifier，aggregate production／test input byte-identical。
- **Visual disposition / 視覺處置：** no XAML, UI state, localization, accessibility, or layout changed. No screenshot was required or replaced, and the existing recorder image is not claimed as lifecycle evidence. · 冇改 XAML、UI state、本地化、無障礙或版面；毋須亦冇換截圖，既有 recorder 圖唔會冒充 lifecycle 證據。
- **Delivery boundary / 交付界線：** production/docs commit `7498eb0cf`, deterministic-fixture/evidence commit `623f9fbb6`, and latest-main merge `6c8da26f2` preserve `origin/main` `2a16b67cbe67e0e6488cc9c241176098570031cf` as an ancestor. This final memory commit follows on `codex/fix-screen-recorder-lifecycle`; the branch is pushed and exact-tip-proved for the coordinating agent, while `main` merge, remote-main proof, release observation, and cleanup remain deliberately outside this subtask. · Production／docs commit `7498eb0cf`、deterministic fixture／evidence `623f9fbb6` 同最新 main merge `6c8da26f2` 已保留 `origin/main` `2a16b67cb` 做祖先；最終 memory commit 跟住喺專用分支，會 push 同做 exact-tip proof 交畀統籌 agent，而 main merge／remote-main proof／release／cleanup 按分工留畀上層。

## Current 2026-07-24 small-module roadmap reconciliation · 2026-07-24 細型模組路線圖對帳

- Six stale roadmap rows are evidence-backed shipped: Hosts block/redirect, Cloudflare/Google/automatic DNS, WSL distro management, generated Windows Sandbox launch, persisted global hotkey macros, and world-clock/timezone conversion. Color Picker remains honestly open because HSV and the magnifying loupe are absent despite working global click sampling and HEX/RGB/HSL copy. · 六個過時 roadmap 項目已按證據確認交付：Hosts 封鎖／重新導向、Cloudflare／Google／自動 DNS、WSL 發行版管理、生成 Windows Sandbox 啟動、持久全域熱鍵巨集、世界時鐘／時區換算。Color Picker 雖然已有全螢幕點擊取色同 HEX／RGB／HSL 複製，但 HSV 同放大鏡仍然未有，所以如實保持未完成。
- Launch-only checks passed for `hosts`, `wsl`, `colorpicker`, `hotkeys`, and `worldclock`; source audit resolved **2,893/2,893** handlers and **1,937/1,937** direct actions with zero lifecycle mismatches/actionable markers, and XAML literal safety passed. This is a documentation-status correction with no visual-tree change, so existing canonical screenshots remain applicable. · 五個 deep link launch-only 全過；source audit handler／direct action 全 resolve，零 lifecycle mismatch／actionable marker，XAML literal safety 亦通過。今次只係文件狀態修正，visual tree 冇改，所以現有正式截圖仍然適用。

## Current 2026-07-24 core roadmap capability reconciliation — strict evidence branch · 2026-07-24 核心路線圖功能對帳 — 嚴格證據分支

- **Scope and outcome / 範圍同結果：** eight stale `docs/ROADMAP.md` sections were reconciled against reachable controls, their handler/catalog bindings, real service/registry/command mechanisms, and generated documentation. The exact result is **74/115 shipped** and **41 intentionally unchecked**: Windows 11 10/13, ViveTool 15/15, Media 4/15, Maintenance 10/15, Dev & Terminal 9/15, Home Assistant 13/14, Archives 10/14, and Browser Control 3/14. The categorized source ledger is `docs/audits/roadmap-core-capability-audit-2026-07-24.md`; `tools/Test-RoadmapCoreAudit.ps1` prevents title/status swaps as well as count drift. · 八個過時 `docs/ROADMAP.md` 章節已同可達控制、handler／catalog binding、真實 service／registry／command 機制同生成文件逐項核對；準確結果係 **74/115 已交付**、**41 項刻意保留未剔選**。分類原始碼證據喺 audit 文件，專項 test 會同時防止數量漂移同標題／狀態偷換。
- **Conservative boundary / 保守界線：** Browser app mode, kiosk, and proxy remain gaps because their fixed example values do not provide the advertised configurable workflows. The Cloudflare quick tunnel is counted because its reachable action launches a real `cloudflared tunnel --url http://localhost:8080` share, while broader URL selection is not claimed. Home Assistant check-config and restart stay unchecked as a combined safety workflow because restart does not require a successful check. Similar partials are itemized factually in the audit. · Browser app mode／kiosk／proxy 嘅固定示例冇提供所聲稱嘅可設定流程，所以繼續留空；Cloudflare quick tunnel 就有可達操作真係執行 `cloudflared tunnel --url http://localhost:8080` 分享，因此只按呢個實際範圍計已交付，唔聲稱可揀任意 URL。Home Assistant 驗證同重啟亦未有強制安全閘，所有類似部分實作都喺 audit 如實列明。
- **Adversarial correction / 對抗式修正：** all 43 Media, Archives, and Browser Control dispositions were compared again with the strict-review findings; no classification changed. The four checked Media notes now mirror the shipped handlers exactly. In particular, animated WebP uses `fps=15`, `scale=480`, `-c:v libwebp`, and `-loop 0`, with no explicit quality value. · 已重新按嚴格 findings 核對 Media、Archives 同 Browser Control 全部 43 項，分類冇改；四個已剔選 Media 註解而家準確跟 shipped handler。動態 WebP 係 `fps=15`、`scale=480`、`-c:v libwebp`、`-loop 0`，冇明確 quality 參數。
- **Verification / 驗證：** the focused contract passes with the exact **74/115 + 41-gap** matrix. The post-rebase source audit reports **337 XAML files**, **2,893/2,893 resolved handlers**, **1,937/1,937 resolved direct actions**, **322 generated feature docs**, **1,920 generated button docs**, zero language-subscription mismatches, and zero actionable markers. XAML literal safety passes. Full self-contained site generation/publish exits 0 and writes **322 modules, 22 categories, 1,217 features, and 2,296 wiki pages** from the rebased app plus authored wiki. `git diff --check` passes. · 專項 matrix、完整 source audit、XAML safety、self-contained site generation／publish 同 whitespace check 全過；handler／direct action 全部 resolve，亦冇語言 subscription mismatch 或 actionable marker。
- **Visual evidence / 視覺證據：** this is documentation plus a static verifier only; no WinUI control or layout changed. No screenshot was created, replaced, or claimed, and visual disposition is **not applicable**. · 今次只改文件同靜態驗證器，冇改 WinUI 控制／版面；冇建立、替換或聲稱有新截圖，視覺處置係 **不適用**。
- **Delivery boundary / 交付界線：** source/audit commit `632d0e551383c143908cfc65e25fa4d60f937715` is based on coordinating baseline `4af5e60e9d41f258f2d4697b1f0383138c8d1642`; it was pushed and proved byte-identical at `origin/codex/roadmap-reconcile-core`, with the baseline as its ancestor and all expected audit files present in the remote tree. The adversarial correction tip `a5ba497fb49aa9b93a4c82ba9a4134043008c686` is likewise remotely proved; final `main` integration and cleanup are performed by the coordinating parent. · 原始碼／audit commit `632d0e551` 以統籌基線 `4af5e60e9` 為祖先，已 push 並證明同 remote branch 完全一致，預期 audit 檔案亦全部喺 remote tree；對抗式修正 tip `a5ba497fb` 亦完成 remote proof，最終 `main` 整合同清理由統籌 parent 執行。

## Current 2026-07-24 preserved package-stash reconciliation — combined hardened branch · 2026-07-24 已保存套件 stash 對帳 — 合併強化分支

- **Scope and disposition / 範圍同處置：** preserved commits `5cc3aa712f9e326dd8d9ae0bdd4c16d8771e1cb6` (`codex-preserve-unrelated`, ten files, +987/−217 against parent `27f343be170c43675e4a97f3de152eafb6c99e20`) and `181fc231c93b2533392344a405cb18750b4eaa48` (`codex-temp-powertoys`, six files) were first reviewed read-only. Current in-app implementations already retain useful scheduler/coordinator, schema/solution registration, engine discovery/update, bundle/source, configured executable/PowerShell host, WinGet detection, PowerToys discoverability, and provenance intent. Malformed or stale bodies and the external UniGetUI launcher, credential-in-URL, strip-only sanitization, fixed builders, and automatic source-trust paths remain rejected. After the safe union was merged and remote-proved, the two exact redundant stash refs were revalidated by object ID and dropped; neither stale patch was ever applied or popped. · 兩份保留 commit 先逐檔只讀審核。現行 app 實作已保留有用排程／coordinator、schema／solution 註冊、引擎搜尋／更新、清單／來源、自訂 executable／PowerShell host、WinGet 探測、PowerToys discoverability 同來源證明；壞咗／過時 body、外部 launcher、URL 認證、只刪字元 sanitization、固定 builder 同自動信任來源繼續拒絕。安全 union 合併並完成 remote proof 後，再按準確 object ID 核對並刪除兩個已冗餘 stash ref；兩份舊 patch 從未 apply 或 pop。
- **Reliability and security / 可靠性同安全：** bundle saves serialize to a unique same-directory staging file and only report success after `File.Replace` or `File.Move`; failure cleans owned staging residue, preserves any previous destination, keeps the editor dirty, and shows bilingual non-blocking status. `PackageManagerInputPolicy` treats proxy authority and vcpkg triplet as bounded structured command values. Persistence and command construction both fail closed; Settings no longer collects credentials it cannot safely use, and detected legacy values stay DPAPI-protected until explicitly forgotten/reset without entering URLs, previews, or process arguments. · 清單先寫同資料夾唯一暫存檔，`File.Replace`／`File.Move` 成功先報成功；失敗會清理自家暫存、保留舊檔同未儲存狀態，再用雙語非阻塞狀態回報。新 policy 將 proxy authority 同 vcpkg triplet 當有界結構化指令值，保存同建立指令兩邊 fail closed；Settings 唔再收集無法安全使用嘅認證，舊值只會 DPAPI 保護直到明確刪除／重設，永遠唔會進入 URL、預覽或者 process argument。
- **Accessibility, localization, and layout / 無障礙、本地化同版面：** package sections expose semantic headings and polite live status, manager names honor English/Cantonese/bilingual mode, fixed toolbars/dialog widths were replaced with bounded narrow-safe rows, action targets are at least 44 pixels, long strips scroll, and dynamic controls have programmatic names. · 套件分節有語意 heading 同 polite live status，管理器名稱跟英文／粵語／雙語模式，固定 toolbar／dialog 闊度改成有界窄畫面安全分行，action target 最少 44 像素，長列可捲動，動態控制亦有程式化名稱。
- **Verification / 驗證：** source lanes passed **29/29** and **28/28** independently; the merged `PackageManagerCore.Tests` contract passes **30/30**, covering portable formats, source/command safety, queue lifecycle, redaction, atomic create/replace/failure preservation, and malicious proxy/triplet rejection. Exact `dotnet build WinForge.sln -c Debug -p:Platform=x64` exits 0 with **0 errors**; the exact self-contained publish exits 0; XAML literal safety passes; and the detailed source audit reports 336 XAML files, 2,875/2,875 resolved handlers, 1,922/1,922 resolved direct actions, 321 generated feature docs, 1,899 button docs, and zero lifecycle mismatches or actionable markers. · 兩條來源線各自 **29/29**／**28/28**，合併專項 contract **30/30**；完整 combined solution build 零 errors、自包含 publish exit 0、XAML safety 同詳細 source audit 全過，handler／direct action 全部 resolve，零 lifecycle mismatch／actionable marker。
- **Visual evidence / 視覺證據：** the exact combined self-contained `packages` binary was opened on fresh dedicated `LowLevelCUHeadlessPackagesUnion`. Inspected canonical page captures are 1049×646 (SHA-256 `059973A54C00DBCB2818E50EBFA92A62DB2DD24069F905ADB15FC8B0590A99A0`) and 720×650 (`EF33340FCFD3D456081BA4C027C08EEE29B0C769E5862F64CD7751C79B61E8BB`). At 720 pixels the dialog's proxy/vcpkg view (`B834A599AF7089601E44A9AABB15D9E68B3B2C241EBFFFAF2DC69B94B251CF4F`) contains no credential fields, and its final App Settings view (`6DF62DD0C4E7BE4A33FC083DB4D97950BB8A0D286B8286B7B6AB7F78E669DDE6`) shows three unclipped full-width actions. The exact owned PID/window was closed, the desktop reported zero windows, and its handle was released. The earlier repository-driver unrelated-window capture remains rejected and unpromoted. · Exact combined 自包含 `packages` binary 已喺全新專用 LowLevel desktop 開啟；正式 1049×646／720×650 頁面圖已檢視。720 像素 dialog proxy／vcpkg 圖冇 credential field，最底 App Settings 圖三個全闊 action 冇裁切；最後已關閉準確自家 PID／視窗、確認 desktop 零視窗並釋放 handle。較早 repo driver 不相關視窗圖繼續拒絕，冇升格。
- **Delivery and cleanup / 交付同清理：** source feature `06d876bd6c4512f5ad9eaeb0e7895728c5e69f17`, documentation tip `6f08be02b88f556907a1cfa20753848df9d37ba8`, and union tip `d8399b1c8` were pushed and proven ancestors of remote `main` at `6524543c565a6796a74101ef38dcbb6248eec651`. The expected package files were verified in the remote tree. Only then were the two clean package worktrees and their merged local/remote `codex/integrate-stash-*` branches removed, followed by exact-hash revalidation and removal of the two redundant stash refs. Hosted workflow/release observation remains asynchronous and is not claimed green here. · 來源功能、文件同 union tip 已 push，並證明全部係 remote `main` `6524543c5` 嘅 ancestor；預期套件檔亦已喺 remote tree 核實。之後先刪除兩個乾淨 package worktree、已合併嘅本機／remote `codex/integrate-stash-*` branch，再按準確 hash 重驗並移除兩個冗餘 stash ref。Hosted workflow／release 仍然非同步觀察，呢度唔會預先聲稱綠燈。

## Current 2026-07-24 Command Palette extension hosts — hardened branch ready for integration · 2026-07-24 Command Palette extension host — 強化分支準備整合

- **Scope / 範圍：** Command Palette extension packs can keep using declarative module, URL, copy, and structured-page commands, or explicitly declare a trusted local executable host for richer JSON-lines responses. Host results participate in the same bilingual search, palette, dock, and native structured-page surfaces without turning the pack folder into a general script launcher. · Command Palette extension pack 可以繼續用 declarative module／URL／copy／structured-page command，亦可以明確宣告受信任本機 executable host，經 JSON-lines 回傳較豐富結果。Host result 會加入同一套雙語搜尋、palette、dock 同原生 structured-page 畫面，而唔會將 pack 目錄變成任意 script launcher。
- **Security boundary / 安全界線：** execution is normal-integrity only and fail-closed. Each invocation rereads the current enabled marker and manifest from disk, accepts only a fully qualified local-drive `.exe`, rejects relative/UNC/network/device paths and unsafe arguments, rechecks the declared command, verifies SHA-256 with a fixed-time comparison, and holds a read-only file lease through `Process.Start` to close the hash-to-launch replacement window. Responses are bounded to 64 KiB and eight seconds; cancellation or timeout kills the owned process tree. This is an explicit local trust boundary, not a sandbox. · 執行只限 normal-integrity，而且任何唔確定情況都 fail closed。每次呼叫都會直接重讀最新 enabled marker 同 manifest；只接受完整本機 drive `.exe`，拒絕 relative／UNC／network／device path 同危險 argument，再核對已宣告 command，用固定時間比較驗證 SHA-256，並由 hash 到 `Process.Start` 全程持有唯讀 file lease，封住換檔競態。Response 上限 64 KiB／八秒；取消或 timeout 會終止自家 process tree。呢個係明確本機信任界線，唔係 sandbox。
- **Accessibility and localization / 無障礙同本地化：** host execution is asynchronous and cancellable, with a busy indicator and bilingual feedback instead of freezing the palette. Structured pages provide associated accessible names, live-region notices, selectable body text, bounded input, one clear primary action, 44-pixel action targets, narrow-safe stacking, and language changes that preserve live field values without leaking stale values between pages. · Host 執行改成非同步兼可取消，有 busy indicator 同雙語提示，唔會凍結 palette。Structured page 有正確關聯 accessible name、live-region 通知、可選取正文、有限輸入、一個清楚 primary action、44-pixel action target、窄畫面直排，同埋轉語言時保留當前欄位，但唔會將舊頁值帶去新頁。
- **Verification / 驗證：** `tests/CommandPaletteExtensionHost.Tests` passes **17/17**, covering path/hash/argument/enablement/elevation/command gates, exact copy text, URL and enum validation, single-primary and size limits, request correlation, cancellation, timeout cleanup, and structured-page bounds. The final managed project build passes with **0 errors** and 276 existing warnings. XAML literal safety passes; the detailed source-surface audit reports 334 XAML files, 2,870/2,870 resolved handlers, 1,919/1,919 resolved direct actions, zero subscription mismatches, zero actionable markers, 319 generated feature docs, and 1,896 generated button docs. · 專項 host harness **17/17** 全過，涵蓋 path／hash／argument／enablement／elevation／command gate、原樣 copy、URL／enum 驗證、單一 primary／大小限制、request 對應、取消／timeout 清理同 structured-page 邊界。最終 managed project build 零 errors（276 個既有 warning）；XAML safety 同詳細 source-surface audit 全過，handler／direct action 全部 resolve，亦冇 subscription mismatch 或 actionable marker。
- **Visual evidence / 視覺證據：** the self-contained driver published the app and the `cmdpalette` launch-only fallback passed (PID 15920). LowLevel MCP exists on disk but is not callable in this session. Desktop `CopyFromScreen` was unavailable and `PrintWindow` returned a blank or near-uniform WinUI client, so no PNG was created, retained, replaced, or promoted; visual status is honestly `capture-blocked`, not visual-pass. The isolated structured host window was not opened by installing a live user extension, so test setup did not mutate LocalAppData extension state. · Self-contained driver 已 publish app，`cmdpalette` launch-only fallback 通過（PID 15920）。LowLevel MCP 喺 disk 存在，但今次 session 冇可呼叫工具；desktop `CopyFromScreen` 不可用，`PrintWindow` 又只得空白／近乎單色 WinUI client，所以冇建立、保留、替換或升格 PNG，視覺狀態如實係 `capture-blocked`，唔係 visual-pass。亦冇為咗開 isolated host window 而安裝真實 user extension，避免測試改動 LocalAppData extension state。
- **Delivery boundary / 交付界線：** preserved feature commit `9bd611e57` is followed by local `origin/main` integration merge `3c065c673` on `codex/powertoys-extension-host`. This bounded branch is prepared for its own upstream push; final `main` merge, remote-main proof, hosted workflow/release observation, and branch/worktree cleanup remain with the coordinating agent. · 已保留功能 commit `9bd611e57`，並喺 `codex/powertoys-extension-host` 以 `3c065c673` 合併最新 `origin/main`。呢個 bounded branch 會獨立 push；最終 `main` merge、remote-main proof、hosted workflow／release 觀察同 branch／worktree 清理交由統籌 agent 完成。
## Current 2026-07-24 reactor industrial-load integration — local gates green · 2026-07-24 反應堆工業負載整合 — 本機 gate 全綠

- **Scope / 範圍：** the four ordered commits from `origin/claude/remaining-tasks-kx867f` were integrated as `f591d3304`, `ef2f8a1d6`, `52639fbc9`, and `52bccbdd2`, adding the backlog/count sync, reactor-powered ammonia plant, and strict-priority grid load-shed dispatcher. Follow-up hardening is commit `cf7b28b8`: the ammonia default is production-capable, duplicate ticks cannot advance pressure/tonnes/unserved energy/reclose progress, cold-bus enabled demand is represented as shed without a false event, non-finite inputs fail closed, and the two pages have responsive bilingual labels, semantic theme colors, 44-pixel targets, and automation names/help. · 四個 Claude commit 已按次序整合，加入 backlog／數量同步、反應堆供電合成氨廠，同嚴格優先級電網卸載；`cf7b28b8` 再修實可生產預設值、重複 tick／冷母線／非有限輸入處理、響應式雙語版面、theme 色、44 像素目標同 automation 說明。
- **Local verification / 本機驗證：** `dotnet build WinForge.sln -c Debug -p:Platform=x64` completed with **0 errors**; `dotnet run --project tests/ReactorSim.Tests -c Debug` passed **65/65** with exit 0, including duplicate-tick and cold-bus assertions; XAML literal safety and the detailed source-surface audit passed. The refreshed generator reports **321** module pages and **1,905** actionable-control pages; Pages data reports **321** modules, **22** categories, **1,216** features, and **2,278** wiki pages. The smoke inventory found **348** fixed routes, five dynamic families, **822** aliases, and no structural routing mismatch; it includes all five new aliases (`ammonia`, `fertilizer`, `fertiliser`, `loadshed`, `mwbudget`). · solution 零 errors、Reactor **65/65** exit 0、重複 tick／冷母線 assertion、XAML literal safety 同詳細 source audit 全過；生成資料係 321 module／1,905 actionable control，Pages data 係 321 modules／22 categories／1,216 features／2,278 wiki pages；smoke inventory 有 348 固定 route、五組動態家族、822 aliases、零結構 routing mismatch，亦有齊五個新 alias。
- **Visual evidence / 視覺證據：** both `ammonia` and `loadshed` opened as fresh 1574×887 WinUI windows on the dedicated `WinForgeClaudeRemaining` LowLevel headless desktop. Both inspected client captures were solid black; the repository driver independently rejected its blank/near-uniform fallback, and the permitted visible-desktop fallback failed with `Access is denied`. The owned app processes were stopped and the desktop was closed. No invalid PNG is promoted, so visual status is honestly `capture-blocked`, not pass. · 兩頁都成功喺專用 LowLevel desktop 開啟，但已檢視 client capture 全黑；repo driver 亦拒絕空白 fallback，而可見 desktop fallback 就 access denied。自家 process 同 desktop 已清理；冇無效 PNG 升格，所以 visual 如實係 `capture-blocked`。
- **Delivery state / 交付狀態：** code hardening `cf7b28b8` and documentation/Pages `593b89d1` were pushed on `codex/integrate-claude-remaining`. After fetch, local HEAD, `origin/codex/integrate-claude-remaining`, and `git ls-remote` all resolved to exact `593b89d13bef8d8444faf8f3367269023a6e0e9c`. This subtask intentionally does not merge `main`; the parent integration owner retains that boundary. · code 修實 `cf7b28b8` 同文件／Pages `593b89d1` 已 push；fetch 後本機 HEAD、remote-tracking ref 同 `ls-remote` 全部準確係 `593b89d13bef8d8444faf8f3367269023a6e0e9c`。呢個 subtask 按界線唔會自行 merge `main`，交返畀上層整合負責人。
## Current 2026-07-24 Dew Encryption current-main integration — local gates green · 2026-07-24 Dew Encryption current-main 整合 — 本機 gate 全綠

- **Scope and safety / 範圍同安全：** The native bilingual Dew workspace is integrated onto the current managed baseline with compatible adjacent Git history, history/details, manual and debounced snapshots, deletion-aware rollback-safe restore, and secret-safe password/header-encrypted 7z export. The current-main audit rejects reparse points in selected paths and existing ancestors; bounds imported commits, names, entries, depth, and helper output; propagates cancellation through traversal and tools; and preserves a live replacement in a safety snapshot before applying a historical deletion. Extracted read-only history accepts the same deletion as a no-op. · 原生雙語 Dew 工作區已整合到目前 managed baseline，提供相容旁置 Git 歷史、history／details、人手同 debounced snapshot、識得處理刪除並可 rollback 嘅還原，以及 secret-safe 密碼／檔名加密 7z 匯出。審核強化會拒絕選取路徑同現存祖先嘅 reparse point，限制匯入 commit／名稱／entry／深度／helper output，將取消傳入 traversal 同工具，並喺套用歷史刪除前先 safety-snapshot 目前替代內容；extracted read-only history 對同一刪除安全 no-op。
- **UI and language / UI 同語言：** The page adaptively stacks action, snapshot, password, history, watcher, and Vault surfaces at narrow widths; adds semantic section headings, accessible names, live status announcements, localized tooltips, and 40-pixel action targets; keeps **Open Vault** enabled whenever idle; and categorizes errors in English, Cantonese, and bilingual modes while retaining technical detail. Cancellation copy now states the real checkpoint boundary: Git/traversal can stop promptly, whereas an in-process 7-Zip native call must return first. · 頁面喺窄闊度會 adaptive 直排 action、snapshot、password、history、watcher 同 Vault，加入語義 section heading、無障礙名稱、live status、localized tooltip 同 40-pixel action target；idle 時 **Open Vault** 保持可用；三種語言模式會分類錯誤兼保留技術細節。取消文案如實講明 checkpoint：Git／traversal 可以盡快停，但程序內 7-Zip native call 要先返回。
- **Loaded watcher follow-up / 高負載 watcher 跟進：** A combined-main run exposed that the old watcher assertion allowed only 12 seconds even though its own external-process budget was 30 seconds; the focused case still passed in 10.5 seconds, leaving almost no scheduling headroom. `Start()` also enabled file events before setting its running flag, creating a narrow real startup race. Auto-history now sets the flag first and rolls it back if event activation throws. The regression uses a named 45-second event-to-commit budget (30 seconds plus 15 seconds for debounce/scheduling/contention), performs three rapid writes, and proves exactly one new commit contains the final value. · combined-main run 發現舊 watcher assertion 只俾 12 秒，但自己 external-process budget 已經係 30 秒；focused case 雖然 10.5 秒通過，但排程餘量幾乎冇晒。`Start()` 仲會先開檔案事件、後設 running flag，形成一個窄但真實嘅啟動 race。Auto-history 而家先設 flag，事件啟動拋錯就 rollback。Regression 用具名 45 秒 event-to-commit budget（30 秒加 15 秒 debounce／排程／contention），快速連寫三次，並證明只得一個新 commit 而且包含最終值。
- **Local verification / 本機驗證：** `DewEncryption.Tests` passed **23/23** with no skips under concurrent host load in 498.8 seconds, including writable and extracted historical-deletion restores, ancestor reparse rejection, rollback, dangerous Git config, watcher, SHA-256 history, pinned-upstream interoperability, and encrypted/unencrypted archive cases. Three direct strengthened-watcher stress runs also passed in 63.26, 14.13, and 10.54 seconds total; the slowest stayed within the 45-second event-to-commit phase, with the remaining time spent in the new post-commit history/content validation. The final targeted x64 test-project build completed with zero warnings and zero errors, and its rebuilt focused case passed **1/1** in 16.0 seconds. `dotnet build WinForge.sln -c Debug -p:Platform=x64` completed with zero errors. The XAML literal-safety gate passed and now protects **17** managed ToggleSwitch defaults including Dew; the source audit resolved **2,888/2,888** referenced handlers across **335** XAML files with zero mismatches or tracked markers. Generated references report **320** feature pages and **1,917** button pages; regenerated Pages data reports **320** modules, **22** categories, **1,215** features, and **2,288** wiki pages. · Dew 測試喺 concurrent host load 用 498.8 秒 **23/23** 全過兼零 skip，包括兩種歷史刪除還原、祖先 reparse 拒絕、rollback、危險 Git config、watcher、SHA-256、上游 interop 同加密／非加密 archive。三次加強版 watcher 直接 stress run 亦以 63.26、14.13 同 10.54 秒通過；最慢嗰次 event-to-commit 仍然喺 45 秒內，餘下時間用咗喺新增嘅 post-commit history／內容驗證。最後 targeted x64 test-project build 係零 warning／零 error，而 rebuilt focused case 用 16.0 秒 **1/1** 通過；solution build 零 errors；XAML gate 通過並保護包括 Dew 在內 **17** 個 managed toggle default；source audit 喺 **335** 個 XAML file 解析晒 **2,888/2,888** handler，零 mismatch／marker。生成 reference 有 **320** 個 feature page 同 **1,917** 個 button page；Pages data 係 **320** modules／**22** categories／**1,215** features／**2,288** wiki pages。
- **Visual evidence disposition / 視覺證據處置：** The owned self-contained driver published and launched the correct `dew-encryption` route, but `CopyFromScreen` was unavailable and the `PrintWindow` fallback was blank or near-uniform. LowLevel MCP then created a dedicated headless desktop and confirmed a fresh live 1574×887 `WinUIDesktopWin32WindowClass` with a visible child bridge; its reported capture was inspected and found completely black, and showing the desktop was denied by the host. The owned process was stopped and the desktop handle closed. No invalid image is retained or claimed as proof; the fresh canonical screenshot is explicitly pending a graphics-capable session. · 自家 driver 成功 publish 同開啟正確 Dew route，但 `CopyFromScreen` 不可用而 `PrintWindow` 只得空白／近乎單色。LowLevel MCP 喺專用 headless desktop 確認新鮮 1574×887 live WinUI window 同 visible child bridge，但回傳 capture 經檢視係全黑，host 亦拒絕顯示 desktop；之後已停止自家 process 同關閉 desktop handle。冇保留或聲稱無效圖片係證據；正式新截圖明確等 graphics-capable session 補回。
- **Delivery state / 交付狀態：** The unique Dew feature was cherry-picked as `d15eaf6b8` and the audit hardening, regression coverage, generated references, and this handoff are carried on `codex/integrate-dew-current` for the parent integrator. No merge to `main`, branch deletion, hosted workflow, release, or external Wiki/Discussion update is claimed by this scoped integration task. · Dew 功能以 `d15eaf6b8` cherry-pick；審核強化、regression coverage、生成 reference 同呢份交接由 `codex/integrate-dew-current` 交俾 parent integrator。今次 scoped task 唔會聲稱已 merge `main`、刪 branch、跑 hosted workflow、發 release 或更新外部 Wiki／Discussion。
## Current 2026-07-24 audio/nullability and safe visual capture · 2026-07-24 音訊 nullable 同安全視覺擷取

- **Adversarial correction / 對抗修正：** review found Core Audio interface aliases being released multiple times through the same RCW and a capture compositor resolving the OS/application resource theme instead of the live root theme. Session, activation, and policy aliases now release exactly once through their owning RCW; invalid process IDs fail before routing COM activation; reset to “System default” continues to pass a null HSTRING. · 覆核發現 Core Audio interface alias 經同一 RCW 多次 release，同 capture compositor 用咗 OS／application resource theme 而唔係即時 root theme。Session、activation 同 policy alias 而家只經 owner RCW release 一次；無效 process ID 喺 routing COM activation 前 fail；還原「系統預設」繼續傳 null HSTRING。
- **Capture safety / 截圖安全：** requests are limited to absolute PNG paths of at most 1,024 characters on fixed/removable local drives. Premultiplied pixels are composited against `RootGrid.ActualTheme`, a unique same-directory partial file is flushed, and failure logs omit the requested path. The driver removes stale output at attempt start, restores its capture environment, validates color/dimensions, and sends every live-tree/`PrintWindow` result through same-directory `MoveFileEx(REPLACE_EXISTING | WRITE_THROUGH)` before the requested filename changes. It retains the original process handle, retries both temporary cleanups, and never uses `CopyFromScreen`; targeted `PrintWindow` is the only fallback. · Request 只接受 fixed／removable 本機 drive 上最多 1,024 字元嘅絕對 PNG。Premultiplied pixels 按 `RootGrid.ActualTheme` 合成並先 flush 同目錄唯一 partial file，失敗 log 唔會包括要求路徑。Driver 開始擷取就移除舊 output、還原 capture environment、驗證色彩／尺寸；所有 live-tree／`PrintWindow` 結果都先經同目錄 `MoveFileEx(REPLACE_EXISTING | WRITE_THROUGH)`，要求檔名先會改變。佢保留原始 process handle、重試兩種 temp 清理，永遠唔用 `CopyFromScreen`；唯一後備係 targeted `PrintWindow`。
- **Verification / 驗證：** the final solution build passes with **0 warnings and 0 errors**; audio interop and capture-policy/source-contract harnesses pass **6/6** and **9/9**; PowerShell 5.1 parsing, XAML literal safety, and the source audit (**2,870/2,870** handlers, **1,919/1,919** direct actions) pass. The atomic driver leaves zero capture temporaries/windows/processes, and a bounded scan finds zero requested-target matches in persistent WinForge crash/startup/owned logs. Fresh app-owned **1264×791**, **784×691**, and canonical **1284×811** frames plus independent fresh-HWND **1280×800**, **800×700**, and **1300×820** frames were inspected with no clipping, overlap, stale/foreign pixels, or leakage. The tracked canonical and wiki-local copies are byte-identical at SHA-256 `A7C1F09F3CB6636DEED27CF30158A14E2C0AAC21648245FA1ED412A946D0FB2E`. Headless desktops had no playback endpoint, so the persistent fail-closed notification is expected; no session data was invented. · 最終 solution build **0 warning／0 error**；audio interop／capture-policy／source-contract harness **6/6**、**9/9**；PowerShell 5.1 parser、XAML safety 同 source audit（2,870／2,870 handlers、1,919／1,919 direct actions）全過。Atomic driver 冇遺留 capture temp／window／process；有限 scan 喺 persistent WinForge crash／startup／自家 logs 搵到零個要求 target match。已檢視 app-owned **1264×791**、**784×691**、正式 **1284×811**，同獨立 fresh-HWND **1280×800**、**800×700**、**1300×820**；冇裁切、重疊、舊／其他視窗 pixels 或洩漏。Tracked 正式圖同 wiki-local copy byte-identical，SHA-256 如上。Headless desktop 冇 playback endpoint，所以持續 fail-closed notification 屬預期；冇虛構 session data。
- **Recorder follow-up / Recorder 跟進：** that review-lane aggregate finding is resolved by this dedicated recorder repair; the exact combined runner now passes all **31/31 projects**. · 當時 review lane aggregate finding 已由呢條專用 recorder 修復解決；準確 combined runner 而家 **31/31 project 全過**。
- **Delivery / 交付：** reviewed implementation commit `05451bcd63e5f7c7ac5fddeeb5d19b40f6763008` contains the COM/capture corrections, two focused regression projects, categorized docs, wiki/Pages handoff, roadmap, persistent memory, and refreshed canonical evidence. This completion record is its child on `codex/review-audio-capture`; the task handoff requires `origin/codex/review-audio-capture` to match the completion-record tip exactly. The branch intentionally remains unmerged and its worktree remains present for the coordinating integration sequence. · 已覆核 implementation commit `05451bcd63e5f7c7ac5fddeeb5d19b40f6763008` 包含 COM／capture 修正、兩個專項 regression project、分類文件、wiki／Pages handoff、roadmap、持久記憶同更新正式證據。呢段 completion record 係佢喺 `codex/review-audio-capture` 嘅 child；交接前要求 `origin/codex/review-audio-capture` 同 completion-record tip 完全一致。Branch 刻意保持未 merge，worktree 亦保留畀統籌整合流程。

## Current 2026-07-20 native legacy-ref retirement — destination retention recorded · 2026-07-20 原生舊 ref 退役 — 已記錄目標保留

- **Scope / 範圍：** The legacy C++20/C++/WinRT checkout refs for ASCII Table, Base Converter, BMI, Date Calculator, Duration Calculator, Loan Calculator, Percentage Calculator, Unit Price, and the release-boundary work are retained in the standalone WinForge-Native closure history before their old managed-checkout worktrees are retired. The closure uses source-tree-neutral retention merges: it preserves provenance without putting rewrite files back into this managed tree. · ASCII Table、Base Converter、BMI、Date／Duration／Loan Calculator、Percentage Calculator、Unit Price 同 release-boundary 舊 C++20/C++/WinRT checkout ref，會先保留喺獨立 WinForge-Native 嘅 closure history，之後先退役舊 managed checkout worktree。closure 用唔改 source tree 嘅 retention merge，保留來源而唔會將 rewrite file 放返入呢個 managed tree。
- **Destination evidence / 目標證據：** ASCII, BMI, and Unit Price source/tests are byte-identical in native main; Base Converter and Percentage Calculator are retained with deliberate whitespace hardening; TextAnalysis is byte-identical and the standalone workflow supersedes the old shared release setup. Date, Duration, and Loan feature snapshots are also retained on pushed native WIP refs (99557bcf, 23f63ebf, and 559a23a8) and are not claimed as integrated native-main features. · ASCII、BMI、Unit Price source／test 喺 native main byte-identical；Base Converter 同 Percentage Calculator 保留咗刻意嘅 whitespace hardening；TextAnalysis byte-identical，而獨立 workflow 已取代舊 shared release。Date、Duration、Loan feature snapshot 亦喺已 push 嘅 native WIP ref（99557bcf、23f63ebf、559a23a8），唔會誤報成已整合 native-main feature。
- **Boundary and disposition / 界線同處置：** Managed main continues to carry only the .NET 11/WinUI 3 app and its AudioForge/ImageForge companion C++ programs, not the rewrite. No managed UI or screenshot changes are part of this cleanup. Unrelated dirty PowerToys and Reactor/Dew work remain preserved and are outside this native-ref retirement. · managed main 繼續只帶 .NET 11／WinUI 3 app 同 AudioForge／ImageForge companion C++ program，唔包括 rewrite。今次清理冇 managed UI／截圖改動；唔相關嘅 dirty PowerToys 同 Reactor／Dew 工作會保留，唔屬於今次 native-ref 退役。

## Current 2026-07-20 AWS Manager native EC2 continuation — local gates green · 2026-07-20 AWS Manager 原生 EC2 延續開發 — 本機 gate 全綠

- **Scope / 範圍：** The canonical .NET 11 / WinUI 3 AWS Manager now includes a native EC2 workspace beside its existing S3, Resource Explorer, credential/profile, CLI, CloudFormation, Cost Explorer, and Cloud Control surfaces. `aws`, `awscli`, `s3`, and the new `ec2` route open the intended workspace. EC2 provides local filtering, paged instance discovery, bilingual details, and explicit Start, Stop, Reboot, and Terminate review flows backed by AWSSDK.EC2; it does not shell out to the AWS CLI. · 正式 .NET 11／WinUI 3 AWS Manager 而家喺既有 S3、Resource Explorer、profile、CLI、CloudFormation、Cost Explorer 同 Cloud Control 旁邊加入原生 EC2 工作區；`aws`／`awscli`／`s3`／`ec2` route 會直達正確畫面。EC2 有本機篩選、分頁 instance 清單、雙語詳情，同埋要明確確認先會執行嘅 Start／Stop／Reboot／Terminate，直接使用 AWSSDK.EC2，唔會 shell out 去 AWS CLI。
- **Safety and lifecycle / 安全同生命週期：** Profile/region context generations, operation ownership, stale-result rejection, selection/context revalidation after dialogs, and fail-closed state/action policy prevent old async work from mutating a new account context. S3 upload overwrite is atomic through `If-None-Match: *`; its shield and operation lease prevent overlapping actions from cancelling or exposing partial setup. Credential discovery returns profile metadata only and never retains secret values. Destructive EC2 and Cloud Control mutations require a fresh review bound to the exact context and selection. · profile／region generation、operation ownership、過期結果拒絕、dialog 後重新核對 selection／context，同 fail-closed state policy，會阻止舊 async 工作改到新 account context。S3 覆寫用 `If-None-Match: *` 原子保護，operation shield／lease 亦避免重疊操作；credential discovery 只回傳 profile metadata，唔保留 secret。EC2 同 Cloud Control 破壞性改動都要用綁定當前 context／selection 嘅新 confirmation。
- **Local verification / 本機驗證：** `dotnet build WinForge.sln -c Debug -p:Platform=x64` completed with **0 errors** (318 existing warnings). All **27/27** managed test projects passed in Release, including AWS Manager **11/11** and Reactor **63/63**. The XAML literal-safety gate passed. The full wiki generator produced **319** feature documents and **1,902** button documents, including **45** AWS button references; Pages data reports 319 modules, 22 categories, 1,214 features, and 2,272 wiki pages. · solution build 零 errors；Release test project **27/27** 全過，包括 AWS **11/11** 同 Reactor **63/63**；XAML safety 通過。完整 wiki generator 產生 319 個 feature 文件同 1,902 個 button 文件（AWS 45 個），Pages data 係 319 modules／22 categories／1,214 features／2,272 wiki pages。
- **Visual evidence / 視覺證據：** LowLevel MCP exists on disk but no headless tool is callable in this session. The managed self-contained driver published and launched the owned `ec2` route; desktop `CopyFromScreen` was unavailable and its `PrintWindow` fallback was blank, so an app-owned debug `RenderTargetBitmap` capture was used with empty temporary AWS config/credentials and the credential-safe capture flag. The inspected `2077×1302` direct-EC2 image is promoted to `docs/screenshot-aws.png` and its wiki mirror; both have SHA-256 `E49861D99B9FBA1942A09AAEA1E3BD88006F122CB0CA770BE0FDA019CF84BA49`. No live AWS account query or mutation was performed. · 今次 session 冇可呼叫嘅 LowLevel headless tool；managed self-contained driver 成功 publish 同開啟自家 `ec2` route。因為桌面 capture 不可用、`PrintWindow` 係空白，所以用 app-owned debug `RenderTargetBitmap`，配合空白暫存 AWS config／credentials 同安全 capture flag。已檢視嘅 `2077×1302` EC2 圖已升格到正式同 wiki mirror，SHA-256 相同；全程冇查詢或改動真實 AWS account。
- **Delivery and remote proof / 交付同遙距證明：** Feature commit `ea7238d7ca3f14abffeb6da81d43b20b8416cd55` was pushed on `codex/aws-ec2-manager`, merged to `main` as `84220d29a3c6057ead25957e4c99f1ad01f1ab77`, and remains an ancestor of the remotely proved site-data tip `17fb451c58c4a1aa61b99494172a008b21d68030`; remote-tree checks found the EC2 policy/mapper, test project, AWS guide, and screenshot on `origin/main`. Branch run [29721000525](https://github.com/Ding-Ding-Projects/WinForge/actions/runs/29721000525), merged-main run [29721014631](https://github.com/Ding-Ding-Projects/WinForge/actions/runs/29721014631), site-data run [29721014577](https://github.com/Ding-Ding-Projects/WinForge/actions/runs/29721014577), exact-tip run [29721270091](https://github.com/Ding-Ding-Projects/WinForge/actions/runs/29721270091), and Pages runs [29721014619](https://github.com/Ding-Ding-Projects/WinForge/actions/runs/29721014619) / [29721275818](https://github.com/Ding-Ding-Projects/WinForge/actions/runs/29721275818) all passed. [`v1.1.264`](https://github.com/Ding-Ding-Projects/WinForge/releases/tag/v1.1.264) is non-draft, non-prerelease GitHub Latest at exact `17fb451c`, with exactly `WinForge-Setup.exe` and `WinForge-portable-x64-1.1.264.zip`; the feature and merge releases `v1.1.262` / `v1.1.263` are honestly retained as prereleases. Managed Wiki commit `ab16d14eea6890198d51daa08d9f25b8fef6cb27` publishes the updated guide and byte-identical EC2 screenshot without deleting unrelated Wiki-only content. The proved task branch was removed locally and remotely. This completion record is a docs-only follow-up and remains subject to the repository's same tests-first release contract. · 功能 commit `ea7238d7` 已 push、以 `84220d29` merge 入 `main`，並係已做 remote proof 嘅 site-data tip `17fb451c` 祖先；remote tree 有齊 EC2 policy／mapper、test、AWS 指南同截圖。branch、merge、site-data、exact-tip、Pages run 全綠；`v1.1.264` 係準確指向 `17fb451c` 嘅 stable GitHub Latest，只得 setup 同 portable ZIP，而 `v1.1.262`／`v1.1.263` 如實保留為 prerelease。managed Wiki `ab16d14` 已發佈更新指南同 byte-identical EC2 圖，冇刪除其他 Wiki-only 內容；經證明嘅 task branch 已喺本機同 remote 移除。呢個 completion record 只改文件，仍然受同一先測試後發佈合約約束。
- **Repository hygiene and preservation / Repository 衛生同保留：** The final branch/worktree/stash audit found no AWS task work outside `main`. It intentionally preserves unrelated unique-history Dew, ASCII Table, Base Converter, Health Calculators, Percentage Calculator, Unit Price, release-boundary, and remote Claude branches; uncommitted Date/Duration/Loan and PowerToys worktrees; and two nonredundant package-management stashes. None is merged, deleted, or rewritten by this AWS task. · 最終 branch／worktree／stash audit 冇發現 AWS task 工作散落喺 `main` 之外。獨特歷史嘅 Dew、ASCII Table、Base Converter、Health Calculators、Percentage Calculator、Unit Price、release-boundary、remote Claude branch，未提交嘅 Date／Duration／Loan／PowerToys worktree，同兩個非重複 package-management stash 全部保留；今次 AWS task 冇合併、刪除或者改寫佢哋。

## Current 2026-07-20 repository split completion — remote proof green · 2026-07-20 repository 分拆完成 — 遙距證明全綠

- **Repository boundary / Repository 界線：** The split feature commit `fe791aa6167dbe26dc358df3a31acce51bd0f931` was merged as `165477c4461c6bd33e30d3856ec076f638193e10`; the expected site-data refresh advanced the remotely proved integration tip to `be054aa737df860b1185bd7b1102d8dd9e80ae8e` before this completion record. Remote-tree proof confirms [Ding-Ding-Projects/WinForge](https://github.com/Ding-Ding-Projects/WinForge) contains the managed solution, managed release workflow and installer, but none of `WinForge.Native.sln`, `src/WinForge.App`, `src/WinForge.Core`, `tests/native`, the parity ledger, or the native installer/workflow. Standalone native `main` is `a64e8e30ed8b5fe376197448ba760d1374244c69` and proves the inverse boundary. · 分拆 commit 已經 merge；呢段完成記錄之前，site-data 將已做 remote proof 嘅 integration tip 更新到 `be054aa7`。遙距 tree 證明正式 repo 只保留 managed solution／release／installer，原生 rewrite source、tests、ledger、installer 同 workflow 已搬走，而獨立原生 `main` `a64e8e30` 就準確保留相反界線。
- **Hosted managed proof / Managed hosted 證明：** branch run [29715061742](https://github.com/Ding-Ding-Projects/WinForge/actions/runs/29715061742) passed and published exact-SHA prerelease `v1.1.257`; merged-main run [29715516125](https://github.com/Ding-Ding-Projects/WinForge/actions/runs/29715516125) passed and honestly kept superseded `165477c4` as prerelease `v1.1.258`; site-data run [29715516151](https://github.com/Ding-Ding-Projects/WinForge/actions/runs/29715516151) and integration-tip run [29715701032](https://github.com/Ding-Ding-Projects/WinForge/actions/runs/29715701032) passed. [Managed `v1.1.259`](https://github.com/Ding-Ding-Projects/WinForge/releases/tag/v1.1.259) was non-draft, non-prerelease, GitHub Latest at that proof point, and targets exact `be054aa7`; it contains exactly `WinForge-Setup.exe` and `WinForge-portable-x64-1.1.259.zip`. Pages run [29715705513](https://github.com/Ding-Ding-Projects/WinForge/actions/runs/29715705513) is green and publishes 319 modules, 22 categories, 1,214 features, and no rewrite metadata. Every later docs-only `main` commit, including this completion record, remains subject to the same tests-first release contract. · branch、merge、site-data、integration-tip 同 Pages hosted run 全部通過；`v1.1.259` 喺該證明點係準確指向 `be054aa7` 嘅 stable Latest，只得 managed installer 同 portable ZIP，Pages data 亦冇原生 rewrite metadata。之後包括呢段記錄在內嘅 docs-only `main` commit，仍然要通過同一套先測試後發佈合約。
- **Hosted native proof / 原生 hosted 證明：** [native run 29715120945](https://github.com/codingmachineedge/WinForge-Native/actions/runs/29715120945) and [Pages run 29715120958](https://github.com/codingmachineedge/WinForge-Native/actions/runs/29715120958) passed at exact `a64e8e30`; [native-v1.1.7](https://github.com/codingmachineedge/WinForge-Native/releases/tag/native-v1.1.7) is the native stable Latest with exactly `WinForge-Native-Setup.exe` and `WinForge-native-x64-1.1.7.zip`. Preserved WIP branches remain remotely exact at Date `99557bcf`, Duration `23f63ebf`, and Loan `559a23a8`. · 原生 CI／Pages 同 stable Latest 全部準確指向 `a64e8e30`，只得兩個原生 asset；三條未完成計算器 WIP branch 亦已獨立 push 同保留。
- **Documentation and visual disposition / 文件同視覺處置：** managed Wiki commit `be2571545ee81b9286f36a8a96aa72fdc92769b2` is pushed and live; both [managed Pages](https://ding-ding-projects.github.io/WinForge/) and [native Pages](https://codingmachineedge.github.io/WinForge-Native/) return HTTP 200. GitHub has not initialized `WinForge-Native.wiki.git`: the Wiki URL redirects to the repository, the Git endpoint returns repository-not-found, and no authenticated browser or supported Wiki API was available, so tracked native docs plus Pages are the published native documentation. No managed UI changed; the process-owned Dashboard launch-only check passed and no managed screenshot was replaced. · managed Wiki 同兩個 Pages site 已上線；新原生 Wiki 因 GitHub 未初始化、冇已登入 browser／支援 API 而未能建立第一頁，所以以 tracked docs 同 Pages 發佈。今次冇改 managed UI，Dashboard launch-only 通過，亦毋須換截圖。
- **Cleanup and preservation / 清理同保留：** ancestry-proven split/proof/bootstrap branches, five clean merged worktrees, nine merged original remote branches, stale metadata, and one byte-identical redundant stash were removed only after remote proof; the temporary native remote was removed from the managed checkout. Dirty Date/Duration/Loan and PowerToys worktrees, unique Dew work, exact-tip-divergent historical native/release branches, and the two nonredundant stashes remain untouched. Post-cleanup audit found both default checkouts clean and synchronized. · 遙距證明後先清除已合併 branch／worktree／remote branch／重複 stash 同暫時 remote；有未提交或獨特歷史嘅工作樹、branch 同兩個有效 stash 全部保留，兩個 default checkout 最後都乾淨並同 remote 同步。

## Current 2026-07-19 repository split — managed app restored as canonical · 2026-07-19 repository 分拆 — 正式 app 回復為 managed 版

- **Scope / 範圍：** The experimental C++20/C++/WinRT rewrite moved to [codingmachineedge/WinForge-Native](https://github.com/codingmachineedge/WinForge-Native). This repository again owns only the canonical .NET 11 / WinUI 3 app and its managed installer, updater, documentation, Pages data, and releases. `native/` stays here because it contains small companion applications used by the managed app, not the rewrite. · 實驗性 C++20／C++/WinRT 重寫版已搬去獨立 WinForge-Native；呢個 repository 再次只負責正式 .NET 11／WinUI 3 app、managed installer／updater、文件、Pages data 同 release。`native/` 入面係 managed app 使用嘅細型 companion，唔係重寫版，所以保留。
- **Native remote state / 原生 remote 狀態：** standalone `main` baseline `842f8dacbdde96a54fc015cf2bdbd7e92813dc8f` passed hosted native CI/installer run `29713539685` and Pages run `29713539675`. Preserved WIP tips also passed hosted CI and exact-SHA prereleases: Date `99557bcf481c963c7a4700b32fe6c0f5d7811b6d`, Duration `23f63ebfd69e07fe0f7d6b4ed51e4af40996848a`, and Loan `559a23a81840130c01316393ae45986a855e6c87`. · 獨立原生 `main` 同三條保留 WIP branch 嘅 hosted CI、installer、Pages／prerelease 證據已通過。
- **Managed local validation / 正式版本機驗證：** `dotnet build WinForge.sln -c Debug -p:Platform=x64` completed with zero errors; all **26/26** managed test projects passed in Release, including Reactor **63/63**; the XAML literal-safety gate passed; the self-contained driver published and completed an owned Dashboard launch-only check; workflow YAML and changed PowerShell parsed successfully. · managed solution 零 errors、Release test project **26/26**（包括 Reactor **63/63**）、XAML safety、self-contained Dashboard owned launch-only、workflow YAML 同 PowerShell parse 全部通過。
- **Visual evidence / 視覺證據：** The repository split changes no managed UI surface, so no managed canonical screenshot was replaced. Native visual evidence and the accepted Percentage Calculator image moved with WinForge-Native; its About page received launch-only evidence during standalone validation. · 分拆冇改 managed UI，所以冇替換 managed canonical 截圖；原生 visual 證據同已接受嘅 Percentage Calculator 圖已跟 WinForge-Native 搬走，獨立驗證亦完成 About launch-only check。
- **Git state / Git 狀態：** Native source, tests, parity tooling/ledger, dedicated installer, workflow, and feature evidence are removed from this tree only after being pushed to the new repository. Final managed branch/main ancestry, hosted managed release, GitHub Wiki sync, and task-worktree cleanup are recorded in the follow-up completion entry after remote proof. · 原生 source、tests、parity tooling／ledger、專用 installer、workflow 同功能證據已先 push 去新 repository，之後先由呢個 tree 移除；managed branch／main ancestor、hosted release、GitHub Wiki 同 worktree cleanup 會喺 remote proof 後下一段 completion entry 記錄。

## Current 2026-07-20 native Percentage Calculator controlled integration — local gates green · 2026-07-20 原生百分比計算器受控整合 — 本機 gate 已通過

- **Scope / 範圍：** `percent`, `percentage`, and `module.percentcalc` are integrated as dependency-free standard-C++ `PercentCalc` plus a genuine C++/WinRT renderer. Six local cards preserve percent-of, reverse percent, signed change, increase/decrease, tip splitting, ratio simplification, current-culture/invariant parsing, managed Unicode trimming, six-place away-from-zero display rounding, banker’s people rounding, localization retention, fresh-route reset, accessibility, and explicit-only clipboard Copy; no CLR host or managed delegation is included. The sole native workflow is also hardened for test-gated, idempotent C++-only release retries.
- **Current evidence / 目前證據：** Debug and Release x64 native builds have 0 errors; both cores pass **915/915**, including Percentage Calculator **37/37**; focused `-PercentCalcRoutesOnly -AllowClipboardMutation` UIA passes **14/14**; catalog parity is **346 fixed routes + five dynamic families**, 319 registry records, and 346 ledger rows; installer contract passes; renderer accounting is **38/346** (`38 in-progress / 308 not-started`).
- **Visual and delivery / 視覺同發佈：** the repository-local LowLevel checkout has no callable headless tool, but the required native driver obtained a valid 1962×1311 PrintWindow fallback after CopyFromScreen was unavailable. It was visually inspected and promoted as `docs/screenshot-percent.png` and its wiki-local copy; visual evidence is `pass`. Publishing remains C++-only; earlier hosted GitHub API-outage failures are pending remote repair after this controlled push.

**粵語摘要：** 三個百分比 alias 已受控整合成標準 C++ core 同真正 C++/WinRT renderer，六張卡保留 managed 百分比、反求、變化、加減、貼士同化簡比例，以及相容 Unicode 修剪、取捨、語言保留、新 route 重設、無障礙同只限明確 Copy。Debug／Release 0 errors、core 各 **915/915**（Percentage Calculator **37/37**）、UIA **14/14**、catalog parity 346+5／319 registry／346 ledger 同 installer contract 通過；renderer **38/346**。LowLevel MCP 未可呼叫，但 driver 有效 PrintWindow 截圖已檢視並升格，所以 visual 係 `pass`；唯一 C++ publisher 已有測試 gate／idempotent retry，controlled push 後仲要 repair 較早 hosted API outage。

## Current 2026-07-20 native ASCII Table controlled integration — local gates green · 2026-07-20 原生 ASCII 表受控整合 — 本機 gate 已通過

- **Scope / 範圍：** `ascii`, `asciitable`, and `module.asciitable` are integrated as a dependency-free standard-C++ `AsciiTable` core plus genuine C++/WinRT renderer. It preserves 0–127 by default, explicit Latin-1 through 255, control/space/DEL/C1/NBSP distinctions, radix columns, invariant local search, virtualization, fresh-route reset, language-state retention, accessibility, and explicit-only raw-character Copy; no CLR host, managed delegation, workflow, or release-policy change is included.
- **Current evidence / 目前證據：** Debug and Release x64 native solution builds have 0 errors; both combined core suites pass **878/878**, including ASCII Table **21/21**; focused `-AsciiTableRoutesOnly -AllowClipboardMutation` UIA passes **16/16** across all aliases and language modes; catalog parity is **346 fixed routes + five dynamic families**, 319 registry records, and 346 ledger rows; and the native installer contract passes. Renderer accounting is **37/346 fixed routes**, **37 `in-progress` / 309 `not-started`**.
- **Visual and shell disposition / 視覺同 shell 狀態：** the repository-local LowLevel checkout exists but no headless MCP tool is callable in this session. The required native driver rejected a blank/near-uniform fallback after `CopyFromScreen` was unavailable; no PNG was retained and no root WinForge process remained, so visual evidence remains `capture-blocked`. The broader shell invocation was stopped after the observed pre-existing `wordfreq` launch stalled and is explicitly not a full-shell pass. Publishing remains C++-only.

**粵語摘要：** 三個 ASCII alias 已受控整合成唔靠依賴嘅標準 C++ core 同真正 C++/WinRT renderer，保留 0–127／明確 Latin-1 255、控制碼／空格／DEL／C1／NBSP、進制欄、invariant 搜尋、虛擬化、新 route 重設、語言保留、無障礙同只限明確 raw-character Copy。Debug／Release 0 errors、core 各 **878/878**（ASCII **21/21**）、專項 UIA **16/16**、catalog parity 346+5／319 registry／346 ledger 同 installer contract 已通過；renderer **37/346**（**37 `in-progress` / 309 `not-started`**）。LowLevel MCP 未可呼叫，driver 拒絕空白 fallback，冇 PNG／冇殘留 process，所以 visual 係 `capture-blocked`。較廣 shell 喺觀察到既有 `wordfreq` launch 卡住後已停止，唔當 full-shell pass；發佈繼續只限 C++。

## Current 2026-07-19 native Health Calculators controlled integration — local gates green · 2026-07-19 原生健康計算器受控整合 — 本機 gate 已通過

- **Scope / 範圍：** `bmi`, `health`, and `module.bmi` are now integrated as a pure standard-C++ `Bmi` core plus C++/WinRT Health Calculators renderer. It preserves WHO BMI bands, Mifflin–St Jeor BMR, five TDEE factors, US Navy male/female body-fat rules, raw metric/imperial relabelling, invalid recovery, all three language modes, lifecycle reset, and no clipboard-write path; no CLR host, managed delegation, workflow, or release-policy change is included.
- **Current evidence / 目前證據：** Debug and Release x64 native solution builds have 0 errors; both combined core suites pass **842/842**, including BMI **14/14**; focused `-BmiRoutesOnly` UIA passes **14/14** across all aliases; catalog parity is **346 fixed routes + five dynamic families**, 319 registry records, and 346 ledger rows; the native installer contract passes. Renderer accounting is **35/346 fixed routes**, **35 `in-progress` / 311 `not-started`**.
- **Visual and release state / 視覺同發佈狀態：** the local LowLevel checkout exists but no headless MCP tool is callable in this session. The required native driver rejected its blank/near-uniform fallback after `CopyFromScreen` was unavailable; no PNG was retained or promoted, so visual evidence remains `capture-blocked`. Publishing remains C++-only.

**粵語摘要：** 三個健康計算器 alias 而家已同原生 shell 受控整合，係純標準 C++ core 加 C++/WinRT renderer，保留 WHO BMI 分級、Mifflin–St Jeor BMR、五個 TDEE 系數、美國海軍男女體脂規則、原始公制／英制重標籤、無效輸入復原、三種語言、route reset 同冇寫剪貼簿路徑。Debug／Release 0 errors、core 各 **842/842**（BMI **14/14**）、專項 UIA **14/14**、catalog parity 346+5／319 registry／346 ledger 同 installer contract 都通過；renderer **35/346**（**35 `in-progress` / 311 `not-started`**）。LowLevel MCP 不可呼叫，driver 拒絕空白 fallback，冇 PNG，所以 visual 保持 `capture-blocked`。發佈繼續只限 C++。

## Current 2026-07-19 native Unit Price controlled integration — local gates green · 2026-07-19 原生單位價格受控整合 — 本機 gate 已通過

- **Scope / 範圍：** `priceper`, `unitprice`, and `module.unitprice` are now integrated as a pure standard-C++ `UnitPrice` core plus C++/WinRT renderer. It preserves managed valid-row filtering, free/infinity/tolerance ties, invariant output, Add/remove/release/reset lifecycle, all three language modes, and explicit-only Copy; no CLR host, managed delegation, workflow, or release-policy change is included.
- **Current evidence / 目前證據：** Debug and Release x64 native solution builds have 0 errors; both combined core suites pass **828/828**, including Unit Price **13/13**; focused Unit Price UIA passes **15/15**; Utility UIA passes **39/39** including CSS Unit Converter; catalog parity is **346 fixed routes + five dynamic families**, 319 registry records, and 346 ledger rows; the native installer contract passes. Renderer accounting is **34/346 fixed routes**, **34 `in-progress` / 312 `not-started`**.
- **Visual and shell disposition / 視覺同 shell 狀態：** the local LowLevel checkout exists but no headless MCP tool is callable in this session. The required native driver rejected its blank/near-uniform fallback after `CopyFromScreen` was unavailable; no PNG was retained or promoted, so visual evidence remains `capture-blocked`. A broad aggregate reached the Unit Price assertions but did not return a captured final footer; it is explicitly not a completed full-shell claim. Publishing remains C++-only.

**粵語摘要：** 三個 Unit Price alias 而家已同原生 shell 受控整合，係純標準 C++ core 加 C++/WinRT renderer，保留有效行／免費／平手／invariant 顯示、增減／release／reset、三種語言同只限明確 Copy，冇 CLR／managed delegation／workflow／release-policy 改動。Debug／Release 0 errors、core 各 **828/828**（Unit Price **13/13**）、專項 UIA **15/15**、包括 CSS 嘅 Utility UIA **39/39**、catalog parity 346+5／319 registry／346 ledger 同 installer contract 都通過；renderer **34/346**（**34 `in-progress` / 312 `not-started`**）。LowLevel MCP 不可呼叫，driver 拒絕空白 fallback，冇 PNG，所以 visual 保持 `capture-blocked`；廣泛 aggregate 冇最後 footer，唔當 full-shell 完成。發佈繼續只限 C++。
## Historical 2026-07-19 native Health Calculators feature handoff (pre-integration) · 歷史原生健康計算器功能交接（整合前）

- **Scope / 範圍：** `bmi`, `health`, and `module.bmi` now resolve to one genuine C++/WinRT Health Calculators page over pure standard-C++ `Bmi` logic. It preserves managed BMI WHO bands, Mifflin–St Jeor BMR, five TDEE factors, US Navy body-fat formulae/validation, raw metric/imperial relabelling, all language modes, route lifecycle reset, and no clipboard mutation.
- **Evidence / 證據：** native Debug and Release x64 solution builds both have 0 errors; Debug and Release core each pass **829/829**, including **14/14 BMI Calculator** contracts; catalog parity is **346 fixed routes + five dynamic families**; the native installer contract passes; and focused `-BmiRoutesOnly` UI Automation passes **14/14** across all three aliases, including UIA bounds/names, formulae, invalid recovery, unit-state retention, localization, release, and in-process re-entry reset.
- **Visual and boundary / 視覺同界線：** LowLevel MCP is not callable in this Codex session. The required `bmi` driver found `CopyFromScreen` unavailable and rejected a blank/near-uniform `PrintWindow` fallback; no PNG was created or retained and no worktree process remained, so visual evidence is honestly `capture-blocked`. This is an isolated feature-only handoff: no workflow, release, GitHub, `main`, or push mutation occurred.

**粵語摘要：** `bmi`、`health` 同 `module.bmi` 已經係真正 C++/WinRT 健康計算器頁，用純標準 C++ `Bmi` logic；保留 managed BMI WHO 分級、Mifflin–St Jeor BMR、五個 TDEE 系數、美國海軍體脂公式／驗證、原始公制／英制重標籤、三種語言、route reset，同埋冇剪貼簿改動。Debug／Release 0 errors、core 各 **829/829**（BMI **14/14**）、catalog parity 346+5、installer contract 同三個 alias UIA **14/14** 已通過。今個 session 冇可呼叫 LowLevel MCP；driver 因 `CopyFromScreen`／空白 fallback 受阻，冇 PNG／冇殘留 process，所以 visual 如實係 `capture-blocked`。呢個係孤立功能 handoff，冇改 workflow／release／GitHub／`main`，亦冇 push。

**Remote proof / 遙距證明：** Unit Price merge `37cc0e8a1d4605864756751265d379a954978b27` is an ancestor of `origin/main`. [Native run 29706847786](https://github.com/Ding-Ding-Projects/WinForge/actions/runs/29706847786) passed every native gate and published stable `native-v1.0.76` exactly at that SHA with only `WinForge-Native-Setup.exe` and `WinForge-native-x64-1.0.76.zip`. Its successful site-data run committed `fbaff0788`; dispatched [native run 29707001900](https://github.com/Ding-Ding-Projects/WinForge/actions/runs/29707001900) passed and made stable `native-v1.0.77` Latest at that current-main SHA, again exactly the native setup and ZIP. · Unit Price merge 係 `origin/main` 祖先；native run 29706847786 通過並準確出 stable `native-v1.0.76`，只有原生 setup 同 ZIP。site-data 提交 `fbaff0788` 後，dispatch run 29707001900 通過並將準確 current-main SHA 嘅 `native-v1.0.77` 設為 Latest，仍然只得兩個原生 asset。

## Current 2026-07-19 native Namespaced UUID controlled integration — local gates green · 2026-07-19 原生具名空間 UUID 受控整合 — 本機 gate 已通過

- **Scope / 範圍：** `uuid5`, `uuidv5`, and `module.uuidv5` are now a pure standard-C++/C++/WinRT RFC 4122 v3/v5 renderer: DNS/URL/OID/X500/custom namespaces, managed-compatible D/N/B/P/X parsing, UTF-16 replacement, U+180E parity, local bulk rows, language retention, route reset, and explicit-only Copy.
- **Current evidence / 目前證據：** Debug and Release x64 native solution builds have 0 errors; both core suites pass **815/815**; focused UUID UIA passes **21/21**; catalog parity is **346 fixed routes + five dynamic families**, 319 registry records, and 346 ledger rows; and the native installer contract passes. Renderer accounting is **33/346 fixed routes**, **33 `in-progress` / 313 `not-started`**.
- **Visual and release state / 視覺同發佈狀態：** no callable LowLevel MCP tool is exposed in this Codex session despite the local checkout. The fresh UUID driver capture and final aggregate shell are still required before the controlled `main` push; no visual success is claimed yet. This merge changes no workflow and preserves the C++-only release publisher.

- **Final gate update / 最終 gate 更新：** the aggregate native shell now passes **469/469**. The fresh `uuid5` driver found `CopyFromScreen` unavailable and rejected the blank/near-uniform PrintWindow fallback; no PNG or root app process remains, so visual evidence is honestly `capture-blocked`. · 完整 native shell 而家通過 **469/469**。最新 `uuid5` driver 發現 `CopyFromScreen` 不可用並拒絕空白／近乎單色 PrintWindow fallback；冇 PNG 或 root app process，所以 visual 如實係 `capture-blocked`。

**粵語摘要：** 三個 UUID alias 而家係純標準 C++／C++/WinRT RFC 4122 v3/v5 renderer，有 DNS／URL／OID／X500／自訂 namespace、managed 相容 D/N/B/P/X parser、UTF-16 replacement、U+180E parity、本機 bulk 行、語言保留、route reset 同只限明確 Copy。Debug／Release 0 errors、core 各 **815/815**、UUID UIA **21/21**、catalog parity 346+5／319 registry／346 ledger 同 installer contract 已通過；renderer 係 **33/346**（**33 `in-progress` / 313 `not-started`**）。雖然有本機 LowLevel checkout，今個 Codex session 冇可呼叫工具；最新 driver 擷取同完整 aggregate shell 未完成，未會聲稱 visual success。冇改 workflow，繼續由只限 C++ publisher 發佈。
## Current 2026-07-19 native Base Converter controlled integration — local gates green · 2026-07-19 原生進位轉換受控整合 — 本機 gate 已通過

- **Scope / 範圍：** `baseconvert` and `module.baseconvert` are integrated as a genuine local C++/WinRT renderer over a dependency-free standard-C++ arbitrary-precision core. It preserves 2–36 signed conversion, grouped binary, 64-bit two's-complement display, BigInteger-compatible bitwise operations, localization/state lifecycle, accessibility, explicit-only clipboard Copy, and managed Unicode `Trim()` diagnostics.
- **Evidence / 證據：** Debug and Release x64 solution builds exit 0 with 0 errors; both combined cores pass **857/857**, including **15/15** Base Converter contracts; focused `-BaseConvertRoutesOnly -AllowClipboardMutation` UI Automation passes **14/14** across both aliases; catalog parity is **346 fixed routes + five dynamic families**, 319 registry records, and 346 ledger rows; the native installer contract passes; and renderer accounting is **36/346** fixed routes (**36 `in-progress` / 310 `not-started`**).
- **Visual evidence / 視覺證據：** the local LowLevel MCP checkout is present but no headless MCP tool is callable in this Codex session. The fresh current `driver.ps1 -Native -Page baseconvert -WaitMs 16000` attempt reported CopyFromScreen unavailable and rejected a blank/near-uniform PrintWindow client. No PNG was created, retained, replaced, or promoted, cleanup left no WinForge process, and the route is honestly `capture-blocked`, not visual-pass.
- **Boundary / 界線：** this integration changes no `.github` workflow or release policy; the next step is a controlled `main` commit/push and verification of the existing C++-only release flow.

**粵語摘要：** `baseconvert` 同 `module.baseconvert` 已整合成真正本機 C++/WinRT renderer 同唔靠依賴嘅標準 C++ 任意精度 core；保留 2–36 有符號轉換、二進制分組、64-bit 二補數、BigInteger 相容 bitwise、本地化／狀態 lifecycle、無障礙、只限明確 Copy 同 managed Unicode `Trim()` 診斷。Debug／Release 0 errors、合併 core 各 **857/857**（Base Converter **15/15**）、兩個 alias UIA **14/14**、catalog parity 346+5（319 registry／346 ledger）、installer contract 通過，renderer 係 **36/346**（**36 `in-progress` / 310 `not-started`**）。LowLevel MCP 今個 session 不可呼叫；最新目前 driver 因 CopyFromScreen 不可用而拒絕空白／近乎單色 PrintWindow client，冇 PNG／冇 process，所以 visual 如實係 `capture-blocked`。整合冇改 `.github`／release policy；下一步係受控 `main` commit／push 同驗證只限 C++ release flow。

## Current 2026-07-19 native Slugify controlled integration — hosted release proven · 2026-07-19 原生網址別名受控整合 — hosted 發佈已證明
## Historical 2026-07-19 native Unit Price feature branch (pre-integration) · 歷史原生單位價格功能分支（整合前）

- **Scope / 範圍：** `priceper`, `unitprice`, and `module.unitprice` are a dedicated C++/WinRT renderer over pure standard-C++ `UnitPrice`. It preserves managed valid-row filtering, free/infinity and tolerance-tie decisions, invariant formatting, first-unit Add, removal/release/reset lifecycle, three-language state retention, and explicit-only clipboard Copy. No CLR host, managed-app launch, IPC delegation, workflow, or release-policy change is included.
- **Verification / 驗證：** native Debug and Release x64 solution builds exit 0 with 0 errors; Debug and Release core each pass **814/814**, including Unit Price **13/13**; catalog parity passes **346 fixed routes + five dynamic families**; and focused `-UnitPriceRoutesOnly -AllowClipboardMutation` UI Automation passes **15/15** across every alias. Renderer accounting is **33/346 fixed routes**, **33 `in-progress` / 313 `not-started`**.
- **Full-shell disposition / 完整 shell 狀態：** a broad run passed the full Unit Price assertion block but later stalled silently in unrelated CSS Unit Converter work. The integration owner directed an orderly stop of only the owned WinForge/smoke processes, so this is explicitly **not** a completed full-shell result; the authoritative full shell must run after controlled integration.
- **Visual / 視覺：** the local LowLevel checkout is present but no headless MCP tool is callable in this session. The required driver found `CopyFromScreen` unavailable and rejected a blank/near-uniform `PrintWindow` fallback. No PNG or canonical screenshot changed, no process…6303 tokens truncated…0 errors; XAML literal safety passed; full owned shell **300/300**, strengthened utility shell **39/39**, and catalog parity **346 + five families** passed. A deterministic one-million-finite-double .NET 11 differential found zero Aspect Ratio display-format mismatches.
- **Headless visual status / 無頭視覺狀態：** LowLevel Computer Use MCP 1.28.1 launched all three routes from one immutable 294-file runtime snapshot on separate named desktops, confirmed each exact launch PID, resolved one 1320×880 WinUI frame per route, captured and inspected full/client frames, killed each PID, and closed each desktop. Every 1304×841 client frame was one white color with zero standard deviation/non-white fraction. The repository driver separately launched each route and rejected the same blank `PrintWindow` fallback when `CopyFromScreen` was unavailable. The six invalid PNGs and immutable stage were deleted; no canonical image was replaced, so all three rows are honestly `capture-blocked`.
- **Branch release proof / 分支版本證明：** hosted native run [29663954724](https://github.com/Ding-Ding-Projects/WinForge/actions/runs/29663954724) passed every build/test/parity/package/installer-smoke gate and published [native-v1.0.37](https://github.com/Ding-Ding-Projects/WinForge/releases/tag/native-v1.0.37). Its tag ref and `target_commitish` both resolve exactly to `ce879cc6626eae328ec72e0143761c0edfbae340`; `WinForge-Native-Setup.exe` and `WinForge-native-x64-1.0.37.zip` are present with recorded SHA-256 digests.
- **Remote integration proof / 遙距整合證明：** after the final main push and fetch, verify `828c3279` and `ce879cc6` as ancestors of `origin/main`, confirm the expected core/app/tests/smoke/generator/docs/parity/handoff paths from the remote main tree, and only then delete `codex/native-utility-four` locally/remotely.

**粵語摘要：** 文字差異比對、長寬比計算同 CSS 單位換算已經真正原生化，功能／測試／文件／分支版本證據完成；三頁喺指定 LowLevel MCP 同 repository driver 都成功開啟，但呢個 desktop session 冇 WinUI composition，client frame 全白，所以視覺如實係 `capture-blocked`。今批完成後仍有 **325 條固定 route** 加五組動態家族要繼續移植。


## Native App Uninstaller integration record / 原生 App 解除安裝器整合記錄

- **Task commit / 任務提交：** 20fd3bb5813ade9056b1215de25473aeaa72660c.
- **Merge commit / 合併提交：** 477d2b2691e6c99a4b0de5237b6ed92ed70fc09e.
- **Scope / 範圍：** native current-user Store/UWP inventory, cache-only literal/PCRE2 Regex filtering, reviewed Confirm removal, and normal-integrity fail-closed protection; no deep cleanup or local-data deletion.
- **Evidence / 證據：** Debug/Release core 417/417, native Debug build 0 warnings/0 errors, catalog parity passed. LowLevel off-screen UI is honestly blocked by a blank WinUI client and missing NativePageTitle after 30 seconds; it never falls back to the visible desktop.
- **Remote proof / 遠端證明：** after fetch, task commit, pushed feature tip, and merge were ancestors of origin/main, with source, tests, docs, Pages mirror, and headless harness present. Detailed record: handoff-app-uninstaller.md and design/content/handoff-app-uninstaller.md.

**粵語：** 呢個 native slice 冇 deep cleanup 或本機資料刪除；Debug/Release core 各自 417/417。Cheap LowLevel off-screen WinUI frame 空白，headless UI 證據如實受阻，絕不回退去可見桌面。

## Native installer CI integration record · 原生安裝程式 CI 整合紀錄

- **Task commit / 任務提交：** b5cae63dd53e1892aca61e039597d1f3b9a6b73c.
- **Merge commit / 合併提交：** 1c3c9a1a.
- **Scope / 範圍：** reusable native installer contract verification at staged runtime, compiled Inno Setup executable, and installed payload boundaries; exact setup-output enforcement; CI documentation and Pages mirrors.
- **Evidence / 證據：** local static installer contract and three-gate workflow-wiring checks passed. The hosted Windows 2022 CI owns Inno Setup compilation and silent lifecycle execution.
- **Remote proof / 遙距證明：** after fetch, the task commit, pushed feature tip, and merge commit are ancestors of origin/main. The workflow, verifier, documentation, Pages mirrors, generated site data, and handoff memory exist in the remote main tree.
- **粵語摘要：** task、已推送 branch tip 同 merge commit 都係 origin/main ancestor；workflow、verifier、docs、Pages mirror、site data 同 handoff memory 都已確認喺 remote main。


## Native Symbols Palette integration record · 原生特殊符號調色盤整合紀錄

- **Task commit / 任務提交：** ba1a6c6192c1a150e35ebf09c0242d4c1d686177.
- **Merge commit / 合併提交：** 04a593f8.
- **Branch / 分支：** codex/native-symbols-palette was pushed and merged; cleanup is permitted only after this verified-memory commit is pushed and rechecked.
- **Remote proof / 遙距證明：** after fetch, the task commit, pushed feature tip, and merge commit are ancestors of origin/main. The native source, tests, docs, Pages mirror, capture status, and handoff records exist in the remote main tree.
- **粵語摘要：** 任務提交、已推送分支 tip 同合併提交 fetch 後全部係 origin/main ancestor；原生 source、tests、docs、Pages mirror、capture status 同 handoff memory 都喺 remote main。
- **Scope / 範圍：** native C++ catalog and C++/WinRT page for 226 local symbols, bilingual categories, safe literal/PCRE2 search, explicit Copy, and Regex Builder handoff.
- **Evidence / 證據：** core tests 411/411 in Debug and Release; owned LowLevel MCP UI Automation 238/238; catalog parity passed.
- **Visual status / 視覺狀態：** capture-blocked. The isolated driver rejected its blank/near-uniform fallback, so no screenshot is claimed or retained.
- **Detailed task memory / 詳細任務記憶：** handoff-symbols.md and design/content/handoff-symbols.md.


## Latest integration record — 2026-07-16

**Native Regex Tester all-match and replacement continuation / 原生 Regex Tester all-match 及 replacement 延續**

- `module.regextester` now uses the native bounded PCRE2 core to enumerate up to **100** non-overlapping matches, keep named capture metadata, safely progress zero-length matches under one shared deadline, and preview local replacements. PCRE2 `(x)` extended whitespace and `(n)` named-capture-only flags travel through the selected Shell, All Apps, cache-only Package Discover, or Regex Cheatsheet target.
- The replacement preview is deliberately not full .NET compatibility: it accepts only `$$`, existing `$0`–`$99`, and `${name}`, and invalid replacement text or the 32 KiB output cap fails closed without applying a target. Package Discover remains local-cache-only and never sends a pattern to argv or HTTPS.
- Evidence before integration: Debug and Release native suites each passed **403/403**; isolated LowLevel MCP headless UI Automation passed **226/226** for flags, all-match rows/named captures, valid/invalid replacement preview, the cap, target Apply, accessibility, and clipping. The inspected 852×880 full-window and 836×841 client-only captures were blank and discarded, so visual evidence is honestly `capture-blocked`.
- Git integration (verified): task commit `72ce549110b3d235b406de397736e89ecbcdb055` and remote feature tip `72ce549110b3d235b406de397736e89ecbcdb055` merged into `main` as `f7cba1a4694df705cd483868755af079e6250fda`. After fetch, all three commits were proven ancestors of `origin/main`, and the implementation, tests, docs, Pages mirrors, parity ledger, and these handoff files were confirmed in the remote main tree before cleanup.

**原生 Regex Tester all-match 及 replacement 延續 / Native Regex Tester all-match and replacement continuation**

- `module.regextester` 而家用原生有界 PCRE2 core 列舉最多 **100** 個非重疊相符、保留命名 capture metadata、喺同一個 deadline 下安全處理零長度相符，並預覽本機 replacement。PCRE2 `(x)` 忽略 pattern 空白同 `(n)` 只保留命名 capture 旗標會跟住已揀 Shell、All Apps、只限快取嘅 Package Discover 或 Regex Cheatsheet target。
- Replacement preview 刻意唔係完整 .NET 相容：只接受 `$$`、存在嘅 `$0`–`$99` 同 `${name}`；無效 replacement 或 32 KiB output cap 都會 fail closed，唔會套用 target。Package Discover 保持只限本機快取，絕對唔會將模式傳去 argv 或 HTTPS。
- 整合前證據：Debug 同 Release 原生 suite 都通過 **403/403**；isolated LowLevel MCP headless UI Automation 通過 **226/226**，覆蓋旗標、all-match rows／命名 capture、有效／無效 replacement preview、cap、target Apply、accessibility 同 clipping。852×880 full-window 同 836×841 client-only 截圖係空白、已丟棄，所以視覺證據如實係 `capture-blocked`。
- Git 整合（已驗證）：task commit `72ce549110b3d235b406de397736e89ecbcdb055` 同 remote feature tip `72ce549110b3d235b406de397736e89ecbcdb055` 已經以 `f7cba1a4694df705cd483868755af079e6250fda` 合併入 `main`。fetch 後已證明三個 commit 都係 `origin/main` 嘅 ancestor，清理前亦已確認 implementation、tests、docs、Pages mirrors、parity ledger 同呢兩份 handoff file 都喺 remote main tree。

## Latest continuation record — 2026-07-16

**Native Regex Cheatsheet / 原生 Regex 速查表**

- `module.regexcheat` is now a real C++/WinRT route, not a pending page. Its pure-C++ immutable catalog preserves 67 bilingual reference rows in nine categories and eight copy-only ready-made patterns. .NET-only reference syntax stays documentation; only an explicitly enabled, bounded PCRE2 local filter is evaluated.
- The native builder now targets this fourth registered local search surface. Invalid filters retain the preceding visible rows; static reference data never reaches a command line, package engine, network, or process. Clipboard writes require an explicit Copy button.
- Evidence: Debug and Release native suites each passed **395/395**; catalog parity passed 346 fixed routes, five dynamic families, 319 registry entries, and 22 categories; the isolated LowLevel MCP headless UI Automation shell passed **224/224**, including Cheatsheet filtering, invalid-pattern retention, explicit Copy, builder handoff, and horizontal bounds.
- Visual evidence is honestly `capture-blocked`: inspected LowLevel MCP full-window **852×880** and client-only **836×841** frames had a title bar/blank client and a blank client respectively. Both temporary PNGs were discarded; no stale, synthetic, or managed substitute was used as native proof.
- Git integration (verified): task commit `24f32ba85eade7244dc839760807ea3ea3d1a5d9` merged as `2872b234022188d70f250fdbae3d78a740f68fa8`; after fetch, both the task commit and `origin/codex/native-regex-cheatsheet` tip were proven ancestors of `origin/main`, with the implementation, docs, and memory files present in the remote tree before cleanup.

**原生 Regex 速查表 / Native Regex Cheatsheet**

- `module.regexcheat` 而家係真正嘅 C++/WinRT route，唔再係 pending page。純 C++ 不變 catalog 保留 67 項雙語參考、九個分類同八個只可明確複製嘅現成模式。
- 速查表成為第四個已註冊嘅本機 regex 搜尋 surface；只有明確開啟時先會以有資源限制嘅 PCRE2 篩選靜態文字。無效模式會保留原有結果，唔會送去命令列、套件引擎、網絡或者程序。
- 驗證：Debug/Release 都通過 **395/395**；catalog parity 通過；隔離 LowLevel MCP headless UI Automation 通過 **224/224**。852×880 full-frame 同 836×841 client-frame 都係空白客戶端，所以已丟棄，視覺證據係 `capture-blocked`。

## Project

**Repository:** WinForge  
**Current completion state:** Major launcher, companion apps, updater, reactor, and security hardening work completed.

## Git State

Final pushed state:

- `main`: `5aab5e5`
- Feature branch: `codex/finish-companions-reactor-p3`
- Feature commit: `f2a054e`
- Working tree: clean
- Feature branch merged into `main`
- `main` pushed successfully

The repository should be continued from `main`.

---

# Completed Work Summary

## 1. Companion App System

Implemented and hardened the companion application architecture.

Completed:
- Native companion launch support.
- Companion installation flow.
- Companion window management.
- Secondary window reuse.
- Safer process launching.
- Better failure handling.
- Explicit install state handling.

Fixed:
- False install success reporting.
- Race conditions when opening companion windows.
- Unsafe external process behavior.
- Elevated execution issues.

---

# Native Companion Fixes

## Problem

The native ImageForge/AudioForge companions built successfully but failed on machines missing MinGW runtime DLLs.

Affected runtime dependencies:
- `libgcc_s_seh-1.dll`
- `libwinpthread-1.dll`

## Resolution

Updated native build configuration:

- Added full static runtime linking.
- Removed dependency on external MinGW runtime DLLs.
- Verified resulting binaries only depend on Windows system libraries.

Validated:
- Native editor builds.
- Native editor launches from WinForge.
- No missing DLL dialogs.

---

# App Launcher

Completed launcher improvements.

Implemented:

- Launcher hub.
- Companion discovery.
- Install flow.
- Explicit installation state.
- Better launch error handling.
- Improved module navigation.
- Better secondary window lifecycle.

Validated:
- Launcher opens correctly.
- Modules load correctly.
- Companion routes work.

---

# Reactor Simulation

## Completed Fixes

The reactor simulation had a full-power thermal balance issue.

Fixed:

- Thermal equilibrium calculations.
- High-power stability behavior.
- Sustained operating plateau handling.

Added/improved:
- Reactor documentation.
- Operating procedure documentation.
- Emergency scenario documentation.
- Test reporting.

Validation:
- Reactor reaches stable high-power operation.
- No runaway thermal behavior in tested scenarios.

---

# Security Hardening

## Archive Extraction

Fixed:
- Archive traversal vulnerabilities.

Added:
- Safe extraction path validation.
- Protected extraction boundaries.

---

## Elevated Execution

Fixed:
- User-writable executable execution risk while elevated.

Added:
- Refusal of unsafe elevated native compilation.
- Safer launch behavior.

Applications now avoid inheriting unnecessary administrator privileges.

---

## Web Bridge

Hardened:

- Origin handling.
- Payload size limits.
- Save operation handling.
- Cancellation behavior.

---

## Diagram Import

Fixed:

- Unsafe imported IDs being inserted into SVG.

Added:
- Sanitization of imported identifiers.

---

## Admin Detection

Improved:

- Elevation checks.
- Fail-closed behavior when inspection fails.

---

# Updater

## Completed Updater Hardening

Implemented:

- SHA-256 verification.
- Side-by-side updater runtime.
- External updater helper.
- Mutex protection.
- Bounded download handling.
- Persistent updater logs.
- Legacy bootstrap recovery.

---

# Installer Fixes

Resolved:

- Installer exit code 3 handling.
- Bootstrap/relaunch issues.
- Update handoff failures.

Updated:
- Installer script.
- Launcher update recovery path.
- Updater startup flow.

---

# Logging

Added/improved:

- Persistent logs.
- Update diagnostics.
- Failure visibility.

---

# Build Validation

Completed:

- WinForge build.
- Launcher build.
- Updater build.
- Native companion build.
- Integration validation.

Important validation results:

- 0 build errors.
- Native companions launch successfully.
- Updater builds successfully.
- Git checks passed.

---

# UI Validation

Completed checks:

## WinForge Launcher
Passed:
- Application startup.
- Module loading.
- Launcher UI rendering.

## Image Editor
Passed:
- Module opening.
- Native editor launch path.
- Runtime dependency validation.

## CodeForge
Passed:
- First-run installation path testing.
- Monaco install security path validation.

---

# Run Skill / Automation Updates

Updated:

`.agents/skills/run-winforge`

Changes:
- Better publish failure handling.
- Stops stale WinForge processes before publishing.
- Avoids continuing after failed builds.
- Improved validation reliability.

Desktop automation was intentionally stopped before completion to avoid interfering with active applications.

---

# Deferred Request: Task Scheduler Auto Start

A request was made:

> Add Task Scheduler auto-run without UAC.

Decision:

Not implemented.

Reason:
- Creating a privileged scheduled task to bypass UAC would weaken Windows security.
- It could create a persistence/elevation risk.

Current behavior:
- Runs at normal user integrity.
- No UAC bypass.
- No hidden privileged startup.

Possible future safe alternative:
- Normal-user scheduled task.
- Startup shortcut.
- User-approved background service design.

---

# Continuation Update — Visible First-Run Compiler UX

Completed and committed on 2026-07-09:

- Expanded the native companion preparation popup into a resizable, bilingual terminal-style build window.
- Added separate phase/status UI, indeterminate progress, live batched stdout/stderr, selectable scrollback,
  Retry/Close states, and stable automation IDs.
- Blocked title-bar close while preparation is active. Cancel now waits for compiler process-tree cleanup before
  the window closes; a bounded cleanup-timeout state prevents an unclosable trap, disables unsafe Retry, and
  quarantines later native builds in that WinForge process until restart. Native preparation is process-wide
  serialized so a second companion cannot overlap cleanup or race the quarantine transition.
- Moved compiler discovery off the UI thread and made its filesystem/vswhere probes cancellation-aware.
- Added durable per-attempt logs under `%LOCALAPPDATA%\WinForge\logs\companion-builds`, with UTF-8 output,
  per-companion retention, log-folder access, complete result diagnostics, and fail-open disk-error handling.
- Preserved the prebuilt/source-hash cache fast paths, temporary-exe cleanup, atomic publication, normal-integrity
  execution, and static MinGW linking.
- Added `tests/CompanionBuildLog.Tests` and registered it in `WinForge.sln`.

Validation completed:

- `dotnet build WinForge.sln -c Debug -p:Platform=x64` — 0 errors.
- `dotnet run --project tests/CompanionBuildLog.Tests -c Debug` — 4/4 passed.
- Self-contained publish and Image Editor module render — passed.
- Injected compiler failure — live stdout/stderr, blocked close, failure UI, Retry, and persistent log passed.
- Explicit Cancel — compiler exited before the preparation window closed; cancellation log passed.
- Genuine MSVC build — ImageForge compiled, cached, launched, and logged `SUCCESS`; prior cache was restored.

---

# Remaining Work / Future Tasks

## Updater UX Improvements

Potential improvements:

- Better progress display.
- Retry button.
- Detailed error messages.
- Update history.
- Recovery diagnostics.

---

## Logging Improvements

Potential:

- Central application log viewer.
- Export diagnostics bundle.
- Log rotation.
- Crash reporting.

---

# Important Development Notes

- Continue from `main`.
- Do not reset to old feature branches.
- Existing companion/security work is already merged.
- Avoid reintroducing elevated auto-start behavior.
- Preserve static native linking.
- Keep updater verification and integrity checks.

---

# Recommended Next Session Start

1. Review the committed visible compiler/log UX changes.
2. Re-run:
   - `git status`
   - build validation.
3. Continue with updater UX improvements (retry, richer errors, update history/recovery diagnostics).
4. Consider a central application log viewer and diagnostics-bundle export.

End state: WinForge is in a completed hardened state with remaining work focused mainly on UX improvements.
# Current synchronized Regex Builder core — 2026-07-24 · 目前同步正則砌法核心

- **Scope / 範圍：** reusable `SearchPatternSession` + `Controls/SearchPatternBox` now keep the compact query, raw .NET pattern, explicit regex mode, `IgnoreCase`/`Multiline`/`Singleline`/`IgnorePatternWhitespace`/`ExplicitCapture`, validation, session-only sample, live matches/captures, guided construction, and explicit copy synchronized with the exact state driving real results. Dashboard, Category, Search Results, Manual, App Launcher, Licenses, Native OSS Hub, and Settings Hub are integrated. Plain text remains the default. · 可重用 session／控制會將精簡 query、原樣 .NET pattern、明確 regex mode、五旗標、驗證、只限今次 session sample、即時配對／擷取、引導砌法同明確 copy，同真正結果狀態同步；八個核心／共用介面已整合，純文字保持預設。
- **Safety / 安全：** patterns cap at 4,096 characters and candidates at 1,000,000; each result refresh compiles once and applies a 250 ms per-candidate timeout. A timed-out matcher is poisoned for the remainder of its batch. Dedicated tester caps/timeouts remain 4,096/1,000,000/65,536, 2,000 results, one second, safe zero-width progress, and conservative replacement work. Nothing is persisted, transmitted, executed, or placed on a command line. · Pattern／候選有界、每次 refresh 只編譯一次、每候選 250 ms 超時；一超時就令餘下 batch 即時 fail closed。專用 tester 原有上限／安全界線不變；唔保存、傳送、執行或者放入命令列。
- **Inventory / 清單：** `tools/New-SearchSurfaceInventory.ps1` scans every source XAML file and classifies **93 controls across 74 files**: 8 integrated, 64 applicable ordinary searches retained, 9 specialized dialects requiring adapters, 7 dedicated pattern tools, 2 read-only outputs, and 3 shared-control internals. BPF, Wireshark display filters, JMESPath, provider queries, hex/encoding search, rename transforms, and logcat semantics are never silently relabelled as .NET regex. · 生成清單覆蓋全部 source XAML；八個已整合、64 個一般搜尋保留、九個專用方言要 adapter、七個 pattern 工具、兩個唯讀輸出、三個共用控制內部欄位，專用方言絕不靜靜雞改做 .NET regex。
- **Verification / 驗證：** focused `RegexBuilder.Tests` passes **33/33** on the branch after merging `origin/main` `b0828ada5d0ac501fc1f33f42c3135961675517d`; XAML literal safety passes; and the detailed source audit reports 337 XAML files, 2,907/2,907 resolved handlers, 2,020 action controls, zero lifecycle mismatches, and zero actionable markers. The immediately preceding post-placeholder project build and full self-contained publish passed with **0 warnings / 0 errors**. An explicit stop request arrived before the exact post-merge solution build could be rerun, so that combined build is not claimed here. · 合併 `origin/main` 後，專項 **33/33**、XAML literal safety 同詳細 source audit 全過；337 個 XAML、2,907/2,907 handler resolve、2,020 個 action control、零 lifecycle mismatch／actionable marker。合併前最後一次 project build／完整 self-contained publish 係 **0 warning／0 error**；收到明確停止要求後冇再跑完整 post-merge solution build，所以呢度唔會冒稱已驗證。
- **Visual evidence / 視覺證據：** inspected canonical compact-row captures are LowLevel 852×646 `docs/screenshot-regex-search-core.png` (SHA-256 `A87DF8C69D9B9C37F962CF38B719D759E8EDB5D08065093ED8E75F92235BD529`) and app-owned 760×720 `docs/screenshot-regex-search-core-narrow.png` (`EBEA49C39D3DAC882494AE3546EE754B760AAAD5561944E375CB0FC94B557EC5`). The narrow row exposes the active regex state and direct builder action without clipping. A live flyout audit found the two-column-flag and long-placeholder defects and directly produced one-column flags plus the short sample prompt; the final complete flyout is source/test locked because app-owned root capture excludes transient popup content and later `PrintWindow` frames were black. Exact PID 16492/HWND 93521508 closed, LowLevel returned zero windows/processes, and desktop `WinForgeRegexCoreOwnedCapture` closed true. · 已檢視正式正常／窄畫面精簡列圖；active regex 同 builder action 冇裁切。live flyout 審核搵到並修正雙欄旗標／長 placeholder；app-owned root 唔包 transient popup，而之後 `PrintWindow` 係黑畫面，所以完整 flyout 最終以原始碼／測試鎖實，冇冒充視覺證據。準確 PID／HWND／desktop 全部清理完成。
- **Remaining global work / 餘下全域工作：** the roadmap stays unchecked for 64 ordinary surfaces plus 9 explicit adapters. The 7 dedicated tools, 2 outputs, and 3 component internals are not product-search migrations. · 路線圖保持未剔，保留 64 個一般介面同九個明確 adapter；其餘專用工具／輸出／component internals 唔係 product-search migration。
- **Delivery boundary / 交付界線：** feature commit `68e1fc7802a8eb522cec092ab218c2952c7f1ae2` and merge commit `cf26b7dd` are preserved on `codex/regex-search-core`. This source branch is for the parent integration agent to merge and clean; this lane does not merge into `main`, delete the branch/worktree, or claim hosted CI success. · 功能 commit 同最新 `main` 合併 commit 都保留喺 `codex/regex-search-core`；由上層整合 agent 負責合併／清理，呢條 lane 唔會自行 merge `main`、刪 branch／worktree，亦唔會預先聲稱 hosted CI 成功。

## Optional nuclear feature power and emergency diesel — 2026-07-27 · 可選核電功能同應急柴油

- **Scope / 範圍：** Nine playful feature gates now prefer healthy live nuclear power but can use an explicitly enabled simulated emergency-diesel feature bus. Permission is persisted and defaults OFF; every app session begins with the generator stopped and its 60 L tank empty. The operator must fill while stopped, start manually, and wait 10 seconds. Starting/running burns 1.0 L/min. The 250 MWe EDG supplies at most two exact owner-token module tabs/instances, with atomic contention and release on close/navigation/power loss. The other 19 reactor-industrial simulations remain nuclear-only. · 九個玩味功能閘門優先用健康即時核電，但可以明確啟用模擬應急柴油功能匯流排。權限會保存兼預設 OFF；每次 app session 都由停機、60 L 空缸開始，操作員要停機時入油、手動啟動再等 10 秒。啟動／運行每分鐘燒 1.0 L；250 MWe EDG 同時最多供應兩個準確 owner-token 分頁／instance，並行爭位係原子操作，關頁／轉頁／失電會釋放。其餘 19 個反應堆工業模擬繼續只限核電。
- **Continuous Cake and reactor authority / 蛋糕持續用電同反應堆 authority：** Cake Factory continuously checks its exact owner lease. Losing fallback preserves the live page and plant model; powered machinery, commands, animations, and active CIP progress freeze exactly, while passive biology, spoilage, transport, and order clocks continue. Reactor pages share one canonical session: the newest visible page is the sole driver, parallel pages are live read-only observers, mutating companions close on demotion, and any real-shutdown countdown aborts before authority handoff. Page-owned audio, Awake, Home Assistant, SystemLink, and shutdown effects end with the last visible owner while background physics/API remain truthful. · 蛋糕工廠持續檢查自己準確 lease；失去後備電會保留即時頁面／廠房模型，有電機器、指令、動畫同進行中 CIP 準確凍結，被動生物、變壞、運輸同訂單時鐘繼續。反應堆頁共用一個正式 session；最新可見頁係唯一 driver，其他係即時唯讀 observer，降級會關閉可改動 companion，真實關機倒數會喺 authority 交接前自動中止。最後可見 owner 離開時，頁面擁有嘅音效、Awake、Home Assistant、SystemLink 同關機效果會結束，但背景物理／API 繼續如實更新。
- **Headless-only operation / 只限無頭操作：** Repository instructions and `.agents/skills/run-winforge/driver.ps1` now require a unique non-input Win32 desktop, `CREATE_NO_WINDOW`, `SW_SHOWNOACTIVATE`, bounded automation data, app-owned capture with targeted `PrintWindow` only as a validated fallback, exact process/window ownership, and paired cleanup. Direct visible launches and terminal popups are prohibited. The existing unrelated WinForge PID 37896 was never touched. · Repo 指引同 driver 而家強制唯一非輸入 Win32 desktop、`CREATE_NO_WINDOW`、`SW_SHOWNOACTIVATE`、有限 automation data、自家 capture（只可用已驗證 targeted `PrintWindow` 後備）、準確 process／window ownership 同成對清理；禁止直接可見啟動同終端彈窗。現有唔相關 WinForge PID 37896 全程冇郁過。
- **Verification / 驗證：** Exact current source passes `dotnet build WinForge.sln -c Debug -p:Platform=x64 -m:1` with **0 warnings / 0 errors**, XAML literal safety, **10/10** Reactor Settings lifecycle contracts, and **67/67** reactor/dependent scenarios with exit 0. The generated references contain 322 modules and 1,948 button pages; Pages data contains 322 modules, 22 categories, 1,217 features, and 2,335 wiki records. Final adversarial review found no remaining implementation blocker. The final open-issue scan returned zero issues. · 準確目前 source 嘅 solution build **0 warning／0 error**、XAML literal safety、反應堆設定生命週期 **10/10**、反應堆／依賴情景 **67/67** 全過並以 0 退出。生成 reference 有 322 modules／1,948 button pages；Pages data 有 322 modules／22 categories／1,217 features／2,335 wiki records。最終對抗 review 冇剩餘實作 blocker；最終 open issue 掃描係零。
- **Visual evidence / 視覺證據：** Eight inspected real-build captures cover the corrected two-row Reactor toolbar; Reactor Settings default-off, enabled-empty, starting, and running/fuel telemetry; no-source recovery; search requirement badge; and EDG-powered Cake Factory. The 1558×878 app-owned captures and 1574×887 fresh-HWND LowLevel captures were made only on dedicated off-screen desktops; every owned app/helper/desktop was closed afterward. Canonical SHA-256 values are recorded beside the files in the 2026-07-27 task Discussion update. · 八張已檢視真實 build 截圖覆蓋修正後兩行反應堆工具列、反應堆設定預設關閉／已容許空缸／啟動中／運行油量、冇電源復原、搜尋要求徽章，同 EDG 供電蛋糕工廠。1558×878 app-owned 同 1574×887 fresh-HWND LowLevel 圖只喺專用離屏桌面產生；全部自家 app／helper／desktop 之後已關閉。正式 SHA-256 會記喺 2026-07-27 task Discussion update。
- **Delivery and external handoff / 交付同外部交接：** Rolling progress is Discussion [#10](https://github.com/Ding-Ding-Projects/WinForge/discussions/10). Feature commit [`b28b3dd3b71661c906edbadc94eb28ba705b80e5`](https://github.com/Ding-Ding-Projects/WinForge/commit/b28b3dd3b71661c906edbadc94eb28ba705b80e5) was pushed on `codex/optional-reactor-power`, then merged non-fast-forward as [`1bc45a663f3f8f8bacad2997c25ad2d047b5273c`](https://github.com/Ding-Ding-Projects/WinForge/commit/1bc45a663f3f8f8bacad2997c25ad2d047b5273c). The merge tree exactly matched the verified feature tree `bc8a7db49d2c1fac65167bd52f3757c4c928b605`, and immediate `ls-remote` proved `origin/main` at the exact merge SHA. Managed [run 30304844226](https://github.com/Ding-Ding-Projects/WinForge/actions/runs/30304844226), site-data [run 30304844229](https://github.com/Ding-Ding-Projects/WinForge/actions/runs/30304844229), and Pages [run 30304844234](https://github.com/Ding-Ding-Projects/WinForge/actions/runs/30304844234) were queued/running at this record and are not claimed successful. GitHub Projects could not be updated because the active token has `repo`, `workflow`, `gist`, and `read:org` but lacks `read:project` / `project`; no existing Project item was changed. Discussion pinning is not exposed by the available GitHub GraphQL schema, so no pin state was changed. · 滾動進度係 Discussion #10；功能 commit 已 push，並以 non-fast-forward merge 準確整合到 `main`。Merge tree 同已驗證 feature tree 完全一致，`ls-remote` 即時證明 remote `main` 係準確 merge SHA。三個 hosted workflow 喺記錄時排隊／運行中，未冒稱成功。現用 token 冇 Project scopes，所以冇改任何 Project item；可用 GitHub GraphQL schema 亦冇 Discussion pin 操作，所以冇改 pin 狀態。
- **Final proof and cleanup / 最終證明同清理：** Integration-record commit [`2ab52400244636bd34955c41b51dd711a961eb88`](https://github.com/Ding-Ding-Projects/WinForge/commit/2ab52400244636bd34955c41b51dd711a961eb88) was pushed and fetched. The feature, merge, and record commits were all proven ancestors of `origin/main`; required implementation, instruction, documentation, and screenshot paths were read directly from the remote tree. Pages [run 30304913877](https://github.com/Ding-Ding-Projects/WinForge/actions/runs/30304913877) succeeded, while managed [run 30304913934](https://github.com/Ding-Ding-Projects/WinForge/actions/runs/30304913934) was still running at this check. Only after the ancestry and remote-file proof, `codex/optional-reactor-power` was deleted locally and remotely. The final checkout had one `main` worktree, zero stashes, zero open issues, no task branch, a clean worktree, and `main...origin/main` divergence `0/0`. · 整合記錄 commit `2ab52400` 已 push 再 fetch；功能、merge 同記錄 commit 全部證明係 `origin/main` ancestor，亦直接由 remote tree 讀到所需實作、指引、文件同截圖。Pages run 已成功，managed run 喺檢查時仍運行中。只喺 ancestry／remote-file 證明完成後先刪本機同 remote task branch；最終只剩一個乾淨 `main` worktree、零 stash、零 open issue、零 task branch，同 remote 分歧 `0/0`。
