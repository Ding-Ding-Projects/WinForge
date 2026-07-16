# Package Manager · 套件管理

![Managed production Package Manager reference · 受控正式版套件管理參考](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-packages.png)

> The displayed image is the shipping managed .NET product, not native visual evidence. On 2026-07-15, the required native driver and a persistent LowLevel MCP HTTP session launched Discover, Updates, Installed, and Operations on isolated desktops. Every inspected HWND capture had a blank or near-uniform WinUI client frame, so no native screenshot is published for this checkpoint. · 呢張展示圖片係發佈中受控 .NET 產品，唔係原生視覺證據。2026-07-15，必需原生 driver 同持續運行嘅 LowLevel MCP HTTP session 喺隔離 desktop 開啟 Discover、Updates、Installed 同 Operations。每張檢查過嘅 HWND 圖都係空白／接近單色 WinUI client frame，所以呢個 checkpoint 冇發佈原生截圖。

## Current native C++ status · 目前原生 C++ 狀態

The shipping managed Package Manager remains production. The native C++20/C++/WinRT implementation is real but incomplete: it exposes nine views and eleven manager adapters, safe read-only Discover/Updates/Installed queries, Details and Sources probes, local update rules, native JSON preferences, and the bounded v3 metadata Bundle workspace. · 發佈中受控 Package Manager 仍然係正式版。原生 C++20/C++/WinRT 實作係真實但未完整：有九個 view、十一個管理器 adapter、安全只讀 Discover／Updates／Installed 查詢、Details 同 Sources 探測、本機更新規則、原生 JSON 偏好同有界 v3 metadata Bundle 工作區。

One cached Discover, Updates, or Installed row can create a redacted reviewed Install/Update/Uninstall argv plan in AwaitingConsent. A separate Confirm action is required before its exact validated argv enters the serial normal-integrity coordinator. The coordinator has a 50-record in-memory cap, a five-minute runtime limit, cancellation before start and while running, navigation/close cancellation, and fresh-consent retry. It rejects elevation, hooks, unsafe IDs, custom mutation arguments, and an oversized consent preview rather than truncating it. · 一條已快取 Discover、Updates 或 Installed 資料列可以喺 AwaitingConsent 建立已遮蔽嘅已檢視安裝／更新／解除安裝 argv 計劃。要由另一個 Confirm 動作先可以將準確已驗證 argv 放入串行正常 integrity 協調器。協調器有 50 條記憶體上限、五分鐘 runtime 上限、開始前同執行中取消、導覽／關閉取消，同重新確認重試。提升權限、hooks、唔安全 ID、自訂修改參數同過長確認預覽都會被拒絕，而唔會截斷預覽。

The coordinator retains the redacted reviewed argv plus request/lifecycle metadata in memory. Existing Operations history receives one bounded redacted lifecycle event; third-party stdout, stderr, and runtime diagnostics are withheld. Multi-select batches, Preview selected, and Update all remain preview-only and never call the mutation coordinator. · 協調器會喺記憶體保留已遮蔽已檢視 argv 連同要求／生命週期 metadata。現有 Operations 歷史會收到一條有界已遮蔽生命週期事件；第三方 stdout、stderr 同執行時診斷會略去。多選批次、預覽所選同全部更新都保持只供預覽，絕對唔會呼叫修改協調器。

## Safety and evidence · 安全同證據

- Debug and Release native tests: **355/355** each (309 core route/package-manager checks + 46 parser checks), including 29 focused coordinator cases. · Debug 同 Release 原生測試：各自 **355/355**（309 個 core route／package-manager 檢查 + 46 個 parser 檢查），包括 29 個協調器專項案例。
- Elevated process-owned UI Automation smoke: **148/148**, including all eleven manager filters and Package Manager horizontal-bound checks. · 提權、自有 process UI Automation smoke：**148/148**，包括全部十一個管理器篩選同 Package Manager 水平邊界檢查。
- Catalog parity: 346 fixed routes, five dynamic families, 319 registry records, and 22 categories. · 目錄對等：346 條固定路線、五組動態家族、319 條 registry 記錄同 22 個分類。
- Normal-integrity external query/mutation proof remains blocked because this session is elevated; the runtime fails closed. · 正常 integrity 外部查詢／修改證明仍受阻，因為呢個 session 已提權；runtime 會 fail closed。
- Visual evidence is capture-blocked, not a visual pass. The managed image above remains clearly labelled as managed only. · 視覺證據係 capture-blocked，唔係 visual pass。上面受控圖片已清楚標示只屬受控版。

## Remaining UniGetUI-informed gaps · 尚欠 UniGetUI 參考缺口

Batch consent, elevation mediation, normal-integrity live proof, full bundle interoperability, Setup/bootstrap, scheduler and notifications, broad per-manager settings, backup/restore, rich manager-specific Details/actions, downloads/source tracking, and authenticated API/CLI/headless/widget surfaces remain pending. The vendored `ThirdParty/UniGetUI` source is an MIT-licensed behavior/provenance reference only and is excluded from the native runtime. · 批次確認、提升權限調停、正常 integrity 即時證明、完整 Bundle 互通、Setup／bootstrap、排程同通知、廣泛逐管理器設定、備份／還原、豐富逐管理器 Details／動作、下載／來源追蹤同已驗證 API／CLI／headless／widget 介面仍待完成。vendor 嘅 `ThirdParty/UniGetUI` 原始碼只係 MIT 授權行為／來源參考，並已排除喺原生 runtime 之外。

## Deep links · 深層連結

Use `package-*` or `packages-*` with `discover`, `updates`, `installed`, `bundles`, `sources`, `ignored`, `setup`, `settings`, or `operations`; routing availability does not by itself claim feature parity. · 可以用 `package-*` 或 `packages-*` 配合 `discover`、`updates`、`installed`、`bundles`、`sources`、`ignored`、`setup`、`settings` 或 `operations`；有 route 本身唔代表功能對等。
