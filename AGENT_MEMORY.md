# Persistent Agent Task Memory / 長期代理任務記憶

This file is the durable execution contract for every agent working in this repository. It supplements, and never weakens, `AGENTS.md`.

## Latest native text-analysis and release checkpoint (2026-07-19) / 最新原生文字分析及發佈檢查點（2026-07-19）

- Native Text Statistics, Word Frequency, and String Compare have dedicated C++/WinRT renderers over standard-C++ `TextAnalysis`. Commit `c6f8a24d52e4949596efd58e070e0601a2939511` tightens Word Frequency `CurrentCultureIgnoreCase` parity with managed .NET: ICU applies .NET Runtime width/kana tailoring, folds fullwidth case while retaining width/kana significance, preserves stable canonical-equivalent ordering, and shares an initialized collator without per-comparison serialization. The named total remains queryable without a per-edit polite live announcement; the required MIT attribution is in `THIRD-PARTY-NOTICES.txt`.
- `HasNativeRenderer` and the ledger account for **27/346 fixed routes**, leaving **319 fixed routes** plus five dynamic families. The ledger remains exactly **27 `in-progress` / 319 `not-started`**: a pending renderer is never called ported, and visual capture remains independently blocked.
- Current native evidence is green: Debug and Release core each pass **717/717**, including Text Analysis **87/87**; focused `-TextAnalysisRoutesOnly` UI Automation passes **40/40**; the full native shell passes **388/388 (388 passed, 0 failed)**; and catalog parity covers all 346 fixed routes plus five dynamic families.
- The repo-local LowLevel Computer Use MCP checkout exists but its tools were not callable in this Codex session. The fresh native `wordfreq` repository-driver retry rejected a blank/near-uniform `PrintWindow` fallback because `CopyFromScreen` was unavailable; it retained no PNG. Do not claim new LowLevel evidence, reuse an image, or change the three rows from `capture-blocked` / `in-progress` until a current valid frame is inspected.
- Current native-only release proof: merge `03b1e66f21b07c28fe2bd0b30ef60fe4af134e59` passed hosted [main run 29695569334](https://github.com/codingmachineedge/WinForge/actions/runs/29695569334) and published stable, non-draft [`native-v1.0.57`](https://github.com/codingmachineedge/WinForge/releases/tag/native-v1.0.57) at that exact SHA. `/releases/latest` resolved to it with exactly `WinForge-Native-Setup.exe` and `WinForge-native-x64-1.0.57.zip`—no managed app release. The site-data [run 29695569335](https://github.com/codingmachineedge/WinForge/actions/runs/29695569335) staged before checking its diff and completed the normalized no-op as `No change to design/winforge-data.js.` The sole publisher remains `.github/workflows/native-release.yml`; managed site generation does not publish releases.
- 原生文字統計、詞頻統計同字串相似度而家有專用 C++/WinRT renderer，同時共用標準 C++ `TextAnalysis`。Commit `c6f8a24d52e4949596efd58e070e0601a2939511` 進一步對齊詞頻 `CurrentCultureIgnoreCase` 同 managed .NET：ICU 用 .NET Runtime 闊度／假名 tailoring，fullwidth case 會折疊但闊度／假名仍有分別，canonical-equivalent 詞語保持穩定次序，並共用已初始化 collator，唔會每次比較都序列化。命名 total 仍可查詢，但每次修改唔再發 polite announcement；MIT 歸屬資料喺 `THIRD-PARTY-NOTICES.txt`。
- Renderer 計 **27/346**，仲有 **319** 條固定 route 同五組動態家族，ledger 保持 **27 `in-progress` / 319 `not-started`**。Debug／Release core 各 **717/717**，包括 Text Analysis **87/87**；專項 UIA **40/40**；完整 native shell **388/388**；catalog parity 覆蓋 346 條固定 route 加五組家族。本機 LowLevel checkout 喺度但呢個 Codex session 冇可呼叫工具；最新 `wordfreq` driver 重試冇法擷取有效畫面，亦冇保留 PNG，所以 visual 保持 `capture-blocked`。
- 原生-only 發佈證明：merge `03b1e66f21b07c28fe2bd0b30ef60fe4af134e59` 通過 hosted main run 29695569334，並以同一 SHA 發佈 stable／non-draft `native-v1.0.57`。`/releases/latest` 準確解析到佢，只有 `WinForge-Native-Setup.exe` 同 `WinForge-native-x64-1.0.57.zip`，冇 managed app release。site-data run 29695569335 先 stage 再檢查 diff，並成功完成正規化無變更；唯一 publisher 仍係 `.github/workflows/native-release.yml`。

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
