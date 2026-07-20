# WinForge Full Development Handoff

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

**Remote proof / 遙距證明：** Unit Price merge `37cc0e8a1d4605864756751265d379a954978b27` is an ancestor of `origin/main`. [Native run 29706847786](https://github.com/codingmachineedge/WinForge/actions/runs/29706847786) passed every native gate and published stable `native-v1.0.76` exactly at that SHA with only `WinForge-Native-Setup.exe` and `WinForge-native-x64-1.0.76.zip`. Its successful site-data run committed `fbaff0788`; dispatched [native run 29707001900](https://github.com/codingmachineedge/WinForge/actions/runs/29707001900) passed and made stable `native-v1.0.77` Latest at that current-main SHA, again exactly the native setup and ZIP. · Unit Price merge 係 `origin/main` 祖先；native run 29706847786 通過並準確出 stable `native-v1.0.76`，只有原生 setup 同 ZIP。site-data 提交 `fbaff0788` 後，dispatch run 29707001900 通過並將準確 current-main SHA 嘅 `native-v1.0.77` 設為 Latest，仍然只得兩個原生 asset。

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
- **Visual / 視覺：** the local LowLevel checkout is present but no headless MCP tool is callable in this session. The required driver found `CopyFromScreen` unavailable and rejected a blank/near-uniform `PrintWindow` fallback. No PNG or canonical screenshot changed, no process remained, and visual evidence is `capture-blocked`.
- **Integration state / 整合狀態：** feature work remains on `codex/native-unitprice` rooted at `ef822c1dc64a30662e2a48af998d6f0387422667`; it is deliberately unpushed while the C++-only release owner controls rebase, main, CI, and release verification.

**粵語摘要：** Unit Price 三個 alias 係純標準 C++ core 加 C++/WinRT renderer，唔改 workflow／release policy；Debug／Release 0 errors、core 各 **814/814**（Unit Price **13/13**）、catalog parity 同 UIA **15/15** 已通過。廣泛 shell 已通過晒 Unit Price，但不相關 CSS block 停住後按 owner 指示清理，唔當 full-shell 完成。LowLevel MCP 不可呼叫、driver 拒絕空白 fallback，冇 PNG／冇 process，所以 visual 係 `capture-blocked`。分支刻意未 push，等只限 C++ release owner 受控整合。

## Current 2026-07-19 native Slugify controlled integration — local validation green · 2026-07-19 原生網址別名受控整合 — 本機驗證通過

- **Scope / 範圍：** `slug`, `slugify`, and `module.slugify` are now a genuine C++/WinRT renderer over the standard-C++ `Slugify` core, integrated with existing native Reference Text and Morse routes. It preserves managed line/blank/diacritic/Unicode/separator/case/UTF-16-preview semantics, route lifecycle, all three language modes, and explicit-only clipboard Copy.
- **Current evidence / 目前證據：** Debug and Release x64 solution builds exit 0 with 0 errors; both core suites pass **801/801**; catalog parity passes **346 fixed routes + five dynamic families**; focused UIA is **29/29** Reference Text, **13/13** Morse, and **12/12** Slugify; the exhaustive native shell is **441/441**. `HasNativeRenderer` and the ledger are **32/346 fixed routes**, **32 `in-progress` / 314 `not-started`**.
- **Visual evidence / 視覺證據：** the local LowLevel MCP checkout exists but is not callable in this Codex session. The required native `slugify` driver found `CopyFromScreen` unavailable and rejected a blank/near-uniform `PrintWindow` fallback. No PNG was retained or promoted and no canonical screenshot changed; these five routes remain honestly `capture-blocked`.
- **Release boundary / 發佈界線：** the integration does not change workflows or release policy. It remains a pure native C++ feature; the controlled `main` push will be verified against the existing C++-only release gate before it is called remotely complete.

**粵語摘要：** Slugify 三個 alias 已同現有 Reference Text 同 Morse 原生 route 受控整合，保留 managed 分行／空白／重音／Unicode／分隔符／大小寫／UTF-16／預覽、route lifecycle、三種語言同只限明確 Copy。Debug／Release 0 errors、core 各 **801/801**、catalog parity、Reference Text **29/29**、Morse **13/13**、Slugify **12/12** 同完整 shell **441/441** 已通過；計數係 **32/346**，ledger **32 `in-progress` / 314 `not-started`**。LowLevel MCP 今個 session 不可呼叫，driver 因 `CopyFromScreen`／空白 fallback 受阻，冇 PNG 或 canonical 圖變更，所以五條 route 保持 `capture-blocked`。呢次唔改 workflow／release policy，受控 main push 會由現有只限 C++ release gate 驗證。

**Remote release proof / 遙距發佈證明：** Slugify tip `bb853ef3` and controlled merge `88672704` are ancestors of `origin/main`. [Native run 29702620758](https://github.com/codingmachineedge/WinForge/actions/runs/29702620758) passed all native gates and published non-draft [`native-v1.0.70`](https://github.com/codingmachineedge/WinForge/releases/tag/native-v1.0.70) exactly at `88672704`, with only `WinForge-Native-Setup.exe` and `WinForge-native-x64-1.0.70.zip`. The successful [site-data run 29702620742](https://github.com/codingmachineedge/WinForge/actions/runs/29702620742) committed `ef822c1d`; its dispatched [native run 29702777841](https://github.com/codingmachineedge/WinForge/actions/runs/29702777841) passed and promoted stable [`native-v1.0.71`](https://github.com/codingmachineedge/WinForge/releases/tag/native-v1.0.71) to Latest at that exact current-main SHA, again with exactly the two native assets. · Slugify tip `bb853ef3` 同受控 merge `88672704` 都係 `origin/main` 祖先。原生 run 29702620758 通過所有原生 gate，準確喺 `88672704` 發佈非 draft `native-v1.0.70`，只有 native setup 同 `WinForge-native-x64-1.0.70.zip`。成功 site-data run 29702620742 提交 `ef822c1d`；佢 dispatch 嘅原生 run 29702777841 通過，將準確 current-main SHA 嘅 stable `native-v1.0.71` 升做 Latest，仍然準確只有兩個原生 asset。

## Historical 2026-07-19 native Reference Text + Morse integration — hosted release proven · 2026-07-19 歷史原生參考文字及摩斯整合 — hosted 發佈已證明

- **Scope / 範圍：** the controlled main merge combines native Phonetic Speller, Box & Banner Text, HTML Entities, and Morse Code. All four have genuine C++/WinRT renderers over testable standard-C++ cores, local processing, explicit-only clipboard writes, route reset/lifecycle handling, localization, and accessible control surfaces.
- **Current evidence / 目前證據：** native Debug and Release x64 solution builds each exit 0 with 0 errors; Debug and Release core each pass **783/783**; catalog parity passes **346 fixed routes + five dynamic route families**; `-ReferenceTextRoutesOnly` passes **29/29**; `-MorseRoutesOnly` passes **13/13**; and the exhaustive native shell passes **430/430 (430 passed, 0 failed)**. `HasNativeRenderer` and the ledger are **31/346 fixed routes**, **31 `in-progress` / 315 `not-started`**; capture-blocked is never presented as port complete.
- **Visual evidence / 視覺證據：** the local LowLevel MCP checkout exists but is not callable in this Codex session, so no MCP evidence is claimed. The fresh native `morse` driver launch rejected its blank/near-uniform `PrintWindow` fallback because `CopyFromScreen` is unavailable. No PNG was retained or promoted and no canonical screenshot changed; all four routes remain honestly `capture-blocked`.
- **Integration and release proof / 整合及發佈證明：** merge `dc95f0f83390e370ecd22f3e239261b8bd2ead94` contains source tip `6d9c4ae8f25726cf7d17549310f34b33194d669f`; both it, `66bb415e5f1730bb128f4d649e493150da222d06`, and the Reference Text tip are ancestors of `origin/main`. Hosted [run 29700338773](https://github.com/codingmachineedge/WinForge/actions/runs/29700338773) passed every native build/test/parity/installer/release gate and published stable [`native-v1.0.64`](https://github.com/codingmachineedge/WinForge/releases/tag/native-v1.0.64) exactly at `dc95f0f8`, with only `WinForge-Native-Setup.exe` and `WinForge-native-x64-1.0.64.zip`. The paired site-data run committed `5e2636fdb84fcb691f3cd272e5dd9231cc427e21`; successful [run 29700486762](https://github.com/codingmachineedge/WinForge/actions/runs/29700486762) published stable [`native-v1.0.65`](https://github.com/codingmachineedge/WinForge/releases/tag/native-v1.0.65), the current `/releases/latest`, with exactly the two native assets. Downloaded v1.0.65 checksums match GitHub and its 292-entry ZIP contains one MZ `WinForge.exe` with no managed WinForge payload.

**粵語摘要：** 合併後有四個真正原生 renderer；Debug／Release 0 errors、core 各 **783/783**、catalog parity、Reference Text UIA **29/29**、Morse UIA **13/13** 同完整 shell **430/430** 已通過。計數係 **31/346**，仲有 **315** 條固定 route 同五組動態家族。LowLevel MCP 今個 session 不可呼叫，最新 `morse` driver 因 `CopyFromScreen`／空白 fallback 受阻，冇 PNG 或 canonical 圖變更，所以四條 route 保持 `capture-blocked`。`dc95f0f8` 嘅 hosted run 29700338773 已通過並發佈準確 native-v1.0.64；site-data 自動提交 `5e2636fd` 後嘅 run 29700486762 亦通過並發佈 current Latest native-v1.0.65，兩個 release 都只得原生 setup 同 ZIP，冇 managed app。

## Historical pre-integration Reference Text and Morse record · 歷史整合前參考文字及摩斯紀錄

### Morse Code feature / 摩斯電碼功能

- **Scope / 範圍：** module.morse plus morse alias is a genuine C++/WinRT renderer over the new standard-C++ Morse core. It covers managed-compatible UTF-16 encode/decode aliases, unique unknown-symbol reporting, bounded WPM/timing timeline, explicit Copy, and lifecycle-safe dispatcher-timer cleanup when playback finishes or the route/window closes.
- **Branch evidence / 分支證據：** native Debug and Release solution builds exit 0; Debug and Release core executables each pass **741/741**, including focused Morse **24/24**; -MorseRoutesOnly native UI Automation passes **13/13**; and native catalog parity covers 346 fixed routes plus five dynamic families. This branch's ledger is **28/346 fixed routes**, **28 in-progress / 318 not-started**.
- **Headless visual disposition / 無頭視覺處置：** LowLevel Computer Use MCP exists locally but is not callable in this Codex session, so no LowLevel evidence is claimed. The required native driver successfully launched --page morse, then rejected capture because CopyFromScreen was unavailable and the PrintWindow fallback was blank or near-uniform. No invalid image was retained, no canonical screenshot changed, and visual evidence is capture-blocked.
- **Integration state / 整合狀態：** feature commit 66bb415e5f1730bb128f4d649e493150da222d06 is in a controlled merge with current main. Fresh native build, core, UIA, parity, capture disposition, hosted CI, and native-only release evidence will replace the branch-only aggregate after integration.

### Reference Text routes / 參考文字 route
## 2026-07-19 native Slugify wave — focused routes green; visual capture blocked · 2026-07-19 原生網址別名批次 — 專項 route 通過；視覺擷取受阻

- **Scope / 範圍：** `slug`, `slugify`, and `module.slugify` now have a dedicated C++/WinRT page backed by standard-C++ `Slugify`. It preserves managed CRLF/LF/CR block conversion, blank-line skipping, diacritic normalization, default ASCII and opt-in Unicode categories, separator/case/max-length rules, first nonblank-line preview, route-entry reset, language rerender retention, and explicit-only clipboard Copy.
- **Inventory / 清單：** `HasNativeRenderer` and the provisional ledger account for **31/346 fixed routes**, leaving **315** fixed routes plus five dynamic families. The new row is `in-progress`; a renderer without valid visual evidence is never called complete.
- **Evidence green / 已通過證據：** Debug and Release x64 native solution builds each exit 0 with 0 errors; Debug and Release core each pass **777/777**, including Slugify **18/18**; catalog parity covers 346 fixed routes plus five dynamic families; focused `-SlugifyRoutesOnly` UI Automation passes **12/12** across all three aliases and covers explicit clipboard Copy.
- **Visual disposition / 視覺處置：** LowLevel MCP is not callable in this Codex session. The required repository-driver capture reached the fallback, reported `CopyFromScreen` unavailable, and rejected a blank/near-uniform `PrintWindow` client. No PNG was created, no worktree WinForge process remained, and no canonical screenshot changed. The ledger visual dimension is `capture-blocked`.
- **Integration boundary / 整合界線：** feature-only work: no workflow, release, tag, GitHub, or `main` mutation. The isolated `codex/native-slugify` commit must be integrated under the release owner’s controlled native-only publishing flow.

**粵語摘要：** 網址別名三個 alias 而家有專用原生 renderer 同 `Slugify` core；renderer 計 **31/346**，仲有 **315** 條固定 route 同五組家族。Debug／Release 0 errors、core 各 **777/777**（Slugify **18/18**）、catalog parity 同三個 alias UIA **12/12** 已通過。今次冇可呼叫 LowLevel MCP；driver 因 `CopyFromScreen` 唔可用而拒絕空白 fallback，冇 PNG／冇 worktree process／冇改 canonical 截圖，所以 visual 係 `capture-blocked`。呢個分支只改功能，唔改 workflow／release／GitHub／`main`，要由 release owner 受控整合。

## 2026-07-19 native reference-text wave — focused routes green; visual capture blocked · 2026-07-19 原生參考文字批次 — 專項 route 通過；視覺擷取受阻

- **Scope / 範圍：** dedicated C++/WinRT renderers for Phonetic Speller, Box & Banner Text, and HTML Entities, backed by the shared standard-C++ `ReferenceText` core. The wave preserves three phonetic alphabets, digits, uppercase/punctuation options, managed UTF-16 code-unit rows and spoken output; eight box/comment styles, three alignments, bounded padding, optional titles, multiline/tab-aware Unicode layout; and named/numeric entity encode/decode behavior, invalid UTF-16 safety, managed-compatible scanning and a local 50-row bilingual reference. All processing is local; clipboard writes require explicit Copy.
- **Inventory / 清單：** `HasNativeRenderer` and the provisional ledger now account for **30/346 fixed routes**, leaving **316 fixed routes** plus five dynamic route families. The ledger is exactly **30 `in-progress` / 316 `not-started`**; no pending or uninspected page is called complete.
- **Evidence green / 已通過證據：** fresh native Debug and Release x64 solution builds each exit 0 with 0 errors; Debug and Release core each pass **759/759**; catalog parity passes all 346 fixed routes plus five dynamic families; focused `-ReferenceTextRoutesOnly` UI Automation passes **29/29** across all eight registered aliases, including English, Cantonese, and bilingual HTML-reference rows; and the full native shell passes **417/417 (417 passed, 0 failed)**.
- **Visual disposition / 視覺處置：** LowLevel Computer Use MCP is not available in this Codex session. The repository driver attempted `phonetic`, `boxtext`, and `htmlentities`; after final localization a fresh `htmlentities` attempt again found `CopyFromScreen` unavailable and rejected a blank/near-uniform `PrintWindow` client. No PNG was created, no canonical screenshot changed, and cleanup left no WinForge process. The three ledger visual dimensions are `capture-blocked`, and the rows remain `in-progress`.
- **Integration boundary / 整合界線：** the handoff is feature-only and changes no workflow or release policy; controlled integration preserves that boundary while the native release gate supplies remote verification.

**粵語摘要：** 拼讀字母表、文字方框／橫幅同 HTML 實體已有專用原生 renderer 同共用 `ReferenceText` core；計數係 **30/346**，仲有 **316** 條固定 route 同五組動態家族。最新 Debug／Release build 各 0 errors、兩個 core 各 **759/759**、catalog parity、八個 alias UIA **29/29** 同完整 native shell **417/417（417 passed、0 failed）** 已通過。今次 session 冇 LowLevel MCP；整合後最新 HTML driver 重試拒絕空白 fallback，冇建立或保留 PNG、冇殘留 process、冇改 canonical 截圖，所以 visual 係 `capture-blocked`。handoff 只改功能，唔改 workflow／release policy；受控整合會守住界線，再由 native release gate 做遙距驗證。

## Historical native text-analysis collation wave — exhaustive shell green; visual capture blocked · 歷史原生文字分析排序批次 — 完整 shell 通過；視覺擷取受阻

- **Scope / 範圍：** dedicated C++/WinRT renderers for Text Statistics, Word Frequency, and String Compare, backed by the shared standard-C++ `TextAnalysis` core. Text Statistics preserves local UTF-16/CJK word/sentence/paragraph/average/time/Flesch/top-ten behavior. Word Frequency preserves word/bigram/Unicode-scalar modes, options and stop words, current-culture ordering, count/bar/percentage rows, and exact CSV. String Compare preserves normalization, UTF-16 metrics, OSA/Jaro–Winkler/longest-common-substring/subsequence results, the greater-than-2,000 guard, exact greedy Jaro optimized subquadratically, and route-exit cleanup. Clipboard mutation is explicit-only for Word Frequency and String Compare; Text Statistics has no side effect. Commit `c6f8a24d52e4949596efd58e070e0601a2939511` additionally aligns native `CurrentCultureIgnoreCase` with .NET width/kana tailoring, stable canonical-equivalent order, and a shared initialized collator; it removes comparator serialization and per-edit polite live announcements while keeping the named total queryable and attributing the source in `THIRD-PARTY-NOTICES.txt`.
- **Inventory / 清單：** `HasNativeRenderer` now accounts for **27/346 fixed routes**, leaving **319 fixed routes** plus five dynamic route families. The provisional ledger is exactly **27 `in-progress` / 319 `not-started`**; `capture-blocked` routes are not called complete.
- **Evidence green / 已通過證據：** native Debug and Release core executables each pass **717/717**, including focused Text Analysis **87/87**; focused `-TextAnalysisRoutesOnly` UI Automation passes **40/40** across all seven registered aliases; the post-c6 exhaustive native shell passes **388/388 (388 passed, 0 failed)**; and catalog parity covers 346 fixed routes plus five dynamic families.
- **Headless capture disposition / 無頭擷取處置：** Historical LowLevel MCP 1.28.1 attempts resolved the three native windows but produced only inspected pure-white clients; all invalid images, processes, and desktops were cleaned. For the fresh post-c6 `wordfreq` retry, the repo-local LowLevel checkout was present but its tools were not callable in the active Codex session, so no LowLevel evidence is claimed. The required repository driver reached the fallback and failed with: `CopyFromScreen is unavailable and the PrintWindow fallback produced a blank or near-uniform WinUI client frame; graphics capture is unavailable in this desktop session.` No PNG was retained, created, reused, or substituted. All three visual rows remain conclusively `capture-blocked`.
- **Current CI and release proof / 目前 CI 及版本證明：** `1732af0b458454f144d1ac32be222b9e9015e5c5` passed [run 29694567950](https://github.com/codingmachineedge/WinForge/actions/runs/29694567950), proving the staged site-data no-op gate and publishing native-only prerelease `native-v1.0.55`. Integration merge `03b1e66f21b07c28fe2bd0b30ef60fe4af134e59` passed [main run 29695569334](https://github.com/codingmachineedge/WinForge/actions/runs/29695569334) and published stable, non-draft [native-v1.0.57](https://github.com/codingmachineedge/WinForge/releases/tag/native-v1.0.57) at that exact SHA. `/releases/latest` resolves to the same release with exactly `WinForge-Native-Setup.exe` and `WinForge-native-x64-1.0.57.zip`, never a managed app. Site-data [run 29695569335](https://github.com/codingmachineedge/WinForge/actions/runs/29695569335) staged before diffing and logged `No change to design/winforge-data.js.` successfully. `.github/workflows/native-release.yml` remains the only publisher.

**粵語摘要：** 文字分析 renderer 計 **27/346**，仲有 **319** 條固定 route 同五組動態家族，ledger 保持 `27 in-progress / 319 not-started`。Debug／Release core 各 **717/717**，Text Analysis **87/87**，專項 UIA **40/40**，完整 shell **388/388**；visual 仍係 `capture-blocked`，因為本機 LowLevel checkout 冇喺呢個 session 暴露工具，而 driver fallback 空白，冇保留 PNG。`1732af0b`／run 29694567950 證明 site-data 正規化 no-op gate；main merge `03b1e66f`／run 29695569334 發佈 stable `native-v1.0.57`，`/releases/latest` 準確只有 native setup 同 native ZIP，冇 managed app。

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
