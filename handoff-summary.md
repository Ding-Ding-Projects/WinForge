# WinForge Full Development Handoff

## Native text-analysis wave — exhaustive shell green; visual capture blocked · 原生文字分析批次 — 完整 shell 通過；視覺擷取受阻

- **Scope / 範圍：** dedicated C++/WinRT renderers for Text Statistics, Word Frequency, and String Compare, backed by the shared standard-C++ `TextAnalysis` core. Text Statistics preserves local UTF-16/CJK word/sentence/paragraph/average/time/Flesch/top-ten behavior. Word Frequency preserves word/bigram/Unicode-scalar modes, options and stop words, current-culture ordering, count/bar/percentage rows, and exact CSV. String Compare preserves normalization, UTF-16 metrics, OSA/Jaro–Winkler/longest-common-substring/subsequence results, the greater-than-2,000 guard, exact greedy Jaro optimized subquadratically, and route-exit cleanup. Clipboard mutation is explicit-only for Word Frequency and String Compare; Text Statistics has no side effect.
- **Inventory / 清單：** `HasNativeRenderer` now accounts for **27/346 fixed routes**, leaving **319 fixed routes** plus five dynamic route families. The provisional ledger is exactly **27 `in-progress` / 319 `not-started`**; `capture-blocked` routes are not called complete.
- **Evidence green / 已通過證據：** native Debug and Release builds each exit 0; both core executables pass **714/714**, including focused Text Analysis **84/84**; managed `StructuredTextTools.Tests` passes **7/7**; focused `-TextAnalysisRoutesOnly` UI Automation passes **40/40** across all seven registered aliases; the DPI-aware utility shell regression passes **39/39**; the final exhaustive native shell passes **388/388 (388 passed, 0 failed)**; catalog parity covers 346 fixed routes plus five dynamic families; and the managed Debug x64 solution build exits 0 with 0 errors.
- **Headless capture disposition / 無頭擷取處置：** LowLevel Computer Use MCP 1.28.1 created `winforge-textstats-capture`, `winforge-wordfreq-capture`, and `winforge-stringcompare-capture`; launched routes `textstats`, `wordfreq`, and `similarity`; and resolved PIDs **24016**, **20180**, and **26228** with `WinUIDesktopWin32WindowClass` HWNDs **49022214**, **49087750**, and **49153286**. Each window was **1980×1320**. Full-window 1980×1320 and client-only **1958×1264** screenshots reported `rendered_ok=true`, but original inspection showed frame-only white content and pixel audit found every client was exactly one RGB color, `(255,255,255)`. The repository driver independently ran all three routes with `-Native -WaitMs 16000`; every command exited 1 with: `CopyFromScreen is unavailable and the PrintWindow fallback produced a blank or near-uniform WinUI client frame; graphics capture is unavailable in this desktop session.` All six invalid MCP PNGs were removed, the driver retained no invalid file, all three exact PIDs were terminated with forced cleanup, all three named private desktops closed successfully, subsequent process checks found no WinForge process, and no canonical screenshot changed. Visual evidence is conclusively `capture-blocked` for all three rows.
- **Evidence still pending / 仍待證據：** the visual blocker is final and all three ledger rows remain `capture-blocked` / `in-progress` until a new valid inspected frame exists. Feature integration and its branch/main native-only releases are proven below. The amended automatic Latest invariant still needs exact hosted branch/main run IDs, tags, and SHAs; neither the completed text-analysis runs nor partial corrective run 29673965179 proves it.
- **Completed integration and release proof / 已完成整合同版本證明：** feature `fc2b76e52171e4f81ab1d15f9fb1da5818791171` passed hosted branch run [29673079883](https://github.com/codingmachineedge/WinForge/actions/runs/29673079883) and published exact-SHA prerelease [native-v1.0.43](https://github.com/codingmachineedge/WinForge/releases/tag/native-v1.0.43) with exactly setup + portable ZIP. Merge `f7a9eec44aeffdf829f5c07f5eeb364f08a7677f` passed hosted `main` run [29673310778](https://github.com/codingmachineedge/WinForge/actions/runs/29673310778) and published stable exact-SHA [native-v1.0.44](https://github.com/codingmachineedge/WinForge/releases/tag/native-v1.0.44), again with exactly those two native assets. Independent download verification matched the published digests and found 292 ZIP entries, 48 PE files, zero CLR/apphost or forbidden managed/build artifacts, and AMD64 PE32+ `WinForge.exe`.
- **Latest-channel defect and manual correction / Latest channel 缺陷同手動修正：** `gh release create --latest` left `/releases/latest` at historical managed `v1.0.256`. A later bare `gh release edit native-v1.0.44 --latest` was insufficient: release 44 was observed as `prerelease=true` at that edit timestamp, so Latest fell back to managed. Explicit `gh release edit native-v1.0.44 --latest --prerelease=false --draft=false` restored stable/non-draft release 44 and exact `/releases/latest` at `f7a9eec44aeffdf829f5c07f5eeb364f08a7677f` with exactly two native assets.
- **Corrective branch partial proof / 修正分支部分證明：** commit `53da21446a5c2cded97e1387f3e36f557770a3c5` passed hosted run [29673965179](https://github.com/codingmachineedge/WinForge/actions/runs/29673965179) and published [native-v1.0.47](https://github.com/codingmachineedge/WinForge/releases/tag/native-v1.0.47) with official Node 24 actions and exactly two native assets. Its old noncurrent check was insufficient and passed while observing managed Latest. The amended policy therefore requires explicit stable/draft flags for fresh current main, stable-native/two-asset `/latest` verification on every release run, and restoration of a verified current-main stable native candidate by noncurrent runs when GitHub points to managed or invalid Latest. Exact amended hosted proof remains pending. Legacy managed workflow ID `301226619` remains disabled; exactly 28 obsolete failed/cancelled no-release run records were deleted while successful history and releases were retained.

**粵語摘要：** 文字分析 renderer 計 **27/346**，仲有 **319** 條固定 route 同五組動態家族，visual 仍係 `capture-blocked`。功能 `fc2b76e5`／run 29673079883／`native-v1.0.43` 同 merge `f7a9eec4`／run 29673310778／`native-v1.0.44` 嘅 exact-SHA、two-asset 同 292-entry／48-PE 原生審查證明保留。Bare `gh release edit ... --latest` 唔足夠，release 44 變成 prerelease 後 Latest 跌返 managed；明確 `--prerelease=false --draft=false` 先恢復 exact stable native Latest。`53da2144`／run 29673965179／`native-v1.0.47` 證明 Node 24 同兩個原生 asset，但舊 noncurrent check 見到 managed Latest 仍然通過，所以修正版自動 invariant 嘅準確 branch／main hosted 證明仍待完成。

## Earlier native line-processing wave — focused validation record; visual capture blocked · 較早原生行文字處理批次 — 專項驗證紀錄；視覺擷取受阻

**Evidence-count note · 證據計數備註：** The managed build's 161 warnings are recorded only as historical, non-contract output; the durable gate is exit 0 with 0 errors. · Managed build 嘅 161 個 warning 只記錄做歷史、非合約輸出；長期 gate 係 exit 0 同 0 errors。

- **Scope / 範圍：** dedicated C++/WinRT renderer source for Line Tools, Line Sort & Dedupe, and Text Wrap; one shared standard-C++ `LineProcessing` core; renderer membership, route aliases, focused tests, UIA harness coverage, generated feature status, and native rewrite mirrors.
- **Inventory / 清單：** `HasNativeRenderer` and the provisional parity ledger account for **24/346 fixed native renderers**, leaving **322 fixed routes** pending plus five dynamic route families. This advances the continuing port goal but does not complete it.
- **Evidence green / 已通過證據：** native Debug and Release builds each exit 0; both core executables pass **630/630**, including **70/70** Line Processing contracts; catalog parity passes 346 fixed routes plus five dynamic families; focused `-LineRoutesOnly` UI Automation passes **42/42** across all seven registered aliases; the full owned native shell smoke passes **348/348** with exit 0; and the managed `dotnet build WinForge.sln -c Debug -p:Platform=x64` check exits 0 with 0 errors (161 existing warnings were reported by that observed run).
- **Headless launch and capture disposition / 無頭啟動同擷取處置：** LowLevel Computer Use MCP 1.28.1 launched the exact canonical entry commands `--page lines`, `--page textsort`, and `--page textwrap` on separate private desktops. The first pass resolved one **852×880** window per launch at PIDs 29740, 19176, and 25196, then cleaned every process and desktop. Initial `PrintWindow` captures returned `rendered_ok`, but inspection found composition-white invalid frames. A compositor retry on the same persistent server started PIDs 21696, 23644, and 22048, but `show_headless_desktop` failed for all three pages with Win32 error 5, Access denied, before monitor capture; each PID was killed and desktop closed, including Text Sort's brief 0×0 ghost. The required run-winforge driver separately attempted all three routes with `-Native -WaitMs 16000`; `CopyFromScreen` was unavailable, each blank/near-uniform `PrintWindow` fallback was rejected, and each attempt exited 1. The artifact directory is empty, all invalid images were deleted, and no canonical screenshot was replaced. Launch and behavioral evidence are green; visual evidence is conclusively `capture-blocked`.
- **Evidence still pending / 仍待證據：** hosted release-per-push proof, task commit, merge, remote ancestry/file proof, and cleanup. Visual evidence is conclusively `capture-blocked`; no canonical screenshot, release, or integration pass is claimed yet.

**粵語摘要：** 行工具、行排序同去重、文字換行嘅原生 source 同共用 `LineProcessing` core 已加入；暫時計數係 **24/346 條固定 route**，仲有 **322 條固定 route** 同五組動態家族。Debug／Release build 同 core、Line Processing **70/70**、總套件 **630/630**、catalog parity、managed 0-error build、全部七個 registered alias 嘅專項 UIA **42/42**，同完整自有原生 shell smoke **348/348** 已通過。LowLevel MCP 1.28.1 第一輪喺獨立 private desktop 準確開過 `--page lines`、`--page textsort` 同 `--page textwrap`，每次解析到 **852×880** window，PID 29740／19176／25196 同所有 desktop 已準確清理；但 `rendered_ok` `PrintWindow` 圖檢查後係 composition-white 無效畫面並已刪除。之後同一 persistent server 用 PID 21696／23644／22048 做 compositor retry，三頁都因 Win32 error 5 Access denied 未能 `show_headless_desktop`，而 run-winforge driver 三頁亦因 `CopyFromScreen` 唔可用同空白／近乎單色 `PrintWindow` fallback 被拒而 exit 1；artifact directory 已清空、冇替換 canonical 圖，所以 visual 最終係 `capture-blocked`。Release 同 Git 遙距整合證據仍待完成。

## Native design-utility wave integration record · 原生設計工具批次整合紀錄

- **Feature commit / 功能提交：** `828c32791e1f135d2a46848e9283087ef8ec9156`.
- **Hardened integration tip / 加固整合 tip：** `ce879cc6626eae328ec72e0143761c0edfbae340` from `codex/native-utility-four`; local `main` was fast-forwarded through this exact tip before this record.
- **Scope / 範圍：** genuine C++/WinRT Text Diff, Aspect Ratio, and CSS Unit Converter routes backed by standard-C++ cores; native aliases, managed-parity reset/language state, bounded virtualization, accessibility live regions, explicit-only clipboard Copy, durable generated feature references, and release-per-push workflow provenance.
- **Inventory / 清單：** `HasNativeRenderer` and the parity ledger agree on **21/346 fixed native renderers**, leaving **325 fixed routes** pending plus five dynamic route families. This is a completed integration wave, not completion of the continuing 100% port goal.
- **Evidence / 證據：** native Debug and Release builds completed with 0 warnings/0 errors under Visual Studio MSBuild; both core executables passed **560/560** (Design Tools **94/94**, Text Diff **27/27**); managed Debug build passed with 0 warnings/0 errors; XAML literal safety passed; full owned shell **300/300**, strengthened utility shell **39/39**, and catalog parity **346 + five families** passed. A deterministic one-million-finite-double .NET 11 differential found zero Aspect Ratio display-format mismatches.
- **Headless visual status / 無頭視覺狀態：** LowLevel Computer Use MCP 1.28.1 launched all three routes from one immutable 294-file runtime snapshot on separate named desktops, confirmed each exact launch PID, resolved one 1320×880 WinUI frame per route, captured and inspected full/client frames, killed each PID, and closed each desktop. Every 1304×841 client frame was one white color with zero standard deviation/non-white fraction. The repository driver separately launched each route and rejected the same blank `PrintWindow` fallback when `CopyFromScreen` was unavailable. The six invalid PNGs and immutable stage were deleted; no canonical image was replaced, so all three rows are honestly `capture-blocked`.
- **Branch release proof / 分支版本證明：** hosted native run [29663954724](https://github.com/codingmachineedge/WinForge/actions/runs/29663954724) passed every build/test/parity/package/installer-smoke gate and published [native-v1.0.37](https://github.com/codingmachineedge/WinForge/releases/tag/native-v1.0.37). Its tag ref and `target_commitish` both resolve exactly to `ce879cc6626eae328ec72e0143761c0edfbae340`; `WinForge-Native-Setup.exe` and `WinForge-native-x64-1.0.37.zip` are present with recorded SHA-256 digests.
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
