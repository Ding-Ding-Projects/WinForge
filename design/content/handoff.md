# WinForge — Handoff reference (per feature) · 交接參考（逐項功能）

## Native Morse Code feature branch — verification complete; integration pending · 原生摩斯電碼功能分支 — 驗證完成；整合待處理

**EN.** module.morse and alias morse now have a genuine C++/WinRT renderer backed by standard-C++ Morse logic. It preserves managed-compatible UTF-16 encode/decode aliases, unique unknown-symbol reporting, bounded WPM and flash timing, explicit Copy, and lifecycle-safe dispatcher-timer cleanup. Native Debug/Release builds exit 0; both core suites are **741/741**, Morse is **24/24**, focused native UIA is **13/13**, and catalog parity covers 346 fixed routes plus five dynamic families. This isolated feature branch records **28/346** fixed routes and **28 in-progress / 318 not-started**. Feature commit 66bb415e5f1730bb128f4d649e493150da222d06 is pushed to origin/codex/native-morse; it is intentionally unmerged while the parent owns rebase, main integration, hosted CI, and native-only release proof.

**Visual evidence.** LowLevel MCP is present locally but not callable in this Codex session, so none is claimed. The required driver successfully launched the native page but rejected its blank/near-uniform fallback because CopyFromScreen is unavailable. No invalid image was retained or promoted; visual evidence remains capture-blocked.

**粵語.** module.morse 同 morse 而家有真正 C++/WinRT renderer 同標準 C++ Morse 邏輯，保留 managed 相容 UTF-16 編碼／解碼別名、唯一未知符號報告、有界 WPM 同閃燈計時、明確 Copy 同 lifecycle-safe dispatcher-timer cleanup。Debug／Release 各通過 **741/741**、Morse **24/24**、專項 native UIA **13/13** 同 catalog parity；呢個獨立 feature branch 記錄 **28/346** 同 **28 in-progress / 318 not-started**。Feature commit 66bb415e5f1730bb128f4d649e493150da222d06 已推送到 origin/codex/native-morse，而家特登未合併；父 agent 擁有 rebase、main 整合、hosted CI 同 native-only release 證明。LowLevel MCP 雖然喺本機但呢個 session 唔可呼叫；driver 開到原生頁但因 CopyFromScreen 唔可用而拒絕空白／近乎單色 fallback。無效圖冇保留或提升，visual 保持 capture-blocked。

## Native text-analysis wave — exhaustive shell green; visual capture blocked · 原生文字分析批次 — 完整 shell 通過；視覺擷取受阻

**EN.** Text Statistics, Word Frequency, and String Compare now have dedicated C++/WinRT renderers over the shared standard-C++ `TextAnalysis` core. The implementations preserve local UTF-16/CJK statistics, sentence/paragraph/average/time/Flesch/top-ten results; word/bigram/Unicode-scalar frequency, managed options/stop words, current-culture ordering, count/bar/percentage rows and exact CSV; and normalization, UTF-16, OSA/Jaro–Winkler/longest-common metrics, the greater-than-2,000 guard, subquadratic exact greedy Jaro, and route-exit cleanup. Word Frequency and String Compare write the clipboard only after explicit Copy; Text Statistics is side-effect free.

Renderer accounting is **27/346 fixed routes**, with **319 fixed routes** plus five dynamic families remaining; the ledger is **27 `in-progress` / 319 `not-started`**. Debug/Release core is **714/714** each, focused Text Analysis **84/84**, managed Structured Text Tools **7/7**, seven-alias UIA **40/40**, utility **39/39**, exhaustive shell **388/388 (388 passed, 0 failed)**, catalog parity, and managed build 0 errors. LowLevel MCP 1.28.1 launched the three canonical pages on named private desktops, resolved 1980×1320 native frames, and returned `rendered_ok=true`; inspection and pixel audit found every 1958×1264 client exactly pure white. Run-winforge independently rejected all three blank fallbacks with exit 1. Six invalid PNGs were removed, exact PIDs/desktops were cleaned successfully, no WinForge process remained, and no canonical image changed. Visual evidence is `capture-blocked`.

