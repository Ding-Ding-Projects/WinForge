# WinForge — Handoff reference (per feature) · 交接參考（逐項功能）

## Current 2026-07-19 native Unit Price controlled integration — local gates green · 2026-07-19 原生單位價格受控整合 — 本機 gate 已通過

**EN.** `priceper`, `unitprice`, and `module.unitprice` are now integrated as a standard-C++ `UnitPrice` core plus genuine C++/WinRT renderer. Debug/Release native builds have 0 errors; both cores are **828/828** including Unit Price **13/13**; focused Unit Price UIA is **15/15**; Utility UIA is **39/39** including CSS Unit Converter; catalog parity is 346+5 with 319 registry and 346 ledger rows; and the native installer contract passes. Accounting is **34/346** (`34 in-progress / 312 not-started`). The broad aggregate did not yield a captured final footer, so no full-shell pass is claimed. LowLevel MCP is not callable and the driver rejected a blank fallback; no PNG was promoted and visual stays `capture-blocked`. The C++-only release boundary is unchanged.

**粵語.** `priceper`、`unitprice` 同 `module.unitprice` 而家已整合成標準 C++ `UnitPrice` core 加真正 C++/WinRT renderer。Debug／Release 0 errors、core 各 **828/828**（Unit Price **13/13**）、專項 UIA **15/15**、包括 CSS 嘅 Utility UIA **39/39**、catalog parity 346+5（319 registry／346 ledger）同 installer contract 都通過；計數係 **34/346**（`34 in-progress / 312 not-started`）。廣泛 aggregate 冇擷取到最後 footer，所以唔聲稱 full-shell pass。LowLevel MCP 不可呼叫、driver 拒絕空白 fallback，冇升格 PNG，visual 保持 `capture-blocked`；只限 C++ release 界線冇變。

## Current 2026-07-19 native Namespaced UUID controlled integration — local gates green · 2026-07-19 原生具名空間 UUID 受控整合 — 本機 gate 已通過

**EN.** `uuid5`, `uuidv5`, and `module.uuidv5` are now a local standard-C++/C++/WinRT RFC 4122 v3/v5 renderer with managed-compatible namespace parsing, UTF-16/U+180E parity, bulk output, language retention, reset, and explicit-only Copy. Debug/Release builds are 0 errors; both cores are **815/815**, focused UUID UIA is **21/21**, catalog parity is 346+5 with 319 registry and 346 ledger rows, and the native installer contract passes. The ledger is **33/346 fixed routes** (`33 in-progress / 313 not-started`). LowLevel MCP is not callable here; fresh capture and the aggregate shell remain pending, so no visual success is claimed. The controlled merge keeps C++ as the sole release publisher.

**粵語.** 三個 UUID alias 而家係本機標準 C++／C++/WinRT RFC 4122 v3/v5 renderer，有 managed 相容 namespace parser、UTF-16／U+180E parity、bulk 輸出、語言保留、reset 同只限明確 Copy。Debug／Release 0 errors、core 各 **815/815**、UUID UIA **21/21**、catalog parity 346+5（319 registry、346 ledger）同 installer contract 通過。ledger 係 **33/346**（`33 in-progress / 313 not-started`）。呢度冇可呼叫 LowLevel MCP；最新 capture 同 aggregate shell 未完成，所以未聲稱 visual success。受控 merge 保持 C++ 係唯一 release publisher。

**Final update / 最終更新：** the controlled native shell now passes **469/469**. The fresh `uuid5` driver rejected the blank/near-uniform PrintWindow fallback after `CopyFromScreen` was unavailable; no PNG was retained, so visual evidence is `capture-blocked`. · 受控 native shell 而家通過 **469/469**。最新 `uuid5` driver 因 `CopyFromScreen` 不可用而拒絕空白／近乎單色 PrintWindow fallback，冇保留 PNG，所以 visual 係 `capture-blocked`。

## Current 2026-07-19 native Slugify controlled integration — hosted release proven · 2026-07-19 原生網址別名受控整合 — hosted 發佈已證明
## Historical 2026-07-19 native Unit Price feature branch (pre-integration) · 歷史原生單位價格功能分支（整合前）

