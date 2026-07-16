# Native C++ Rewrite · 原生 C++ 重寫

WinForge is being migrated to a genuine C++20/C++/WinRT WinUI 3 application. C# and XAML remain only as behavioral-oracle source while the native route ledger is completed; the native executable does not host the CLR, wrap the managed app, use C++/CLI, or outsource behavior over IPC. · WinForge 正喺遷移去真正嘅 C++20/C++/WinRT WinUI 3 app。C# 同 XAML 喺完成原生 route 清單前只係行為基準來源；原生 executable 唔會 host CLR、包住受控 app、用 C++/CLI，或者用 IPC 外判行為。

## Inventory · 清單

| Surface · 介面 | Count · 數量 |
|---|---:|
| Registry entries · Registry 記錄 | 319 |
| Fixed routes · 固定路線 | 346 |
| Dynamic route families · 動態路線組 | 5 |
| Runtime categories · 執行時分類 | 22 |

Unported routes render an explicit native pending page; routing to a page is not claimed as feature parity. · 未移植 route 會顯示明確原生 pending 頁；開到頁面唔代表功能對等。

## Current evidence · 目前證據

- Debug native build: 0 errors; Release native build: 0 errors. · Debug 原生建置：0 errors；Release 原生建置：0 errors。
- Native core tests: **374/374** in Debug and **374/374** in Release (328 core route/package-manager checks plus 46 parser checks). · 原生核心測試：Debug **374/374**、Release **374/374**（328 個 core route／package-manager 檢查加 46 個 parser 檢查）。
- Catalog parity: 346 fixed routes, five dynamic families, 319 registry records, 22 categories, and 346 ledger rows. · 目錄對等：346 條固定路線、五組動態家族、319 條 registry 記錄、22 個分類同 346 條 ledger rows。
- Elevated process-owned UI Automation smoke: **164/164**, covering routing, bilingual accessibility, live controls, native regex search/builder, and Package Manager clipping checks. · 提權、自有 process UI Automation smoke：**164/164**，涵蓋導覽、雙語無障礙、即時控制、原生 regex 搜尋／建立器同 Package Manager 裁切檢查。

This is native core/UI and fail-closed evidence, not normal-integrity external package-manager execution proof. · 呢啲係原生 core／UI 同 fail-closed 證據，唔係正常 integrity 外部 Package Manager 執行證明。

## Package Manager native slice · Package Manager 原生批次

The native Package Manager is in progress, not a full UniGetUI clone. It has nine views, eleven Windows manager adapters, read-only Discover/Updates/Installed queries, Details/Sources probes, local update rules, native preferences, and a bounded metadata-only v3 Bundle workspace. · 原生 Package Manager 仍然進行中，唔係完整 UniGetUI 複製。佢有九個 view、十一個 Windows manager adapter、只讀 Discover／Updates／Installed 查詢、Details／Sources 探測、本機更新規則、原生偏好，同有界只存 metadata 嘅 v3 Bundle 工作區。

One cached Discover, Updates, or Installed row may create a redacted reviewed Install/Update/Uninstall argv plan in `AwaitingConsent`; a separate Confirm action queues it for serial normal-integrity execution. The 50-record in-memory coordinator applies a five-minute limit, supports cancellation before start/while running and on navigation/close, and requires fresh consent to retry. It rejects elevation, hooks, unsafe IDs, custom mutation arguments, and an oversized consent preview. It retains redacted reviewed argv with request/lifecycle metadata in memory and writes a bounded redacted lifecycle event to existing Operations history; third-party stdout, stderr, and runtime diagnostics are withheld. Multi-select and Update all remain preview-only. · 一條已快取 Discover、Updates 或 Installed 資料列可以喺 `AwaitingConsent` 建立已遮蔽已檢視安裝／更新／解除安裝 argv 計劃；要由另一個 Confirm 動作先會排入串行正常 integrity 執行。50 條記憶體協調器有五分鐘上限、支援開始前／執行中及導覽／關閉取消，重試要重新確認。提升權限、hooks、唔安全 ID、自訂修改參數同過長確認預覽都會被拒絕。佢喺記憶體保留已遮蔽已檢視 argv 連同要求／生命週期 metadata，並向現有 Operations 歷史寫入有界已遮蔽生命週期事件；第三方 stdout、stderr 同執行時診斷會略去。多選同全部更新仍然只供預覽。

