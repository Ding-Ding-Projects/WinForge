# Persistent Agent Task Memory / 長期代理任務記憶

This file is the durable execution contract for every agent working in this repository. It supplements, and never weakens, `AGENTS.md`.

## Latest native text-analysis checkpoint (exhaustive shell green; visual capture blocked) / 最新原生文字分析檢查點（完整 shell 通過；視覺擷取受阻）

- Native Text Statistics, Word Frequency, and String Compare now have dedicated C++/WinRT renderers backed by standard-C++ `TextAnalysis`. Text Statistics is local-only; Word Frequency and String Compare mutate the clipboard only after explicit Copy. Preserve UTF-16/CJK statistics, word/bigram/Unicode-scalar frequency with current-culture ordering and exact CSV, and normalized UTF-16 OSA/Jaro–Winkler/LCS comparison with the greater-than-2,000 guard, subquadratic exact greedy Jaro, and route-exit cleanup.
- `HasNativeRenderer` and the provisional ledger account for **27/346 fixed routes**, leaving **319 fixed routes** plus five dynamic route families. Ledger accounting is exactly **27 `in-progress` / 319 `not-started`** until visual evidence changes the three rows.
- Current green evidence: native Debug and Release builds exit 0; both core executables pass **714/714**, including Text Analysis **84/84**; managed Structured Text Tools passes **7/7**; focused text-analysis UIA passes **40/40** across seven aliases; the DPI-aware utility regression passes **39/39**; the final exhaustive native shell passes **388/388 (388 passed, 0 failed)**; catalog parity covers 346 fixed routes plus five families; and the managed build exits 0 with 0 errors.
- LowLevel MCP 1.28.1 launched `textstats`, `wordfreq`, and `similarity` on `winforge-textstats-capture`, `winforge-wordfreq-capture`, and `winforge-stringcompare-capture`. PIDs 24016/20180/26228 and HWNDs 49022214/49087750/49153286 resolved 1980×1320 native windows; every `rendered_ok=true` 1958×1264 client was exactly RGB (255,255,255). Run-winforge independently exited 1 for all three blank fallbacks. Six invalid PNGs were removed, exact PIDs were force-terminated, named desktops closed, driver cleanup left no WinForge process, and no canonical screenshot changed. Keep all three visual dimensions `capture-blocked` and rows `in-progress`; completed release proof does not change the **27 `in-progress` / 319 `not-started`** ledger.
- Text-analysis feature `fc2b76e52171e4f81ab1d15f9fb1da5818791171` passed hosted branch run `29673079883` and published exact-SHA prerelease `native-v1.0.43`; merge `f7a9eec44aeffdf829f5c07f5eeb364f08a7677f` passed hosted `main` run `29673310778` and published stable exact-SHA `native-v1.0.44`. Both releases contain exactly `WinForge-Native-Setup.exe` and the portable ZIP. Independent download verification matched digests and found 292 ZIP entries, 48 PE files, zero CLR/apphost or forbidden managed/build artifacts, and AMD64 PE32+ `WinForge.exe`.
- The native release workflow is the sole publisher, but run `29673310778` exposed that `gh release create --latest` did not move `/releases/latest` from historical managed `v1.0.256`. Official manual `gh release edit native-v1.0.44 --latest` repaired the endpoint to `native-v1.0.44`. Corrective hardening adds explicit release edit, fail-closed Latest/asset verification, and official Node 24 `checkout@v7`, `upload-artifact@v7`, `download-artifact@v8`, and `setup-msbuild@v3`. Exact corrective branch/main run IDs, tags, and SHAs remain pending; do not claim the automatic Latest postcondition until hosted proof exists. Legacy managed workflow ID `301226619` remains disabled; exactly 28 obsolete failed/cancelled no-release run records were deleted while successful history and releases were retained.
- 原生文字統計、詞頻統計同字串相似度而家有專用 C++/WinRT renderer 同共用 `TextAnalysis` core；計數係 **27/346**，仲有 **319** 條固定 route 同五組動態家族。Debug／Release core 各 **714/714**（Text Analysis **84/84**）、managed **7/7**、UIA **40/40**、utility **39/39**、完整 shell **388/388（388 passed、0 failed）**、catalog parity 同 managed 0-error build 已通過，visual 仍係 `capture-blocked`。功能 `fc2b76e5` 嘅 branch run 29673079883 發佈 `native-v1.0.43`；merge `f7a9eec4` 嘅 `main` run 29673310778 發佈 `native-v1.0.44`，兩者都係 exact-SHA、只含兩個原生 asset，獨立 292-entry／48-PE 審查亦全綠。但 `gh release create --latest` 冇移動 Latest endpoint，要用官方手動 edit 修復；修正 workflow 嘅準確 hosted 自動證明仍待完成。

## Earlier native line-processing checkpoint (focused validation green; visual capture blocked) / 較早原生行文字處理檢查點（專項驗證通過；視覺擷取受阻）