**EN.** `priceper`, `unitprice`, and `module.unitprice` are a genuine C++/WinRT renderer over pure standard-C++ `UnitPrice`. It keeps managed valid-row, free/infinity/tie, formatting, Add/remove/reset, language-retention, and explicit-only Copy contracts. Feature-branch evidence: Debug/Release 0-error builds; core **814/814** in each configuration (Unit Price **13/13**); catalog parity **346 + 5**; focused UIA **15/15**; and **33/346** renderer accounting. A broad smoke passed all Unit Price checks but was stopped after a later unrelated CSS stall, so no full-shell pass is claimed. LowLevel MCP is unavailable; the driver rejected a blank fallback and no screenshot was promoted. This branch changes no release workflow/policy and awaits controlled C++-only integration.

**粵語.** `priceper`、`unitprice` 同 `module.unitprice` 係真正 C++/WinRT renderer 加純標準 C++ `UnitPrice`。保留 managed 有效行、免費／infinity／平手、格式、Add/remove/reset、語言保留同只限明確 Copy。功能分支證據：Debug／Release 0 errors、core 各 **814/814**（Unit Price **13/13**）、catalog parity **346 + 5**、專項 UIA **15/15**、renderer **33/346**。廣泛 smoke 通過晒 Unit Price，但後面不相關 CSS stall 後已停止，所以唔聲稱 full shell pass。LowLevel MCP 未可用，driver 拒絕空白 fallback，冇升格截圖。呢個分支唔改 release workflow／policy，等受控只限 C++ 整合。

## Current 2026-07-19 native Slugify controlled integration — local validation green · 2026-07-19 原生網址別名受控整合 — 本機驗證通過

**EN.** `slug`, `slugify`, and `module.slugify` are now a genuine C++/WinRT renderer over the standard-C++ `Slugify` core, integrated with existing native Reference Text and Morse routes. It preserves managed line/blank/diacritic/Unicode/separator/case/UTF-16-preview semantics, route lifecycle, all three language modes, and explicit-only clipboard Copy. Debug and Release x64 builds exit 0 with 0 errors; both cores pass **801/801**; catalog parity passes **346 fixed routes + five dynamic families**; focused UIA is **29/29** Reference Text, **13/13** Morse, and **12/12** Slugify; and the exhaustive native shell is **441/441**. Renderer accounting is **32/346 fixed routes**, **32 `in-progress` / 314 `not-started`**. The local LowLevel checkout is not callable here; the required native `slugify` driver rejected its blank/near-uniform fallback after `CopyFromScreen` was unavailable. No PNG or canonical screenshot changed, so the five routes remain `capture-blocked`. The controlled `main` push preserves the C++-only release boundary and will receive remote gate verification.

**粵語.** `slug`、`slugify` 同 `module.slugify` 而家係真正 C++/WinRT renderer，同現有 Reference Text 同 Morse route 受控整合。保留 managed 分行／空白／重音／Unicode／分隔符／大小寫／UTF-16／預覽、route lifecycle、三種語言同只限明確 Copy。Debug／Release 0 errors、core 各 **801/801**、catalog parity、Reference Text **29/29**、Morse **13/13**、Slugify **12/12** 同完整 shell **441/441** 已通過；renderer 計 **32/346**，ledger **32 `in-progress` / 314 `not-started`**。本機 LowLevel MCP 今個 session 不可呼叫；`slugify` driver 因 `CopyFromScreen` 唔可用而拒絕空白／近乎單色 fallback，冇 PNG 或 canonical 圖變更，所以五條 route 保持 `capture-blocked`。受控 `main` push 會保持只限 C++ release 界線，再做遠端 gate 驗證。

