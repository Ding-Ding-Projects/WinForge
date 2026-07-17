# Native C++ Rewrite · 原生 C++ 重寫

> Status: **in progress**. WinForge is being rebuilt as a real C++20/C++/WinRT WinUI 3 application. The managed app remains the behavioral oracle until every ledger row is proven; the native executable does not host, wrap, launch, or communicate with it. · 狀態：**進行中**。WinForge 正在重建成真正嘅 C++20／C++/WinRT WinUI 3 app。每一條 ledger 證據完成前，受控 app 只係行為基準；原生 executable 唔會 host、包裝、開啟或者同佢通訊。

## Coverage contract · 覆蓋合約

| Surface · 介面 | Count · 數量 |
|---|---:|
| Registry records · Registry 記錄 | 319 |
| Fixed routes · 固定路線 | 346 |
| Dynamic route families · 動態路線組 | 5 |
| Deep-link aliases · 深層連結別名 | 805 |

The native shell provides route parsing, bilingual localization, dashboard, all-app discovery, settings entry, deep-linking, and honest pending surfaces for unported routes. A route that resolves to a pending page is not treated as ported. · 原生 shell 提供路線解析、雙語本地化、dashboard、所有 app 探索、settings 入口、深層連結，同未移植路線嘅如實 pending 畫面。開到 pending 頁唔等於已移植。

## Current native Package Manager slice · 目前原生套件管理批次

The native Package Manager is **in progress**, not a full UniGetUI clone. Its nine views and 11 manager filters render in C++/WinRT. Discover, Updates, and Installed use bounded non-mutating queries; cached Discover filtering is local and never starts another package query. A v3 Bundle workspace is metadata-only and never constructs an argv plan.

原生 Package Manager 係**進行中**，唔係完整 UniGetUI 複製。佢嘅九個 view 同 11 個 manager filter 都喺 C++/WinRT 顯示。Discover、Updates 同 Installed 只做有界非修改查詢；Discover 已快取篩選只喺本機做，絕對唔會開另一個套件查詢。v3 Bundle 工作區只存 metadata，絕對唔會建立 argv 計劃。

One cached Install, Update, or Uninstall row can be reviewed into `AwaitingConsent` and requires a separate confirmation for serial normal-integrity execution. The coordinator keeps at most 50 in-memory records, has a five-minute deadline, supports cancellation and fresh-consent retry, and cancels pending/running work when the page is left or closed. Elevation, hooks, unsafe IDs, custom mutation arguments, and overlong reviewed previews fail closed. It retains only a redacted reviewed argv plus request/lifecycle metadata in memory and writes one bounded redacted lifecycle event to Operations; third-party stdout, stderr, and runtime diagnostics are withheld. Multi-select and Update all remain preview-only.

一條已快取嘅 Install、Update 或 Uninstall 資料列可以先檢視成 `AwaitingConsent`，而且要另一個確認先可以正常 integrity 串行執行。協調器最多保留 50 條記憶體記錄、有五分鐘限期、支援取消同重新確認重試，離開／關閉頁面會取消等待／執行中工作。提升權限、hooks、唔安全 ID、自訂修改參數同過長已檢視預覽都 fail closed。佢只喺記憶體保留已遮蔽嘅已檢視 argv 同要求／生命週期 metadata，並向 Operations 寫入一條有界已遮蔽生命週期事件；第三方 stdout、stderr 同執行時診斷會略去。多選同 Update all 仍然只供預覽。

This is deliberately incomplete: batch consent, elevation brokering, durable workflow recovery, rich manager-specific details, Setup/bootstrap, broader settings, scheduler/notifications, .NET update resolution, PyPI hydration, and normal-integrity live external-query/mutation evidence remain pending. The pinned UniGetUI source snapshot is provenance and a behavior inventory only; it is excluded from runtime and is not parity evidence. · 呢個有意保留未完成：批次確認、提升權限 broker、持久工作流程復原、豐富逐 manager 詳情、Setup／bootstrap、更廣泛設定、排程／通知、.NET 更新解析、PyPI hydration 同正常 integrity 即時外部查詢／修改證據仍然待辦。固定嘅 UniGetUI 原始碼 snapshot 只係來源同功能清單；已排除 runtime，亦唔係對等證據。

