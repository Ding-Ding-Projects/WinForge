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
- Native core tests: **400/400** in Debug and **400/400** in Release (354 core route/package-manager/utility checks plus 46 parser checks), including **42/42** focused Package Mutation Coordinator checks and **11/11** UUID v7 cases. · 原生核心測試：Debug **400/400**、Release **400/400**（354 個 core route／package-manager／utility 檢查加 46 個 parser 檢查），包括 **42/42** 個 Package Mutation Coordinator 專項檢查同 **11/11** 個 UUID v7 案例。
- Catalog parity: 346 fixed routes, five dynamic families, 319 registry records, 22 categories, and 346 ledger rows. · 目錄對等：346 條固定路線、五組動態家族、319 條 registry 記錄、22 個分類同 346 條 ledger rows。
- Elevated process-owned UI Automation smoke: **185/185**, covering routing, bilingual accessibility, live controls, native regex search/builder, UUID v7 generation/decoding/copy/rerender/aliases, Package Manager batch-consent policy, and clipping checks. · 提權、自有 process UI Automation smoke：**185/185**，涵蓋導覽、雙語無障礙、即時控制、原生 regex 搜尋／建立器、UUID v7 產生／解碼／Copy／重建／alias、Package Manager 批次確認政策同裁切檢查。

This is native core/UI and fail-closed evidence, not normal-integrity external package-manager execution proof. · 呢啲係原生 core／UI 同 fail-closed 證據，唔係正常 integrity 外部 Package Manager 執行證明。

## Package Manager native slice · Package Manager 原生批次

The native Package Manager is in progress, not a full UniGetUI clone. It has nine views, eleven Windows manager adapters, read-only Discover/Updates/Installed queries, Details/Sources probes, local update rules, native preferences, and a bounded metadata-only v3 Bundle workspace. · 原生 Package Manager 仍然進行中，唔係完整 UniGetUI 複製。佢有九個 view、十一個 Windows manager adapter、只讀 Discover／Updates／Installed 查詢、Details／Sources 探測、本機更新規則、原生偏好，同有界只存 metadata 嘅 v3 Bundle 工作區。

One cached Discover, Updates, or Installed row may create a redacted reviewed Install/Update/Uninstall argv plan in `AwaitingConsent`; a separate Confirm action queues it for serial normal-integrity execution. Source-aware selected cached rows and the current eligible Review Update all set can now instead submit one all-or-nothing batch of at most 25 fully visible redacted argv plans. Its batch card has one explicit confirmation, serial ordered execution, combined cancellation, and retry that returns only unsuccessful children to fresh consent without replaying successful commands. The 50-record in-memory coordinator rejects elevation, hooks, unsafe IDs, custom mutation arguments, and an oversized consent preview; it retains safe reviewed/lifecycle metadata and withholds third-party stdout, stderr, and runtime diagnostics. · 一條已快取 Discover、Updates 或 Installed 資料列可以喺 `AwaitingConsent` 建立已遮蔽已檢視安裝／更新／解除安裝 argv 計劃；要由另一個 Confirm 動作先會排入串行正常 integrity 執行。識別來源嘅已揀快取資料列同目前合資格嘅檢視全部更新集合而家亦可以提交一個全有或全無、最多 25 條完整可見已遮蔽 argv 計劃嘅批次。批次卡有一次明確確認、按次序串行執行、一齊取消，同只會將未成功子項返回全新確認而唔會重播成功指令。50 條記憶體協調器會拒絕提升權限、hooks、唔安全 ID、自訂修改參數同過長確認預覽；佢會保留安全已檢視／生命週期 metadata，並略去第三方 stdout、stderr 同執行時診斷。

Normal-integrity external query/mutation proof is blocked in this elevated session, and the runtime fails closed. Elevation mediation, scheduler/notification integration, full bundle interoperability, Setup/bootstrap, richer manager-specific UX, and broader settings remain pending. · 正常 integrity 外部查詢／修改證明喺呢個提權 session 受阻，而 runtime 會 fail closed。提升權限調停、排程／通知整合、完整 Bundle 互通、Setup／bootstrap、豐富逐管理器 UX 同更廣設定仍待完成。

## Native regex search and builder · 原生正規搜尋同建立器

`RegexSearchSurface.h` registers all three current native search/filter controls—Shell catalog, All Apps, and cached Package Discover—and each uses bounded PCRE2-16 (UTF/UCP, no JIT, pattern/input/nesting/code-size/match/depth/heap limits, and a 10 ms callout). `module.regextester` is a full four-step native builder for flags; escaped literal, route, package, and semantic-version recipes; safe tokens; grouping/alternation/word-boundary/lookaround assertions/quantifiers; and match/capture preview before applying only a valid pattern to a selected native target. It is not a claim of full .NET regex/replacement parity. · `RegexSearchSurface.h` 登記咗而家全部三個原生搜尋／篩選控制——Shell 目錄、所有 app 同已快取 Package Discover——而且每個都用有界 PCRE2-16（UTF/UCP、唔用 JIT、pattern／input／nesting／code-size／match／depth／heap 限制同 10 ms callout）。`module.regextester` 係完整四步原生建立器，處理旗標、已 escape literal／路線／套件／語義版本 recipe、安全 token、grouping／alternation／word-boundary／lookaround assertion／quantifier，同只會套用有效模式去已揀原生目標之前嘅 match／capture 預覽。呢個唔係完整 .NET regex／replacement 對等聲稱。