**Hosted proof / Hosted 證明：** Slugify tip `bb853ef3` and merge `88672704` are ancestors of `origin/main`. [Run 29702620758](https://github.com/codingmachineedge/WinForge/actions/runs/29702620758) passed and published non-draft [`native-v1.0.70`](https://github.com/codingmachineedge/WinForge/releases/tag/native-v1.0.70) at `88672704`, exactly setup + native ZIP. Successful site-data [run 29702620742](https://github.com/codingmachineedge/WinForge/actions/runs/29702620742) committed `ef822c1d`; dispatched [run 29702777841](https://github.com/codingmachineedge/WinForge/actions/runs/29702777841) passed and made stable [`native-v1.0.71`](https://github.com/codingmachineedge/WinForge/releases/tag/native-v1.0.71) Latest at that exact main SHA, again exactly setup + native ZIP. · Slugify tip `bb853ef3` 同 merge `88672704` 都係 `origin/main` 祖先。run 29702620758 通過並準確喺 `88672704` 發佈非 draft `native-v1.0.70`，只有 setup + native ZIP。成功 site-data run 29702620742 提交 `ef822c1d`；dispatch run 29702777841 通過，將嗰個準確 main SHA 嘅 stable `native-v1.0.71` 設為 Latest，仍然準確只有 setup + native ZIP。

## Historical 2026-07-19 native Reference Text + Morse integration — hosted release proven · 2026-07-19 歷史原生參考文字及摩斯整合 — hosted 發佈已證明

**EN.** The controlled main merge combines Phonetic Speller, Box & Banner Text, HTML Entities, and Morse Code as genuine C++/WinRT renderers over standard-C++ cores. Debug and Release x64 solution builds have 0 errors; both core suites pass **783/783**; catalog parity passes **346 fixed routes + five dynamic families**; focused Reference Text UI Automation passes **29/29**; focused Morse UI Automation passes **13/13**; and the exhaustive native shell passes **430/430**. Renderer accounting is **31/346 fixed routes**, leaving **315** plus five dynamic families, with **31 `in-progress` / 315 `not-started`**. The local LowLevel MCP checkout is not callable in this session; the fresh `morse` driver rejected a blank/near-uniform fallback after `CopyFromScreen` was unavailable, so no image was promoted and all four routes remain `capture-blocked`. Merge `dc95f0f8` passed [native run 29700338773](https://github.com/codingmachineedge/WinForge/actions/runs/29700338773) and published stable [`native-v1.0.64`](https://github.com/codingmachineedge/WinForge/releases/tag/native-v1.0.64) at that SHA; generated-data main `5e2636fd` passed [run 29700486762](https://github.com/codingmachineedge/WinForge/actions/runs/29700486762) and published current Latest [`native-v1.0.65`](https://github.com/codingmachineedge/WinForge/releases/tag/native-v1.0.65), exactly setup + portable native ZIP.

**粵語.** 受控 main merge 合併咗拼讀字母表、文字方框／橫幅、HTML 實體同 Morse Code 四個真正 C++/WinRT renderer。Debug／Release 0 errors、core 各 **783/783**、catalog parity、Reference Text UIA **29/29**、Morse UIA **13/13** 同完整 shell **430/430** 都通過。計數係 **31/346**，仲有 **315** 條固定 route 同五組動態家族，ledger 係 **31 `in-progress` / 315 `not-started`**。LowLevel MCP 今個 session 不可呼叫，`morse` driver 因 `CopyFromScreen`／空白 fallback 受阻，冇提升圖，所以四條 route 保持 `capture-blocked`。`dc95f0f8` 已通過 run 29700338773 同發佈準確 native-v1.0.64；generated-data main `5e2636fd` 通過 run 29700486762 並發佈 current Latest native-v1.0.65，準確只有原生 setup 同 portable ZIP。

## Historical pre-Morse Reference Text wave · 歷史整合前原生參考文字批次
## 2026-07-19 native Slugify wave — feature-only handoff · 2026-07-19 原生網址別名批次 — 只限功能交接

**EN.** `slug`, `slugify`, and `module.slugify` now use a dedicated C++/WinRT page backed by the standard-C++ `Slugify` core. Managed line/blank/diacritic/Unicode/separator/case/UTF-16-length semantics, live preview, route reset, localized state retention, and explicit-only Copy are preserved. Debug and Release builds exit 0 with 0 errors; core is **777/777** in each configuration (Slugify **18/18**), catalog parity is 346+5, and focused UIA is **12/12** across all aliases including explicit Copy. LowLevel MCP is unavailable in this session; the required driver rejected a blank fallback after `CopyFromScreen` was unavailable, retaining no PNG or process. The route remains `in-progress` / `capture-blocked`. The branch changes no workflow or release policy and is awaiting controlled integration by the native-only release owner.

**粵語.** 三個網址別名 alias 已有專用 C++/WinRT 頁同 `Slugify` core；managed 分行、空白、重音、Unicode、分隔符、大小寫、UTF-16 長度、預覽、重設、本地化狀態同只限明確 Copy 都有保留。Debug／Release 0 errors，core 各 **777/777**（Slugify **18/18**），catalog parity 346+5，全部 alias UIA **12/12**。今次冇 LowLevel MCP；`CopyFromScreen` 唔可用後 driver 拒絕空白 fallback，冇 PNG／冇殘留 process，所以 route 保持 `in-progress`／`capture-blocked`。分支唔改 workflow／release policy，等 native-only release owner 受控整合。

## 2026-07-19 native reference-text wave — focused routes green; visual capture blocked · 2026-07-19 原生參考文字批次 — 專項 route 通過；視覺擷取受阻

**EN.** Phonetic Speller, Box & Banner Text, and HTML Entities now have dedicated C++/WinRT renderers over the shared standard-C++ `ReferenceText` core. They preserve three phonetic alphabets and managed UTF-16 code-unit row, digit, and option behavior; eight box/comment styles, three alignments, bounded padding, titles and multiline/tab-aware Unicode layout; and named/numeric entity encode/decode behavior, invalid UTF-16 safety, managed-compatible scanning and a local 50-row bilingual reference. All processing is local, and clipboard writes require explicit Copy.

Renderer accounting is **30/346 fixed routes**, with **316 fixed routes** plus five dynamic families remaining; the ledger is **30 `in-progress` / 316 `not-started`**. Fresh Debug and Release x64 solution builds each exit 0 with 0 errors, Debug and Release core each pass **759/759**, catalog parity passes all 346 fixed routes plus five dynamic families, focused reference-text UIA passes **29/29** across eight aliases, and the full native shell passes **417/417 (417 passed, 0 failed)**. LowLevel Computer Use MCP is unavailable in this Codex session. The fresh post-integration `htmlentities` repository-driver retry found `CopyFromScreen` unavailable and rejected a blank/near-uniform `PrintWindow` client. No PNG was created or retained, no process remained, and no canonical screenshot changed, so visual evidence is `capture-blocked`.

The handoff is feature-only: it changes no workflow or release policy, and controlled integration preserves that boundary while the native release gate supplies remote verification.

**粵語.** 三項參考文字工具已有專用 C++/WinRT renderer 同 `ReferenceText` core；Renderer 計 **30/346**，仲有 **316** 條固定 route 同五組動態家族。最新 Debug／Release build 各 0 errors、兩個 core 各 **759/759**、catalog parity、八個 alias UIA **29/29** 同完整 native shell **417/417（417 passed、0 failed）** 已通過。今次 session 冇 LowLevel MCP；整合後最新 HTML driver 重試拒絕空白 fallback，冇建立或保留 PNG、冇殘留 process、冇改 canonical 截圖，所以 visual 係 `capture-blocked`。呢個 handoff 只改功能，唔改 workflow／release policy；受控整合會守住界線，再由 native release gate 做遙距驗證。

## Historical isolated Native Morse Code branch · 歷史獨立原生摩斯電碼功能分支

**EN.** module.morse and alias morse now have a genuine C++/WinRT renderer backed by standard-C++ Morse logic. It preserves managed-compatible UTF-16 encode/decode aliases, unique unknown-symbol reporting, bounded WPM and flash timing, explicit Copy, and lifecycle-safe dispatcher-timer cleanup. The feature commit is being merged with the current native shell so its final aggregate evidence is recorded only after fresh controlled validation.

**Visual evidence.** LowLevel MCP is present locally but not callable in this Codex session, so none is claimed. The required driver successfully launched the native page but rejected its blank/near-uniform fallback because CopyFromScreen is unavailable. No invalid image was retained or promoted; visual evidence remains capture-blocked.

**粵語.** module.morse 同 morse 而家有真正 C++/WinRT renderer 同標準 C++ Morse 邏輯，保留 managed 相容 UTF-16 編碼／解碼別名、唯一未知符號報告、有界 WPM 同閃燈計時、明確 Copy 同 lifecycle-safe dispatcher-timer cleanup。功能提交正同目前 native shell 受控整合；最後總驗證數字只會喺最新受控驗證後記錄。LowLevel MCP 雖然喺本機但呢個 session 唔可呼叫；driver 開到原生頁但因 CopyFromScreen 唔可用而拒絕空白／近乎單色 fallback。無效圖冇保留或提升，visual 保持 capture-blocked。

## Historical native text-analysis collation wave — exhaustive shell green; visual capture blocked · 歷史原生文字分析排序批次 — 完整 shell 通過；視覺擷取受阻

**EN.** Text Statistics, Word Frequency, and String Compare now have dedicated C++/WinRT renderers over the shared standard-C++ `TextAnalysis` core. The implementations preserve local UTF-16/CJK statistics, sentence/paragraph/average/time/Flesch/top-ten results; word/bigram/Unicode-scalar frequency, managed options/stop words, current-culture ordering, count/bar/percentage rows and exact CSV; and normalization, UTF-16, OSA/Jaro–Winkler/longest-common metrics, the greater-than-2,000 guard, subquadratic exact greedy Jaro, and route-exit cleanup. Word Frequency and String Compare write the clipboard only after explicit Copy; Text Statistics is side-effect free.

Commit `c6f8a24d52e4949596efd58e070e0601a2939511` aligns native Word Frequency `CurrentCultureIgnoreCase` with .NET width/kana tailoring, stable canonical-equivalent ordering, and a shared initialized collator, while removing comparator serialization and per-edit polite live announcements. The named total remains queryable; attribution is in `THIRD-PARTY-NOTICES.txt`.

Renderer accounting is **27/346 fixed routes**, with **319 fixed routes** plus five dynamic families remaining; the ledger remains **27 `in-progress` / 319 `not-started`**. Debug/Release core is **717/717** each, focused Text Analysis **87/87**, seven-alias UIA **40/40**, post-c6 exhaustive shell **388/388 (388 passed, 0 failed)**, and catalog parity covers 346 fixed routes plus five families. The current native `wordfreq` driver retry rejected a blank/near-uniform fallback and retained no PNG. The local LowLevel checkout was present but not callable in this Codex session, so visual evidence remains `capture-blocked` without claiming LowLevel proof.

Feature `fc2b76e52171e4f81ab1d15f9fb1da5818791171` passed hosted branch run [29673079883](https://github.com/codingmachineedge/WinForge/actions/runs/29673079883) and published exact-SHA prerelease [native-v1.0.43](https://github.com/codingmachineedge/WinForge/releases/tag/native-v1.0.43). Merge `f7a9eec44aeffdf829f5c07f5eeb364f08a7677f` passed hosted `main` run [29673310778](https://github.com/codingmachineedge/WinForge/actions/runs/29673310778) and published stable exact-SHA [native-v1.0.44](https://github.com/codingmachineedge/WinForge/releases/tag/native-v1.0.44). Both have exactly setup + portable ZIP. Independent download verification matched digests and found 292 ZIP entries, 48 PEs, zero CLR/apphost/forbidden managed/build artifacts, and AMD64 PE32+ `WinForge.exe`.

`gh release create --latest` left `/releases/latest` at historical managed `v1.0.256`. A later bare `gh release edit native-v1.0.44 --latest` was insufficient because release 44 was observed as `prerelease=true` at that edit timestamp, so Latest fell back to managed. Explicit `gh release edit native-v1.0.44 --latest --prerelease=false --draft=false` restored stable/non-draft release 44 and exact `/releases/latest` at `f7a9eec44aeffdf829f5c07f5eeb364f08a7677f` with two native assets.

The former 53da branch run is historical. Current enforcement is proven by `1732af0b458454f144d1ac32be222b9e9015e5c5` [run 29694567950](https://github.com/codingmachineedge/WinForge/actions/runs/29694567950), which passed the staged site-data no-op guard and published native-only `native-v1.0.55`, and by integration `03b1e66f21b07c28fe2bd0b30ef60fe4af134e59` [main run 29695569334](https://github.com/codingmachineedge/WinForge/actions/runs/29695569334), which published stable, non-draft [native-v1.0.57](https://github.com/codingmachineedge/WinForge/releases/tag/native-v1.0.57) at the exact SHA. `/releases/latest` resolved to exactly `WinForge-Native-Setup.exe` and `WinForge-native-x64-1.0.57.zip`—no managed release. The paired site-data [run 29695569335](https://github.com/codingmachineedge/WinForge/actions/runs/29695569335) staged its output first and successfully logged `No change to design/winforge-data.js.`

**粵語.** 三項文字分析 Renderer 計 **27/346**，仲有 **319** 條固定 route 同五組動態家族，core 各 **717/717**、Text Analysis **87/87**、UIA **40/40**、完整 shell **388/388**，visual 仍係 `capture-blocked`。c6 對齊闊度／假名排序、canonical-equivalent 穩定次序同共用 collator；冇每次比較序列化或者每次修改嘅 polite announcement。`1732af0b`／run 29694567950 證明 site-data no-op guard，main `03b1e66f`／run 29695569334 發佈 stable `native-v1.0.57`；`/releases/latest` 準確只有原生 setup 同 ZIP，冇 managed release。

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
- CI: `.github/workflows/native-release.yml` is the sole publisher and emits exactly the C++ portable ZIP plus `WinForge-Native-Setup.exe`. `1732af0b` run 29694567950 proved staged site-data no-op handling; main `03b1e66f` run 29695569334 published stable `native-v1.0.57`, and `/releases/latest` resolved to that exact SHA with only the native setup and ZIP—no managed app. The earlier 29673965179 record is historical.

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