- The managed build's 161 warnings are historical, non-contract output; only exit 0 with 0 errors is the durable gate. · Managed build 嘅 161 個 warning 只係歷史、非合約輸出；長期 gate 只係 exit 0 同 0 errors。
- Native source now contains dedicated C++/WinRT renderers for Line Tools, Line Sort & Dedupe, and Text Wrap, backed by the shared standard-C++ `LineProcessing` core. `HasNativeRenderer` and the provisional parity ledger account for **24/346 fixed routes**, leaving **322 fixed routes** plus five dynamic route families.
- Native Debug and Release builds each exit 0. Both core executables pass **630/630**, including **70/70** focused Line Processing contracts across managed-compatible line splitting/counting, Unicode cleanup and ordinal folding, CoreLib-style sorting, natural order, deterministic shuffle fixtures, paragraph wrap/reflow, and UTF-16 metrics. Catalog parity passes all 346 fixed routes plus five dynamic families. The managed Debug x64 solution build exits 0 with 0 errors; that observed run reported 161 existing warnings.
- Focused `-LineRoutesOnly` UI Automation passes **42/42** across all seven registered aliases, and the full owned native shell smoke passes **348/348** with exit 0. LowLevel Computer Use MCP 1.28.1 launched the exact canonical entry commands `--page lines`, `--page textsort`, and `--page textwrap` on separate private desktops and resolved one **852×880** window per launch. Its `PrintWindow` captures returned `rendered_ok` but were composition-white and invalid when inspected. The compositor-backed retry failed `show_headless_desktop` with Win32 error 5 before monitor capture, and the required run-winforge driver rejected blank/near-uniform fallbacks for all three routes. Exact processes/desktops were cleaned, all invalid images were deleted, and the artifact directory is empty. Keep the three parity rows `in-progress` with visual evidence `capture-blocked`; only hosted release-per-push proof, commit integration, and remote proof remain pending.
- 原生 source 而家有行工具、行排序同去重、文字換行三個專用 C++/WinRT renderer，共用標準 C++ `LineProcessing` core；Debug／Release build 各自 exit 0，兩個 core executable 各 **630/630**，包括 Line Processing **70/70**；catalog parity 亦通過 346 條固定 route 加五組動態家族。Managed Debug x64 solution build exit 0、0 errors，161 個 warning 只係嗰次實際 run 嘅歷史、非合約輸出。`-LineRoutesOnly` 專項 UIA 喺全部七個 registered alias 通過 **42/42**，完整自有原生 shell smoke 亦以 exit 0 通過 **348/348**；LowLevel MCP 1.28.1 喺獨立 private desktop 準確開過 `--page lines`、`--page textsort` 同 `--page textwrap`，每次都解析到 **852×880** window；但 `PrintWindow` 圖雖然回報 `rendered_ok`，檢查後係 composition-white 無效畫面。Compositor retry 因 Win32 錯誤 5 失敗，run-winforge 三頁亦拒絕空白 fallback；全部 process／desktop／無效圖已清理、artifact directory 已清空，所以 visual 保持 `capture-blocked`。只剩 release 同 Git 遙距證明待完成。

## Earlier native utility checkpoint / 較早原生工具檢查點

- Commits `828c32791e1f135d2a46848e9283087ef8ec9156` and `ce879cc6626eae328ec72e0143761c0edfbae340` deliver genuine native Text Diff, Aspect Ratio, and CSS Unit Converter routes. Local `main` was fast-forwarded through the hardened tip before the final handoff record.
- Native Debug/Release core is **560/560** each; Design Tools is **94/94**, Text Diff is **27/27**, focused utility UI Automation is **39/39**, full owned shell is **300/300**, and catalog parity covers 346 fixed routes plus five dynamic families. The renderer ledger is **21 in-progress / 325 not-started** because compositor capture remains the only incomplete evidence dimension.
- LowLevel Computer Use MCP 1.28.1 launched all three pages on separate named headless desktops from an immutable 294-file stage, confirmed launch PIDs, resolved 1320×880 WinUI frames, captured/inspected 1304×841 clients, and performed exact cleanup. Each client was uniformly white; the repository driver independently rejected the same blank fallback. No blank image was promoted.
- Hosted run 29663954724 published exact-SHA prerelease `native-v1.0.37` with installer and portable zip assets for `ce879cc6626eae328ec72e0143761c0edfbae340`.
- 呢批真正原生化文字差異比對、長寬比計算同 CSS 單位換算；功能證據全綠，視覺只因 compositor 無法擷取而保持 `capture-blocked`。完成後仍有 325 條固定 route 加五組動態家族要繼續。

## Completion and Git / 完成與 Git

- Treat every bounded task as incomplete until its intentional bilingual commit has been pushed.
- Complete each task on a temporary `codex/` branch, merge it into `main`, and push `main` before calling it finished.
- After pushing `main`, fetch the remote and prove both the task commit and branch tip are ancestors of `origin/main`; also verify the expected changed files exist in the remote `main` tree.
- Only after that proof may an agent delete the merged remote/local branch or its worktree. Never delete unmerged, unpushed, or unverified work.
- Never force-push and never discard unrelated user changes.

## Documentation and evidence / 文件與證據

- A feature or page change includes its documentation work: update the relevant `README.md`, `docs/wiki/`, `docs/`, and GitHub Pages content under `design/content/wiki/`, keeping English and Cantonese aligned with the shipped UI.
- For visual changes, capture and inspect current, high-detail evidence for every changed page; replace stale canonical screenshots and matching wiki/Pages assets. If capture is blocked, record the exact blocker and do not claim visual verification.
- Generated documentation remains generated: use its repository generator instead of hand-editing generated feature/reference artifacts.

## LowLevel headless operation / LowLevel 無頭運作

- Use the inexpensive LowLevel Computer Use MCP runner for task commands and all app interaction.
- Launch and drive WinForge only in a dedicated headless desktop. Do not open a visible instance that can steal the user's focus.
- Capture UI evidence through LowLevel MCP from that headless desktop, inspect it, and close test processes/desktops when their evidence is complete.

## Security and hygiene / 安全與整潔

- Never persist, log, copy, or screenshot secrets unnecessarily.
- Keep the task focused; preserve unrelated work already present in the worktree.
- Record verification results honestly, including failures and recovery steps.