Discover regex is local cache only: it never reaches argv or HTTPS, external Search is disabled while it is active, and `NativePackageQueryAudit` changes only for a genuine package query. Applying the builder validates first, forces the Discover view, and retains only a completed Discover cache—never stale Update/Installed rows. PCRE2 attribution ships in [`THIRD-PARTY-NOTICES.txt`](../../THIRD-PARTY-NOTICES.txt), the portable ZIP, and the native installer. · Discover regex 只限本機快取：絕對唔會傳去 argv 或 HTTPS，啟用時會停用外部 Search，而且 `NativePackageQueryAudit` 只會喺真正套件查詢時改。套用 builder 會先驗證、強制 Discover view，而且只保留已完成嘅 Discover 快取——絕對唔會保留舊 Update／Installed 資料列。PCRE2 歸屬會放喺 [`THIRD-PARTY-NOTICES.txt`](../../THIRD-PARTY-NOTICES.txt)、可攜 ZIP 同原生 installer。

## Native UUID v7 · 原生 UUID v7

`module.uuidv7` is now a native C++ RFC 9562 utility reachable through `uuidv7` and the canonical route. It creates 1–1000 canonical v7 values from the 48-bit Unix-millisecond field plus cryptographic randomness; its optional monotonic mode stays ordered through same-millisecond generation, backwards clocks, and `rand_a` overflow. Local decoding accepts canonical, compact, braced, and `urn:uuid:` forms, reports version/variant plus UTC/local time for v7 and best-effort v1, canonicalizes lowercase output, and collapses stale result controls after invalid input. Clipboard writes require explicit Copy, while state survives language rerender. The row is `in-progress` solely because visual capture is blocked. · `module.uuidv7` 而家係原生 C++ RFC 9562 utility，可以用 `uuidv7` 同本體 route 開啟。佢由 48-bit Unix millisecond 欄位加密碼學隨機值產生 1–1000 條標準 v7 值；可選 monotonic 模式會喺同一毫秒產生、時鐘倒退同 `rand_a` overflow 時維持次序。本機解碼接受標準、緊湊、花括號同 `urn:uuid:` 格式，會報告 version／variant 加 v7 同 best-effort v1 嘅 UTC／本機時間、標準化小楷輸出，並喺無效輸入後摺疊舊結果控制。剪貼簿寫入要明確 Copy，而狀態會喺轉語言後保留。清單列只係因視覺擷取受阻先保持 `in-progress`。

## Visual and accessibility evidence · 視覺同無障礙證據

On 2026-07-16, the repository driver launched the changed Dashboard, All Apps, Regex Tester, and Package Discover pages with `-WaitMs 16000`; `CopyFromScreen` was unavailable and each `PrintWindow` fallback was rejected as blank or near-uniform. The repo-local LowLevel MCP isolated desktop separately launched Regex Tester, resolved its 1980×1320 WinUI HWND, and produced an inspected full-window frame with only a title bar and blank client surface. The invalid PNG was discarded. No invalid, stale, synthetic, or managed screenshot is presented as native evidence; changed native pages are `capture-blocked`. The 171/171 UI Automation result remains behavioral/accessibility evidence only. · 2026-07-16，repository driver 用 `-WaitMs 16000` 開啟改過嘅 Dashboard、所有 app、Regex Tester 同 Package Discover 頁；`CopyFromScreen` 用唔到，而且每個 `PrintWindow` fallback 都因空白／接近單色而拒絕。repo 本機 LowLevel MCP 隔離 desktop 亦獨立開啟 Regex Tester、解析佢 1980×1320 嘅 WinUI HWND，並產生一張檢查過嘅完整視窗 frame，只得 title bar 同空白 client surface；無效 PNG 已丟棄。冇將無效、舊、合成或者受控截圖當做原生證據；改過原生頁面係 `capture-blocked`。171/171 UI Automation 結果只係行為／無障礙證據。

On 2026-07-16, the required driver also launched `uuidv7` at `-WaitMs 16000` but rejected its blank/near-uniform `PrintWindow` fallback after `CopyFromScreen` was unavailable. The requested LowLevel MCP isolated desktop then launched UUID v7, resolved its 1980×1320 WinUI HWND, and produced inspected captures: the full window had only its title bar and blank client surface, and the 1958×1264 client-only frame was blank. Both invalid PNGs were discarded; no canonical, wiki-local, stale, synthetic, or managed image was replaced or reused. UUID v7 is `capture-blocked`, and the current **185/185** UI Automation result is behavioral/accessibility evidence only. · 2026-07-16，必需 driver 亦用 `-WaitMs 16000` 開啟 `uuidv7`，但喺 `CopyFromScreen` 用唔到後拒絕咗空白／接近單色 `PrintWindow` fallback。按要求嘅 LowLevel MCP 隔離 desktop 跟住開啟 UUID v7、解析佢 1980×1320 WinUI HWND，並產生檢查過嘅擷取：完整視窗只得 title bar 同空白 client surface，而 1958×1264 只限 client frame 亦係空白。兩張無效 PNG 已丟棄；冇替換或者重用 canonical、wiki 本機、舊、合成或者受控圖片。UUID v7 係 `capture-blocked`，而目前 **185/185** UI Automation 結果只係行為／無障礙證據。

## Completion gate · 完成閘門

Cutover requires every route, control, service, companion, launcher, updater, installer, protocol, test, documentation mirror, and changed-page screenshot to pass, with a final binary audit proving no CLR, C++/CLI, managed assembly, managed subprocess, or wrapper dependency. · 正式切換要求每條 route、control、service、companion、launcher、updater、installer、protocol、test、文件鏡像同改過頁面截圖都通過，並有最後 binary audit 證明冇 CLR、C++/CLI、受控 assembly、受控 subprocess 或 wrapper 相依。

- [Full migration record · 完整遷移記錄](../Native-Cpp-Rewrite.md)
- [Package Manager · 套件管理](Package-Manager.md)
- [Parity ledger · 對等清單](../cpp-port-parity.json)