## Native safe regex search and builder · 原生安全正規搜尋同建立器

`RegexSearchSurface.h` registers all current native regex-search controls: Shell catalog, All Apps, local cached Package Discover filtering, and the static local Regex Cheatsheet. Every surface defaults to literal filtering and enters bounded non-JIT PCRE2-16 only through its explicit Regex mode, with UTF/UCP, pattern/input/nesting/code-size/match/depth/heap limits, and a 10 ms callout. The full four-step `module.regextester` builder adds escaped literal recipes, route/package/version starters, safe tokens, groups/alternation/word-boundary/lookaround assertions/quantifiers, capture preview, and valid-only Apply to Shell, All Apps, cached Discover, Regex Cheatsheet, or tester-only. This is not a complete .NET regex/replacement parity claim. · `RegexSearchSurface.h` 登記咗而家全部原生 regex-search 控制：Shell 目錄、所有 app、本機已快取 Package Discover 篩選同靜態本機 Regex Cheatsheet。每個 surface 預設係 literal 篩選，只有用佢明確嘅 Regex mode 先會進入有界、非 JIT PCRE2-16，帶 UTF/UCP、pattern／input／nesting／code-size／match／depth／heap 限制同 10 ms callout。完整四步 `module.regextester` 建立器加入已 escape literal recipe、路線／套件／版本起始式、安全 token、group／alternation／word-boundary／lookaround assertion／quantifier、capture 預覽，同只會套用有效模式去 Shell、所有 app、已快取 Discover、Regex Cheatsheet 或只限測試器。呢個唔係完整 .NET regex／replacement 對等聲稱。

## Native Regex Cheatsheet · 原生 Regex 速查表

`module.regexcheat` is a live C++/WinRT page backed by a pure-C++ bilingual reference catalog: **67** rows in **nine** categories and **eight** explicit Copy-only ready-made patterns. It keeps .NET-only syntax as inert documentation, never executes it as code, starts no process, makes no network request, and does not alter package argv. Invalid bounded-PCRE2 filtering keeps the previous visible results; the full builder can return a verified pattern to the Cheatsheet target. Debug/Release native tests pass **395/395** (six focused Cheatsheet checks), catalog parity covers 346 fixed routes plus five dynamic families, and isolated LowLevel MCP UI Automation passes **224/224** with catalog/filter/Copy/builder/accessibility/clipping coverage. The inspected 852×880 full and 836×841 client frames were blank and discarded, so this visual record is `capture-blocked`. · `module.regexcheat` 係即時 C++/WinRT 頁面，背後係純 C++ 雙語參考目錄：**67** 項、**九** 個分類，同 **八** 個只可以明確 Copy 嘅現成模式。佢將 .NET 專用語法保留做惰性文件，唔會將佢當 code 執行、唔會啟動 process、發 network request 或改 package argv。無效有界 PCRE2 篩選會保留舊結果；完整 builder 可以將已驗證模式交返 Cheatsheet target。Debug／Release 原生測試係 **395/395**（六個 Cheatsheet 專項檢查），catalog parity 涵蓋 346 條固定 routes 同五組 dynamic families，而隔離 LowLevel MCP UI Automation 係 **224/224**，覆蓋目錄／篩選／Copy／builder／accessibility／clipping。檢查過嘅 852×880 full 同 836×841 client frame 都係空白並已丟棄，所以 visual 紀錄係 `capture-blocked`。

Package Discover regex is cache-only: it never enters argv or HTTPS, cannot begin a package query, disables remote Search, and has a `NativePackageQueryAudit` epoch that changes only when an actual CLI/HTTPS query begins. Builder Apply validates first, forces Discover, and retains only a completed Discover-search cache rather than stale Updates/Installed rows. PCRE2 attribution ships in `THIRD-PARTY-NOTICES.txt`, the portable ZIP, and the native installer. · Package Discover regex 只限快取：唔會入 argv 或 HTTPS、唔可以開始套件查詢、會停用遠端 Search，而且 `NativePackageQueryAudit` epoch 只會喺真正 CLI／HTTPS 查詢開始時先改。builder Apply 會先驗證、強制 Discover，而且只保留已完成 Discover 搜尋快取，而唔會保留舊 Updates／Installed 資料列。PCRE2 歸屬會放喺 `THIRD-PARTY-NOTICES.txt`、可攜 ZIP 同原生 installer。