Normal-integrity external query/mutation proof is blocked in this elevated session, and the runtime fails closed. Batch consent, elevation mediation, scheduler/notification integration, full bundle interoperability, Setup/bootstrap, richer manager-specific UX, and broader settings remain pending. · 正常 integrity 外部查詢／修改證明喺呢個提權 session 受阻，而 runtime 會 fail closed。批次確認、提升權限調停、排程／通知整合、完整 Bundle 互通、Setup／bootstrap、豐富逐管理器 UX 同更廣設定仍待完成。

## Native regex search and builder · 原生正規搜尋同建立器

The native Shell catalog, All Apps, and cached Package Discover filter now use bounded PCRE2-16 (UTF/UCP, no JIT, pattern/input/nesting/code-size/match/depth/heap limits, and a 10 ms callout). `module.regextester` is a four-step native builder for flags, safe tokens, grouping/alternation/quantifiers, and match/capture preview before applying to a selected native target. It is not a claim of full .NET regex/replacement parity. · 原生 Shell 目錄、所有 app 同已快取 Package Discover 篩選而家會用有界 PCRE2-16（UTF/UCP、唔用 JIT、pattern／input／nesting／code-size／match／depth／heap 限制同 10 ms callout）。`module.regextester` 係四步原生建立器，處理旗標、安全 token、grouping／alternation／quantifier，同套用去已揀原生目標之前嘅 match／capture 預覽。呢個唔係完整 .NET regex／replacement 對等聲稱。

Discover regex is local cache only: it never reaches argv or HTTPS, external Search is disabled while it is active, and `NativePackageQueryAudit` changes only for a genuine package query. PCRE2 attribution ships in [`THIRD-PARTY-NOTICES.txt`](../../THIRD-PARTY-NOTICES.txt), the portable ZIP, and the native installer. · Discover regex 只限本機快取：絕對唔會傳去 argv 或 HTTPS，啟用時會停用外部 Search，而且 `NativePackageQueryAudit` 只會喺真正套件查詢時改。PCRE2 歸屬會放喺 [`THIRD-PARTY-NOTICES.txt`](../../THIRD-PARTY-NOTICES.txt)、可攜 ZIP 同原生 installer。

## Visual and accessibility evidence · 視覺同無障礙證據

On 2026-07-16, the repository driver launched Dashboard, All Apps, Regex Tester, and Package Discover with `-WaitMs 16000`; `CopyFromScreen` was unavailable and each `PrintWindow` fallback was rejected as blank or near-uniform. The repo-local LowLevel MCP isolated desktop separately captured full-window and client-only Regex Tester and Package Discover HWNDs; all inspected client frames were blank. No invalid, stale, synthetic, or managed screenshot is presented as native evidence; changed native pages are `capture-blocked`. The 164/164 UI Automation result remains behavioral/accessibility evidence only. · 2026-07-16，repository driver 用 `-WaitMs 16000` 開啟 Dashboard、所有 app、Regex Tester 同 Package Discover；`CopyFromScreen` 用唔到，而且每個 `PrintWindow` fallback 都因空白／接近單色而拒絕。repo 本機 LowLevel MCP 隔離 desktop 另外擷取完整視窗同只限 client 嘅 Regex Tester 及 Package Discover HWND；全部檢查過嘅 client frame 都係空白。冇將無效、舊、合成或者受控截圖當做原生證據；改過原生頁面係 `capture-blocked`。164/164 UI Automation 結果只係行為／無障礙證據。

## Completion gate · 完成閘門

Cutover requires every route, control, service, companion, launcher, updater, installer, protocol, test, documentation mirror, and changed-page screenshot to pass, with a final binary audit proving no CLR, C++/CLI, managed assembly, managed subprocess, or wrapper dependency. · 正式切換要求每條 route、control、service、companion、launcher、updater、installer、protocol、test、文件鏡像同改過頁面截圖都通過，並有最後 binary audit 證明冇 CLR、C++/CLI、受控 assembly、受控 subprocess 或 wrapper 相依。

- [Full migration record · 完整遷移記錄](../Native-Cpp-Rewrite.md)
- [Package Manager · 套件管理](Package-Manager.md)
- [Parity ledger · 對等清單](../cpp-port-parity.json)