Feature `fc2b76e52171e4f81ab1d15f9fb1da5818791171` passed hosted branch run [29673079883](https://github.com/codingmachineedge/WinForge/actions/runs/29673079883) and published exact-SHA prerelease [native-v1.0.43](https://github.com/codingmachineedge/WinForge/releases/tag/native-v1.0.43). Merge `f7a9eec44aeffdf829f5c07f5eeb364f08a7677f` passed hosted `main` run [29673310778](https://github.com/codingmachineedge/WinForge/actions/runs/29673310778) and published stable exact-SHA [native-v1.0.44](https://github.com/codingmachineedge/WinForge/releases/tag/native-v1.0.44). Both have exactly setup + portable ZIP. Independent download verification matched digests and found 292 ZIP entries, 48 PEs, zero CLR/apphost/forbidden managed/build artifacts, and AMD64 PE32+ `WinForge.exe`.

`gh release create --latest` left `/releases/latest` at historical managed `v1.0.256`. A later bare `gh release edit native-v1.0.44 --latest` was insufficient because release 44 was observed as `prerelease=true` at that edit timestamp, so Latest fell back to managed. Explicit `gh release edit native-v1.0.44 --latest --prerelease=false --draft=false` restored stable/non-draft release 44 and exact `/releases/latest` at `f7a9eec44aeffdf829f5c07f5eeb364f08a7677f` with two native assets.

Commit `53da21446a5c2cded97e1387f3e36f557770a3c5` passed [run 29673965179](https://github.com/codingmachineedge/WinForge/actions/runs/29673965179) and published [native-v1.0.47](https://github.com/codingmachineedge/WinForge/releases/tag/native-v1.0.47) with official Node 24 actions and two native assets. Its old noncurrent check passed while observing managed Latest, so it does not prove the invariant. The amended policy explicitly sets stable/draft flags for fresh current main, verifies stable-native/two-asset `/latest` on every run, and makes noncurrent runs restore a verified current-main candidate when Latest is managed or invalid. Exact amended branch/main run IDs, tags, and SHAs remain pending. Legacy managed workflow `301226619` remains disabled, exactly 28 obsolete failed/cancelled no-release run records were deleted, and successful history/releases remain.

**粵語.** 三項文字分析 Renderer 計 **27/346**，仲有 **319** 條固定 route 同五組動態家族，visual 仍係 `capture-blocked`。原有 exact-SHA、two-asset 同 292-entry／48-PE 原生審查證明保留。Bare `gh release edit ... --latest` 唔足夠，release 44 變成 prerelease 後 Latest 跌返 managed；明確 stable／draft flags 先恢復 exact stable native Latest。`53da2144`／run 29673965179／`native-v1.0.47` 證明 Node 24 同兩個原生 asset，但舊 noncurrent check 見到 managed Latest 仍然通過；修正版自動 invariant 嘅準確 hosted branch／main 證明仍待完成。

## Earlier native line-processing wave — focused validation record; visual capture blocked · 較早原生行文字處理批次 — 專項驗證紀錄；視覺擷取受阻

**Evidence-count note · 證據計數備註：** The managed build's 161 warnings are recorded only as historical, non-contract output; the durable gate is exit 0 with 0 errors. · Managed build 嘅 161 個 warning 只記錄做歷史、非合約輸出；長期 gate 係 exit 0 同 0 errors。

**EN.** Dedicated C++/WinRT renderer source now exists for Line Tools, Line Sort & Dedupe, and Text Wrap, backed by one shared standard-C++ `LineProcessing` core. `HasNativeRenderer` and the parity ledger account for **24/346 fixed routes**, leaving **322 fixed routes** plus five dynamic route families. Native Debug and Release builds each exit 0; both core executables pass **630/630**, including **70/70** Line Processing contracts. Catalog parity passes 346 fixed routes plus five dynamic families; focused `-LineRoutesOnly` UI Automation passes **42/42** across all seven registered aliases; the full owned native shell smoke passes **348/348** with exit 0; and the managed Debug x64 solution build exits 0 with 0 errors, with 161 warnings retained only as historical, non-contract output from that observed run. LowLevel Computer Use MCP 1.28.1 launched `--page lines`, `--page textsort`, and `--page textwrap` on separate private desktops. The first pass resolved one **852×880** window per launch at PIDs 29740, 19176, and 25196, then cleaned every process and desktop; its inspected `rendered_ok` `PrintWindow` frames were composition-white and invalid. A compositor retry on the same persistent server started PIDs 21696, 23644, and 22048, but `show_headless_desktop` failed for all three pages with Win32 error 5, Access denied, before monitor capture; each PID was killed and desktop closed, including the Text Sort desktop's brief 0×0 ghost. The required run-winforge driver also attempted all three routes with `-Native -WaitMs 16000`; `CopyFromScreen` was unavailable, each blank/near-uniform `PrintWindow` fallback was rejected, and each attempt exited 1. The artifact directory is empty, no invalid image remains, and no canonical screenshot was replaced; visual evidence is conclusively `capture-blocked`. Release-per-push and Git integration/remote proof remain pending.

**粵語.** 行工具、行排序同去重、文字換行而家有專用 C++/WinRT renderer source，共用標準 C++ `LineProcessing` core；parity 計數係 **24/346 條固定 route**，仲有 **322 條固定 route** 同五組動態家族。Debug／Release build 各 exit 0，兩個 core executable 各 **630/630**，包括 Line Processing **70/70**；catalog parity、managed Debug x64 0-error build（161 個 warning 只保留做嗰次實際 run 嘅歷史、非合約輸出）、全部七個 registered alias 嘅 `-LineRoutesOnly` UIA **42/42**，同完整自有原生 shell smoke **348/348** 亦已通過。LowLevel MCP 1.28.1 喺獨立 private desktop 準確開過 `--page lines`、`--page textsort` 同 `--page textwrap`；第一輪每次有 **852×880** window，PID 29740／19176／25196 同所有 desktop 已清理，檢查過嘅 `rendered_ok` `PrintWindow` 圖係 composition-white 無效畫面。同一 persistent server 再開 PID 21696／23644／22048 做 compositor retry，但三頁都因 Win32 error 5 Access denied 未能 `show_headless_desktop`；每個 PID 同 desktop 已清理，包括 Text Sort 短暫嘅 0×0 ghost。必需 run-winforge driver 亦用 `-Native -WaitMs 16000` 試過三頁；`CopyFromScreen` 唔可用、空白／近乎單色 fallback 被拒，每次都 exit 1。Artifact directory 已清空、冇保留無效圖、冇替換 canonical 截圖，所以 visual 最終係 `capture-blocked`。Release-per-push 同 Git 遙距證明仍待完成。

## Native design utilities · 原生設計工具

**EN.** Text Diff, Aspect Ratio, and CSS Unit Converter are now genuine C++/WinRT routes backed by standard-C++ cores. The integrated feature/hardening commits are `828c32791e1f135d2a46848e9283087ef8ec9156` and `ce879cc6626eae328ec72e0143761c0edfbae340`; local `main` was fast-forwarded through the latter before this record. Renderer accounting is **21/346 fixed routes**, with **325 fixed routes** pending plus five dynamic families. Debug and Release core each pass **560/560** (Design Tools **94/94**, Text Diff **27/27**), the strengthened utility UI Automation shell passes **39/39**, the full owned shell remains **300/300**, catalog parity passes, and a one-million-finite-double .NET 11 differential found zero Aspect Ratio formatting mismatches. Hosted run [29663954724](https://github.com/codingmachineedge/WinForge/actions/runs/29663954724) published exact-SHA prerelease [native-v1.0.37](https://github.com/codingmachineedge/WinForge/releases/tag/native-v1.0.37) with installer and portable zip assets.

**Visual evidence.** LowLevel Computer Use MCP 1.28.1 and the repository driver separately launched all three pages. Every inspected 1304×841 client capture was uniformly white because WinUI composition is unavailable in this desktop session; exact processes/desktops were closed, the immutable stage and invalid frames were deleted, and no canonical image was replaced. The routes are functionally/accessibly verified but honestly `capture-blocked`.

**粵語.** 文字差異比對、長寬比計算同 CSS 單位換算而家係真正 C++/WinRT route，由標準 C++ core 支援。Debug／Release 各 **560/560**、Design Tools **94/94**、文字差異比對 **27/27**、工具專項 UI Automation **39/39**、完整自有 shell **300/300** 同 catalog parity 全部通過；一百萬個有限 double 對 .NET 11 比對係零個格式差異。Hosted run 29663954724 已為準確 SHA 發佈 native-v1.0.37。指定 LowLevel MCP 同 driver 都開過三頁，但 client 因 session 冇 WinUI composition 而全白；無效圖已刪除，視覺如實係 `capture-blocked`。今批係 **21/346**，仲有 **325 條固定 route** 同五組動態家族要繼續。

## Native installer CI verified integration · 原生安裝程式 CI 已驗證整合

**EN.** The native installer contract task b5cae63dd53e1892aca61e039597d1f3b9a6b73c merged as 1c3c9a1a. After fetch, the task commit, pushed feature tip, and merge commit were proven ancestors of origin/main; the workflow, verifier, docs, Pages mirrors, generated site data, and handoff records were present remotely.

**粵語.** 原生 installer contract task b5cae63dd53e1892aca61e039597d1f3b9a6b73c 合併為 1c3c9a1a。fetch 後 task、已推送 branch tip 同 merge commit 都係 origin/main ancestor；workflow、verifier、docs、Pages mirror、site data 同 handoff records 都已確認喺 remote main。


## Native Symbols Palette record · 原生特殊符號調色盤紀錄

**EN.** Main now contains the C++ catalog and C++/WinRT Symbols Palette with 226 local glyphs, safe search, explicit Copy, and builder handoff. Debug/Release core tests passed 411/411 and the owned LowLevel MCP UI Automation campaign passed 238/238. Visual evidence is accurately capture-blocked because the isolated driver rejected a blank or near-uniform fallback.

**Integration proof.** Task ba1a6c6192c1a150e35ebf09c0242d4c1d686177 merged as 04a593f8; after fetch, the task, pushed feature tip, and merge commit were proven ancestors of origin/main and the native sources, tests, documentation, Pages mirrors, capture status, and handoff records were present remotely.

**整合證明。** 任務 ba1a6c6192c1a150e35ebf09c0242d4c1d686177 合併為 04a593f8；fetch 後任務、已推送分支 tip 同合併提交都係 origin/main ancestor，原生 source、tests、documentation、Pages mirror、capture status 同 handoff records 都喺 remote main。

**粵語.** Main 而家有 C++ catalog 同 C++/WinRT 特殊符號調色盤，包含 226 個本機字形、安全搜尋、明確 Copy 同 builder handoff。Debug/Release core tests 411/411，同埋 LowLevel MCP UI Automation 238/238 通過。隔離 driver 拒絕空白／近乎單色 fallback，所以視覺證據正確係 capture-blocked。


## Native Regex Tester integration proof · 原生 Regex Tester 整合證明

`module.regextester` now enumerates at most **100** bounded non-overlapping matches, carries named capture metadata, safely advances zero-length matches under one shared deadline, and previews local replacement. The direct tester is case-sensitive by default; the builder carries PCRE2 `(x)`/`(n)` state to Shell, All Apps, cache-only Package Discover, and Regex Cheatsheet. Replacement is intentionally a local subset only: `$$`, existing `$0`–`$99`, and `${name}`; invalid replacement or the 32 KiB cap fails closed. Debug/Release each passed **403/403** and isolated LowLevel MCP headless UI Automation passed **226/226**. Inspected 852×880 full and 836×841 client frames were blank and discarded, so this is `capture-blocked`. Task `72ce549110b3d235b406de397736e89ecbcdb055` (also the remote feature tip) merged as `f7cba1a4694df705cd483868755af079e6250fda`; after fetch, both plus the merge were proven ancestors of `origin/main` and the expected source/test/docs/Pages/parity/handoff paths were confirmed on remote main before cleanup.

`module.regextester` 而家列舉最多 **100** 個有界非重疊相符、帶住命名 capture metadata、喺同一個 deadline 下安全處理零長度相符，並預覽本機 replacement。直接 Tester 預設 case-sensitive；builder 會將 PCRE2 `(x)`／`(n)` 狀態帶去 Shell、All Apps、只限快取嘅 Package Discover 同 Regex Cheatsheet。replacement 刻意只係本機子集：`$$`、存在嘅 `$0`–`$99` 同 `${name}`；無效 replacement 或 32 KiB cap 都會 fail closed。Debug／Release 都通過 **403/403**，isolated LowLevel MCP headless UI Automation 通過 **226/226**。檢查過嘅 852×880 full 同 836×841 client frame 係空白並已丟棄，所以係 `capture-blocked`。task `72ce549110b3d235b406de397736e89ecbcdb055`（亦係 remote feature tip）以 `f7cba1a4694df705cd483868755af079e6250fda` 合併；fetch 後已證明佢哋加 merge 都係 `origin/main` 嘅 ancestor，亦已確認 expected source／test／docs／Pages／parity／handoff path 都喺 remote main，先做清理。

## Latest native continuation — 2026-07-16

`module.regexcheat` is now a real C++/WinRT route with a pure-C++ 67-row/9-category bilingual reference catalog, eight copy-only ready-made patterns, default literal filtering, and an explicit bounded-PCRE2 local filter. It is the fourth registered native regex search surface and can round-trip a verified pattern through the full builder. .NET-only reference syntax remains documentation and never executes. Debug/Release native tests passed **395/395**, catalog parity passed 346 fixed routes plus five dynamic families, and isolated LowLevel MCP UI Automation passed **224/224**. The inspected 852×880 full and 836×841 client frames were blank, discarded, and recorded as `capture-blocked`; no stale or managed image is used as native evidence.

`module.regexcheat` 而家係真正嘅 C++/WinRT route，有純 C++ 67 項／9 分類雙語參考、八個只可明確複製嘅現成模式、預設 literal 篩選同明確啟用嘅 bounded-PCRE2 本機篩選。佢係第四個已註冊 native regex search surface，亦可以同完整 builder round-trip 已驗證嘅模式。.NET 專用語法只係文件，唔會執行。Debug/Release **395/395**、catalog parity 同隔離 LowLevel MCP UI Automation **224/224** 都通過；852×880 full 同 836×841 client frame 空白、已丟棄，狀態係 `capture-blocked`。

### Git integration proof · Git 整合證明

The native Regex Cheatsheet task commit `24f32ba85eade7244dc839760807ea3ea3d1a5d9` was merged into `main` as `2872b234022188d70f250fdbae3d78a740f68fa8`. After fetching, both that task commit and the remote feature-branch tip were proven ancestors of `origin/main`; `AGENTS.md`, the handoff records, native sources/tests, parity ledger, and wiki/Page mirrors were confirmed in the remote main tree before cleanup.

原生 Regex Cheatsheet task commit `24f32ba85eade7244dc839760807ea3ea3d1a5d9` 已經以 `2872b234022188d70f250fdbae3d78a740f68fa8` 合併入 `main`。fetch 後已證明 task commit 同 remote feature-branch tip 都係 `origin/main` 嘅 ancestor；清理前亦已確認 `AGENTS.md`、handoff records、原生 sources／tests、parity ledger 同 wiki／Pages mirrors 都喺 remote main tree。

A complete map of every module/feature: what it does, how to open it, the page + service files, and the
**real engine it wraps**. WinForge is a bilingual (English + 粵語) WinUI 3 / .NET 11 suite for Windows 11.
**No redirects** — every shipping feature runs in-app and wraps a real engine/API. A genuine C++20/C++/WinRT rewrite now lives beside this managed oracle; its foundation is routable and verified, but feature parity remains evidence-gated and is not yet claimed.

完整列出每個模組／功能：做乜、點開、頁面同服務檔案、同埋包住嘅真實引擎。WinForge 係雙語（英文 + 粵語）
嘅 WinUI 3 / .NET 11 Windows 11 套件。**唔跳轉** — 每個發佈中功能都喺 app 內運行、包住真實引擎／API。而家亦有真正 C++20/C++/WinRT 重寫同受控 oracle 並存；基礎 shell 已可路由同驗證，但功能對等仍要逐項證據把關，未聲稱完成。

## Build / run / release · 建置／運行／發佈
- Build: `dotnet build -c Debug -p:Platform=x64` (must stay 0 errors).
- Native build: `msbuild WinForge.Native.sln /restore /m /p:Configuration=Debug /p:Platform=x64` (the local driver discovers the installed MSVC toolset).
- Native route/unit evidence: `tests\native\WinForge.Core.Tests\bin\x64\Debug\WinForge.Core.Tests.exe`; native live shell evidence: `powershell -ExecutionPolicy Bypass -File eng\native\Invoke-NativeShellSmoke.ps1`.
- Native launch: `powershell -ExecutionPolicy Bypass -File .agents\skills\run-winforge\driver.ps1 -Native -Page <id> -NoCapture`. See [Native C++ Rewrite](Native-Cpp-Rewrite.md); a native route is not a port-complete claim.
- Exe: `bin\x64\Debug\net11.0-windows10.0.26100.0\win-x64\WinForge.exe`. Self-contained (`WindowsAppSDKSelfContained=true`, `WindowsPackageType=None`).
- Launch a page directly: `WinForge.exe --page <id>` (see `docs/CLI.md` for every id) · master search: `--page search:<q>` · headless docs: `--export-docs docs\features`.
- Window: **windowed by default (~82% screen), F11 toggles full screen** (saved). Closing **hides to the system tray** (right-click tray → Quit) so the background clipboard monitor keeps running.
- Theme: Settings → App theme (Light/Dark/System), saved. Language: Settings → Language (Bilingual/Cantonese/English).
- CI: `.github/workflows/native-release.yml` is the sole publisher and emits exactly the C++ portable ZIP plus `WinForge-Native-Setup.exe`. Fresh current main explicitly sets Latest + stable + non-draft; every release run verifies stable native exact SHA + two assets through `/releases/latest`; noncurrent runs restore a verified current-main candidate if Latest is managed or invalid. Run 29673965179 proves Node 24/two-asset branch publication only; exact amended invariant proof is pending.

## Suite modules · 套件模組

| Module · 模組 | `--page` | Page | Service(s) | Engine / mechanism | Status |
|---|---|---|---|---|---|
| Dashboard · 概覽 | `dashboard` | `Pages/DashboardPage` | — | system summary, tiles, search | ✅ |
| Git & GitHub · Git 與 GitHub | `git` | `Pages/GitHubModule` | `Services/GitService` | git + gh CLI, chunked uploader (111 ops) | ✅ |
| Archives · 壓縮檔 | `archives` | `Pages/ArchivesModule` | `Services/ArchiveService` | 7-Zip (create/extract/test/bench, 100 ops) | ✅ |
| Media · 媒體 | `media` | `Pages/MediaModule` | `Services/MediaService` | ffmpeg (convert/trim/gif, 60 ops) | ✅ |
| Registry Editor · 登錄編輯器 | `registry` | `Pages/RegistryEditor` | `Services/RegistryHelper` | live registry browse/edit | ✅ |
| Services · 服務 | `services` | `Pages/ServicesModule` | `Services/ServiceManager` | CIM + *-Service; **Actions dropdown** | ✅ |
| Scheduled Tasks · 排程工作 | `tasks` | `Pages/ScheduledTasksModule` | `Services/TaskSchedulerManager` | Get-ScheduledTask; **Actions dropdown** | ✅ |
| Devices · 裝置 | `devices` | `Pages/DevicesModule` | `Services/DeviceManager` | Get-PnpDevice; enable/disable (confirm) | ✅ |
| Startup Apps · 開機程式 | `startup` | `Pages/StartupModule` | `Services/StartupManager` | Run keys + StartupApproved + folders | ✅ |
| Batch Rename · 批次改名 | `rename` | `Pages/RenameModule` | `Services/RenameEngine` | regex/sequence file rename | ✅ |
| Bulk File Ops · 批次檔案操作 | `bulkops` | `Pages/BulkOpsModule` | `Services/BulkFileOps` | SHFileOperation (copy/move/recycle) | ✅ |
| Duplicate Finder · 重複檔案搜尋 | `duplicates` | `Pages/DuplicatesModule` | `Services/DuplicateFinder` | size + hash dedupe | ✅ |
| Disk Analyser · 磁碟分析 | `disk` | `Pages/DiskAnalyzerModule` | `Services/DiskAnalyzer` | folder-size tree | ✅ |
| Drives · 磁碟機 | `drives` | `Pages/DrivesModule` | `Services/DriveService` | volumes, mount ISO/VHD | ✅ |
| App Uninstaller / 原生解除安裝器 | `uninstall` | native C++/WinRT `module.uninstall`; managed page remains oracle | `WinForge.Core/AppUninstaller` + Windows PackageManager | current-user Store/UWP cache; local literal/Regex filtering; review/Confirm; normal-integrity gate; no local-data deletion | in progress: headless UI and visual capture blocked |
| Window Manager · 視窗管理 | `windows` | `Pages/WindowManagerModule` | `Services/WindowManager` | EnumWindows + SetWindowPos (zones) | ✅ |
| Keyboard Remapper · 鍵盤重新對應 | `keyboard` | `Pages/KeyboardModule` | `Services/KeyboardRemapper` | Scancode Map registry | ✅ |
| Hosts Editor · hosts 編輯器 | `hosts` | `Pages/HostsEditorModule` | `Services/HostsService` | hosts file IO + flush DNS | ✅ |
| Mouse & Pointer · 滑鼠與指標 | `mouse` | `Pages/MouseModule` | `Services/MouseSettings` | SystemParametersInfo (live) | ✅ |
| Screen Recorder · 螢幕錄影 | `recorder` | `Pages/ScreenRecorderModule` | `Services/ScreenRecorder` | ffmpeg gdigrab (whole desktop) | ✅ |
| System Monitor · 系統監察 | `monitor` | `Pages/SystemMonitorModule` | `Services/SystemMonitor` | GetSystemTimes/NetworkInterface; per-proc priority/affinity/EcoQoS | ✅ |
| Connections · 連線 | `connections` | `Pages/ConnectionsModule` | `Services/ConnectionsService` | iphlpapi (TCPView-style) | ✅ |
| Event Viewer · 事件檢視器 | `events` | `Pages/EventViewerModule` | `Services/EventLogService` | Get-WinEvent | ✅ |
| Volume Mixer · 音量混合器 | `mixer` | `Pages/VolumeMixerModule` | `Services/AudioMixer` | Core Audio (WASAPI) COM | ✅ |
| Context Menu · 右鍵選單 | `contextmenu` | `Pages/ContextMenuModule` | `Services/ContextMenuService` | HKCU shell verbs | ✅ |
| Awake · 保持喚醒 | `awake` | `Pages/AwakeModule` | `Services/AwakeService` | SetThreadExecutionState | ✅ |
| Color Picker · 螢幕取色 | `colorpicker` | `Pages/ColorPickerModule` | `Services/ColorPickService` | WH_MOUSE_LL hook + GetPixel | ✅ |
| Environment Variables · 環境變數 | `envvars` | `Pages/EnvVarsModule` | `Services/EnvVarService` | Environment.*Variable; per-entry PATH editor | ✅ |
| Clipboard · 剪貼簿 | `clipboard` | `Pages/ClipboardModule` | `Services/ClipboardService` | Clipboard.ContentChanged + **local git repo** + opencode commit msgs | ✅ |
| Package Manager · 套件管理 | `packages` | `Pages/PackageManagerModule` | `Services/PackageService` | winget (UniGetUI-style) + `AutoInstall` | ✅ |
| Android (ADB) · Android（ADB） | `adb` | `Pages/AndroidAdbModule` | `Services/AdbService` | adb (devices/APK/shell/logcat/screencap/reboot); auto-installs adb | ✅ |
| VPN & Mesh · VPN 與網狀網 | `vpn` | `Pages/VpnMeshModule` | `Services/NordVpnService`, `Services/TailscaleService` | NordVPN.exe + tailscale CLIs; auto-install | ✅ |
| Search results · 搜尋結果 | `search:<q>` | `Pages/SearchResultsPage` | `Services/ModuleRegistry` | master search — pages + live tweak toggles | ✅ |
| Settings · 設定 | `settings` | `Pages/SettingsPage` | `Services/SettingsStore` | language, theme, import/export | ✅ |
| About · 關於 | `about` | `Pages/AboutPage` | — | about/version | ✅ |

**System tray** — `Services/TrayService` (raw Shell_NotifyIcon + message-only window): keeps WinForge running when the window is closed; tray menu Open / Quit.

## Tweak catalog · 調校目錄
Data-driven: `Catalog/*Tweaks.cs` files build `TweakDefinition`s via the `Services/Tweak` factory (RegToggle/CustomToggle/RegChoice/Action/Shell/Powershell/Cmd/Info). `Catalog/TweakCatalog.cs` aggregates them per `Catalog/Categories.cs` category. Rendered by `Controls/TweakCard` (shows EN+粵語, reads live state via `GetIsOn`, full scrollable monospace output for actions). ~1140 tweaks/ops across 22 categories incl. **Winaero Tweaks** (45) and **Debloat & Annoyances**.

## Key architecture · 主要架構
- **Language modes:** `Services/Loc` — `Loc.I.Pick(en, zh)` returns bilingual text in Bilingual mode, or a single string in Cantonese/English modes.
- **Registry:** `Services/RegistryHelper` (RegRoot HKCU/HKLM/HKCR/HKU, Get/Set/Delete/SubKeys).
- **Shell:** `Services/ShellRunner` (Run/RunCmd/RunPowershell/CapturePowershell).
- **Nav:** `MainWindow` NavigationView with collapsible groups; `Navigator.GoToModule/GoToCategory` resolve tags **recursively**; `MapType` fallback. `Services/ModuleRegistry` powers page-search.
- **Touchless install:** `PackageService.AutoInstall(id)` = winget silent + refresh process PATH; wired into engine-bars (adb, NordVPN, Tailscale).
- **Docs:** `Services/DocsExporter` writes per-feature Markdown into **per-module subfolders** under `docs/features/`.

## Pending queue · 待辦 (see `docs/ROADMAP.md`)
UX pass (remaining: Devices Actions dropdown, parse tabular command output into tables) → future App Uninstaller work only after a handle-relative, stable-identity local-data deletion primitive; the current native route has no deep-clean path → auto-install everywhere + kill remaining redirects → more 7z/zip features → custom-program runner → full export/import incl. the clipboard git repo → Docker/GitHub config sync → app logo → UniGetUI source port. The 5-min loop builds these one tested+pushed module at a time.

_Auto-maintained alongside the WinForge build loop · 由 WinForge 建置迴圈一齊維護_