## Native utility slices · 原生 utility 批次

Check Digit, Text to Binary, Case Converter, GUID Generator, Base32/Base58/Ascii85, and Roman Numerals have real native C++ cores and C++/WinRT pages. They retain state across English/Cantonese/Bilingual rerenders, use explicit clipboard actions, and expose stable accessibility IDs. Their individual ledger rows remain in progress until all evidence—including visual evidence—is available. · Check Digit、Text to Binary、Case Converter、GUID Generator、Base32／Base58／Ascii85 同 Roman Numerals 已有真正原生 C++ core 同 C++/WinRT 頁面。佢哋會喺英文／粵語／雙語重繪時保留狀態、用明確 clipboard 動作，同有穩定無障礙 ID。所有證據（包括視覺證據）齊全前，各自 ledger 項仍然係進行中。

## Native Password Strength · 原生密碼強度

`module.passwordstrength` is a live native C++ local-only analyzer. It uses the managed ASCII-pool entropy contract, local common-password/repeat/sequence checks, rating and crack-time bands, plus a masked-default in-memory reveal editor. It never persists, logs, sends, or copies the value, clears it on navigation, and guards delayed inactive-editor updates so a reveal toggle cannot erase the analysis model. The Debug/Release core suites include 11 focused checks; the 224/224 headless native shell smoke covers masking, reveal state, common-password warnings, aliases, language retention, accessibility, and clipping. Its row remains `in-progress` only because the inspected LowLevel 852×880 capture had a blank client surface and was discarded. · `module.passwordstrength` 係即時原生 C++、只限本機嘅分析器。佢用受控版 ASCII 字元池熵值合約、本機常見密碼／重複／序列檢查、評級同破解時間範圍，加上預設遮蔽嘅只限記憶體顯示輸入。佢絕對唔會持久化、記錄、傳送或者複製值，導覽時會清除，亦會保護非活動輸入框嘅延遲更新，避免顯示切換抹走分析模型。Debug／Release core 包括 11 個專項檢查；224/224 無頭原生 shell smoke 覆蓋遮蔽、顯示狀態、常見密碼警告、alias、語言保留、無障礙同裁切。呢項只因檢查過嘅 LowLevel 852×880 擷取有空白 client surface、已經丟棄而保持 `in-progress`。

## Verification · 驗證

```powershell
tests\native\WinForge.Core.Tests\bin\x64\Debug\WinForge.Core.Tests.exe
powershell -NoProfile -ExecutionPolicy Bypass -File eng\native\Test-NativeCatalogParity.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File eng\native\Invoke-NativeShellSmoke.ps1
```

- Debug and Release x64 native builds: **0 errors**. · Debug 同 Release x64 原生建置：**0 errors**。
- Native tests: **395/395** in Debug and Release, including the native regex-surface contract, **six** focused Regex Cheatsheet checks, Password Generator, and **11** focused Password Strength checks. · 原生測試：Debug 同 Release 都係 **395/395**，包括原生 regex 搜尋位置合約、**六** 個 Regex Cheatsheet 專項檢查、Password Generator 同 **11** 個 Password Strength 專項檢查。
- Catalog parity: 346 fixed routes, five dynamic families, 319 registry records, and 22 categories. · 目錄對等：346 條固定路線、五組動態家族、319 條 registry 記錄同 22 個分類。
- Headless process-owned UI Automation smoke: **224/224**. It verifies the native UI, recipe/assertion builder controls, invalid-Apply blocking and prior-result retention, deterministic Package Discover routing, Regex Cheatsheet catalog/filter/Copy/builder behavior, Password Strength masking/reveal/local-warning behavior, clipping, and fail-closed behavior—not normal-integrity external package activity. · 無頭、自有 process UI Automation smoke：**224/224**。佢驗證原生 UI、recipe／assertion 建立器控制、無效 Apply 阻擋同舊結果保留、確定性 Package Discover 導覽、Regex Cheatsheet 目錄／篩選／Copy／builder 行為、Password Strength 遮蔽／顯示／本機警告行為、裁切同 fail-closed 行為，唔係正常 integrity 外部套件活動。

