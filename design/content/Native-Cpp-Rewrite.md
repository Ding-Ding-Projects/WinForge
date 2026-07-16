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

## Native utility slices · 原生 utility 批次

Check Digit, Text to Binary, Case Converter, GUID Generator, Base32/Base58/Ascii85, and Roman Numerals have real native C++ cores and C++/WinRT pages. They retain state across English/Cantonese/Bilingual rerenders, use explicit clipboard actions, and expose stable accessibility IDs. Their individual ledger rows remain in progress until all evidence—including visual evidence—is available. · Check Digit、Text to Binary、Case Converter、GUID Generator、Base32／Base58／Ascii85 同 Roman Numerals 已有真正原生 C++ core 同 C++/WinRT 頁面。佢哋會喺英文／粵語／雙語重繪時保留狀態、用明確 clipboard 動作，同有穩定無障礙 ID。所有證據（包括視覺證據）齊全前，各自 ledger 項仍然係進行中。

## Verification · 驗證

```powershell
tests\native\WinForge.Core.Tests\bin\x64\Debug\WinForge.Core.Tests.exe
powershell -NoProfile -ExecutionPolicy Bypass -File eng\native\Test-NativeCatalogParity.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File eng\native\Invoke-NativeShellSmoke.ps1
```

- Debug and Release x64 native builds: **0 errors**. · Debug 同 Release x64 原生建置：**0 errors**。
- Native tests: **355/355** in Debug and Release, including 29 Package Mutation Coordinator cases. · 原生測試：Debug 同 Release 都係 **355/355**，包括 29 個 Package Mutation Coordinator 案例。
- Catalog parity: 346 fixed routes, five dynamic families, 319 registry records, and 22 categories. · 目錄對等：346 條固定路線、五組動態家族、319 條 registry 記錄同 22 個分類。
- Elevated process-owned UI Automation smoke: **148/148**. It verifies the native UI and fail-closed behavior, not normal-integrity external package activity. · 提權、自有 process UI Automation smoke：**148/148**。佢驗證原生 UI 同 fail-closed 行為，唔係正常 integrity 外部套件活動。

## Visual evidence · 視覺證據

Every changed native Package Manager view is currently `capture-blocked`. The required repository driver and a persistent LowLevel HTTP MCP headless desktop both launched Discover, Updates, and Installed, but each inspected HWND PNG had only a title bar plus a blank or near-uniform WinUI client frame. Invalid images were discarded; no stale, synthetic, blank, or managed image is presented as native evidence. `docs/screenshot-packages.png` remains a managed-production reference only. · 每個改過嘅原生 Package Manager view 目前都係 `capture-blocked`。必需 repository driver 同持續 LowLevel HTTP MCP 無頭 desktop 都成功開啟 Discover、Updates 同 Installed，但每張檢查過嘅 HWND PNG 都只得 title bar 同空白／接近單色 WinUI client frame。無效圖片已丟棄；唔會將舊、合成、空白或者受控圖片當成原生證據。`docs/screenshot-packages.png` 仍然只係受控正式版參考。

See the [full native rewrite record](../docs/Native-Cpp-Rewrite.md) and [machine-readable parity ledger](../docs/cpp-port-parity.json). · 詳情請睇[完整原生重寫記錄](../docs/Native-Cpp-Rewrite.md)同[機器可讀對等清單](../docs/cpp-port-parity.json)。
