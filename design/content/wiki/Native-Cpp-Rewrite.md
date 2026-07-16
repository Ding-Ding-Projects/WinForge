# Native C++ Rewrite · 原生 C++ 重寫

WinForge is migrating to a genuine C++20/C++/WinRT WinUI 3 application. C# and XAML are behavioral-oracle source only during migration; the native executable does not host the CLR, wrap the managed app, use C++/CLI, or outsource behavior over IPC. · WinForge 正喺遷移去真正嘅 C++20/C++/WinRT WinUI 3 app。遷移期間 C# 同 XAML 只係行為基準來源；原生 executable 唔會 host CLR、包住受控 app、用 C++/CLI，或者用 IPC 外判行為。

## Current evidence · 目前證據

| Evidence · 證據 | Current result · 目前結果 |
|---|---|
| Native route inventory · 原生 route 清單 | 346 fixed routes, five dynamic families, 319 registry entries, 22 categories · 346 條固定路線、五組動態家族、319 條 registry 記錄、22 個分類 |
| Debug / Release core tests · Debug／Release 核心測試 | **374/374** each (328 core + 46 parser) · 各自 **374/374**（328 個 core + 46 個 parser） |
| Native shell accessibility smoke · 原生 shell 無障礙 smoke | **164/164** elevated process-owned UI Automation checks · **164/164** 提權、自有 process UI Automation 檢查 |
| Catalog parity · 目錄對等 | 346 fixed routes, five dynamic families, 319 registry entries, 22 categories, 346 ledger rows · 346 條固定路線、五組動態家族、319 條 registry 記錄、22 個分類、346 條 ledger rows |

Unported routes display an explicit native pending page; a resolved route is never presented as feature parity. · 未移植 route 會顯示明確原生 pending 頁；route 解析到唔會當成功能對等。

## Package Manager · 套件管理

The native Package Manager remains in progress, not a full UniGetUI clone. It provides safe read-only Discover/Updates/Installed queries, Details and Sources probes, update rules, native preferences, and a bounded v3 Bundle metadata workspace across nine views and eleven Windows manager adapters. · 原生 Package Manager 仍然進行中，唔係完整 UniGetUI 複製。佢喺九個 view 同十一個 Windows manager adapter 提供安全只讀 Discover／Updates／Installed 查詢、Details 同 Sources 探測、更新規則、原生偏好同有界 v3 Bundle metadata 工作區。

An individual cached Install/Update/Uninstall row can become an `AwaitingConsent` record with a redacted reviewed argv. A separate Confirm action is needed before serial normal-integrity execution. The 50-record coordinator has a five-minute limit, cancellation before/during execution and on navigation/close, fresh-consent retry, and fail-closed rejection of elevation, hooks, unsafe IDs, custom mutation arguments, and oversized previews. It keeps redacted reviewed argv plus request/lifecycle metadata in memory, writes a bounded redacted lifecycle event to existing Operations history, and withholds third-party stdout/stderr/runtime diagnostics. Multi-select and Update all remain preview-only. · 個別已快取安裝／更新／解除安裝資料列可以成為附有已遮蔽已檢視 argv 嘅 `AwaitingConsent` 記錄。要由另一個 Confirm 動作先可以串行正常 integrity 執行。50 條協調器有五分鐘上限、開始前／執行中及導覽／關閉取消、重新確認重試，並會 fail closed 拒絕提升權限、hooks、唔安全 ID、自訂修改參數同過長預覽。佢喺記憶體保存已遮蔽已檢視 argv 連同要求／生命週期 metadata、向現有 Operations 歷史寫入有界已遮蔽生命週期事件，並略去第三方 stdout／stderr／執行時診斷。多選同全部更新仍然只供預覽。

Normal-integrity external execution proof is still blocked in this elevated session. Batch consent, elevation mediation, full bundle interoperability, Setup/bootstrap, scheduler/notifications, richer manager UX, and wider settings are pending. · 正常 integrity 外部執行證明仍喺呢個提權 session 受阻。批次確認、提升權限調停、完整 Bundle 互通、Setup／bootstrap、排程／通知、豐富管理器 UX 同更廣設定仍待完成。

## Native regex search and builder · 原生正規搜尋同建立器

Native PCRE2-16 now powers Shell catalog search, All Apps, and cached Package Discover filtering. It is bounded and non-JIT. The real four-step `module.regextester` builder handles flags, safe composition, groups/alternation/quantifiers, match/capture preview, and application to a chosen native target. Package regex is cache-only, never enters argv/HTTPS, disables remote Search, and its `NativePackageQueryAudit` changes only for a real package query. PCRE2 attribution ships in `THIRD-PARTY-NOTICES.txt`, the portable ZIP, and the installer. · 原生 PCRE2-16 而家處理 Shell 目錄搜尋、所有 app 同已快取 Package Discover 篩選；佢有界而且唔用 JIT。真正四步 `module.regextester` 建立器處理旗標、安全組合、group／alternation／quantifier、match／capture 預覽同套用去已揀原生目標。Package regex 只限快取、唔會入 argv／HTTPS、會停用遠端 Search，而且 `NativePackageQueryAudit` 只會喺真正套件查詢時改。PCRE2 歸屬會放喺 `THIRD-PARTY-NOTICES.txt`、可攜 ZIP 同 installer。

## Visual and completion status · 視覺同完成狀態

On 2026-07-16 the required driver retried Dashboard, All Apps, Regex Tester, and Package Discover, while repo-local LowLevel MCP captured Regex Tester and Package Discover on an isolated desktop. `CopyFromScreen` was unavailable, and every inspected `PrintWindow`/HWND client frame was blank or near-uniform; no invalid or stale PNG is shown as native evidence, so these views are `capture-blocked`. The 164/164 UI Automation sweep is behavioral/accessibility proof, not a visual pass. · 2026-07-16，必需 driver 重新嘗試 Dashboard、所有 app、Regex Tester 同 Package Discover，而 repo 本機 LowLevel MCP 喺隔離 desktop 擷取 Regex Tester 同 Package Discover。`CopyFromScreen` 用唔到，而且每張檢查過嘅 `PrintWindow`／HWND client frame 都係空白／接近單色；唔會將無效或者舊 PNG 當做原生證據，所以呢啲 view 係 `capture-blocked`。164/164 UI Automation 掃描係行為／無障礙證明，唔係 visual pass。

Full cutover still requires every route, control, service, companion, launcher, updater, installer, protocol, test, documentation mirror, and screenshot to pass with a final native-binary audit. · 完整切換仍然要求每條 route、control、service、companion、launcher、updater、installer、protocol、test、文件鏡像同截圖都通過，並有最後原生 binary audit。