## Native Package Setup review · 原生 Package Setup 檢視

Native `package-setup` now has an intentionally constrained review surface: eleven manager rows, manual-only Winget/Scoop/PowerShell Gallery/vcpkg, seven immutable Winget engine-bootstrap IDs, and fourteen immutable curated dependency IDs. Review is inert and exposes the redacted validated argv before a separate Operations confirmation can queue normal-integrity serial work. Scripts, arbitrary commands, custom arguments, hooks, unsafe/local sources, and elevation are rejected. Debug/Release pass **389/389** and isolated LowLevel MCP UI Automation passes **216/216**, including accessibility, no-process-start, consent, provenance, and clipping checks.

原生 `package-setup` 而家有刻意收窄嘅檢視介面：11 個 manager 行、manual-only Winget／Scoop／PowerShell Gallery／vcpkg、七個固定 Winget engine-bootstrap ID，同 14 個固定 curated dependency ID。檢視係 inert，會先顯示已遮蔽、已驗證 argv；之後要喺 Operations 獨立確認，先可以喺 normal integrity 串行處理。script、任意 command、自訂參數、hooks、唔安全／local source 同 elevation 都會拒絕。Debug／Release 通過 **389/389**，隔離 LowLevel MCP UI Automation 通過 **216/216**，包括 accessibility、唔會啟動 process、同意、provenance 同裁剪檢查。

## Visual evidence · 視覺證據

Every changed native Package Manager view is currently `capture-blocked`. The required repository driver and a persistent LowLevel HTTP MCP headless desktop both launched Discover, Updates, and Installed, but each inspected HWND PNG had only a title bar plus a blank or near-uniform WinUI client frame. Invalid images were discarded; no stale, synthetic, blank, or managed image is presented as native evidence. `docs/screenshot-packages.png` remains a managed-production reference only. · 每個改過嘅原生 Package Manager view 目前都係 `capture-blocked`。必需 repository driver 同持續 LowLevel HTTP MCP 無頭 desktop 都成功開啟 Discover、Updates 同 Installed，但每張檢查過嘅 HWND PNG 都只得 title bar 同空白／接近單色 WinUI client frame。無效圖片已丟棄；唔會將舊、合成、空白或者受控圖片當成原生證據。`docs/screenshot-packages.png` 仍然只係受控正式版參考。

Password Strength was also launched through the persistent LowLevel MCP headless desktop after the 224/224 shell gate. The 852×880 window capture reported `rendered_ok`, but inspection found a blank client surface beneath its title bar. The invalid PNG was discarded; the page is `capture-blocked`, not visual-pass. Regex Cheatsheet was captured the same way after its 224/224 gate: the 852×880 full frame and 836×841 client frame were blank, discarded, and remain `capture-blocked`. · Password Strength 亦喺 224/224 shell 閘門後經持續 LowLevel MCP 無頭 desktop 開啟。852×880 視窗擷取回報 `rendered_ok`，但檢查發現 title bar 下面嘅 client surface 空白。無效 PNG 已丟棄；頁面係 `capture-blocked`，唔係 visual-pass。Regex Cheatsheet 亦喺佢嘅 224/224 閘門後以同一方法擷取：852×880 full frame 同 836×841 client frame 都係空白、已丟棄，仍然係 `capture-blocked`。

See the [full native rewrite record](../docs/Native-Cpp-Rewrite.md) and [machine-readable parity ledger](../docs/cpp-port-parity.json). · 詳情請睇[完整原生重寫記錄](../docs/Native-Cpp-Rewrite.md)同[機器可讀對等清單](../docs/cpp-port-parity.json)。
