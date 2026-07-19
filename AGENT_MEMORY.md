# Persistent Agent Task Memory / 長期代理任務記憶

This file is the durable execution contract for every agent working in this repository. It supplements, and never weakens, `AGENTS.md`.

## Latest native C++ feature and release checkpoint (2026-07-19) / 最新原生 C++ 功能及發佈檢查點（2026-07-19）

- Native Slugify (`slug`, `slugify`, `module.slugify`) now has a dedicated C++/WinRT renderer backed by standard-C++ `Slugify`. It preserves managed line boundaries, blank-line skipping, accent and explicit Unicode behavior, separator/case/UTF-16-length rules, first-line preview, fresh-route reset, localization state retention, and explicit-only Copy. Debug/Release builds exit 0 with 0 errors; both core executables pass **777/777**, including Slugify **18/18**; catalog parity covers 346 fixed routes plus five dynamic families; and focused `-SlugifyRoutesOnly -AllowClipboardMutation` passes **12/12**. The LowLevel MCP tools are not callable in this session; the required driver rejected a blank/near-uniform fallback after `CopyFromScreen` was unavailable, left no PNG/process, and the ledger row remains `in-progress` / `capture-blocked`. This is a feature-only `codex/native-slugify` handoff: do not mistake it for a main/release integration or modify its release-policy boundary. · 原生網址別名三個 alias 而家有專用 C++/WinRT renderer 同標準 C++ `Slugify`；Debug／Release 0 errors、core 各 **777/777**（Slugify **18/18**）、catalog parity 346+5、專項 UIA **12/12**。今次冇可呼叫 LowLevel MCP，driver 拒絕空白 fallback，冇 PNG／冇 process；ledger 保持 `in-progress`／`capture-blocked`。呢個只係 `codex/native-slugify` 功能交接，唔係 main／release 整合，唔好改佢嘅 release-policy 界線。
- Native Phonetic Speller, Box & Banner Text, and HTML Entities have dedicated C++/WinRT renderers backed by standard-C++ `ReferenceText`. They preserve the managed alphabets, UTF-16 row behavior, eight box/comment styles, Unicode-aware layout, named/numeric entity handling, invalid-UTF-16 safety, and 50-row bilingual reference; all are local-only and Copy is explicit-only.
- Commit `c6f8a24d52e4949596efd58e070e0601a2939511` remains the Text Analysis collation checkpoint: its shared ICU collator follows .NET width/kana tailoring, preserves canonical-equivalent order, avoids comparator serialization and per-edit polite announcements, and carries the required attribution in `THIRD-PARTY-NOTICES.txt`.
- Fresh controlled integration evidence is green: Debug/Release x64 builds have 0 errors, core is **759/759** in each configuration, catalog parity covers 346 fixed routes plus five dynamic families, focused `-ReferenceTextRoutesOnly` UI Automation is **29/29** across eight aliases, and the full native shell is **417/417 (417 passed, 0 failed)**. The earlier c6 snapshot remains **717/717** core (Text Analysis **87/87**), **40/40** focused UIA, and **388/388** shell.
- `HasNativeRenderer` and the ledger now account for **30/346 fixed routes**, leaving **316** plus five dynamic families: exactly **30 `in-progress` / 316 `not-started`**. A pending renderer is never called complete.
- LowLevel Computer Use MCP is not callable in this Codex session. The fresh `wordfreq` and `htmlentities` driver attempts rejected blank/near-uniform `PrintWindow` fallbacks because `CopyFromScreen` was unavailable; no PNG was retained or substituted, no canonical screenshot changed, and affected rows remain `capture-blocked`.
- Historical hosted release proof: `03b1e66f21b07c28fe2bd0b30ef60fe4af134e59` passed [main run 29695569334](https://github.com/codingmachineedge/WinForge/actions/runs/29695569334) and published stable, non-draft [`native-v1.0.57`](https://github.com/codingmachineedge/WinForge/releases/tag/native-v1.0.57) at that SHA with exactly `WinForge-Native-Setup.exe` and `WinForge-native-x64-1.0.57.zip`; [site-data run 29695569335](https://github.com/codingmachineedge/WinForge/actions/runs/29695569335) staged first and logged `No change to design/winforge-data.js.` `.github/workflows/native-release.yml` remains the sole publisher.
- 原生拼讀字母表、文字方框／橫幅同 HTML 實體而家有專用 C++/WinRT renderer，同時共用標準 C++ `ReferenceText`；保留受控版字母表、UTF-16 列、八種框／註解風格、Unicode 排版、實體處理、無效 UTF-16 安全同 50 項雙語參考，而且只會喺明確 Copy 後改剪貼簿。`c6f8a24d` 仍然係 Text Analysis 排序檢查點：ICU 跟 .NET 闊度／假名 tailoring、保留 canonical-equivalent 次序、唔會每次比較序列化或者每次修改做 polite announcement。最新受控整合證據係 Debug／Release 0 errors、core 各 **759/759**、catalog parity、八個 alias UIA **29/29** 同完整 native shell **417/417（417 passed、0 failed）**。c6 較早快照係 core **717/717**（Text Analysis **87/87**）、UIA **40/40**、shell **388/388**。renderer 而家係 **30/346**，仲有 **316** 條同五組家族，ledger 保持 **30 `in-progress` / 316 `not-started`**。今次 session 冇可呼叫 LowLevel MCP；`wordfreq` 同整合後最新 `htmlentities` driver 都拒絕空白 fallback，冇 PNG／冇 canonical 截圖變更，所以 visual 保持 `capture-blocked`。歷史 hosted `native-v1.0.57` 證明只有原生兩個 asset，而 `.github/workflows/native-release.yml` 仍係唯一 publisher。

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
