# Native C++ Rewrite · 原生 C++ 重寫

> Foundation milestone, not full parity · 呢個係基礎里程碑，唔係全功能對等

WinForge now has a runnable, self-contained C++20/C++/WinRT WinUI 3 shell beside the shipping managed application. The managed app remains authoritative until every native parity row passes; pending native routes are clearly labelled and never counted as completed features.

WinForge 而家喺發佈中受控 app 旁邊有一個可以執行、自包含嘅 C++20／C++/WinRT WinUI 3 shell。每項原生對等清單通過之前，受控 app 仍然係權威版本；未完成原生路線會清楚標示，絕對唔會當成已完成功能。

## Inventory · 清單

- **346** fixed routes · **346** 條固定路線
- **5** dynamic route families · **5** 組動態路線
- **805** deep-link aliases · **805** 個深層連結別名
- 319 registry records and 22 runtime categories · 319 registry 記錄同 22 個執行時分類

The earlier 323-route closeout covered a legacy subset; it omitted the 22 runtime categories and Settings. Historical batch numbers remain historical evidence, not the current whole-app total.

之前 323-route 結案只覆蓋舊子集，漏咗 22 個執行時分類同 Settings。歷史 batch 數字會保留做歷史證據，唔係而家全 app 總數。

## Current native surface · 目前原生介面

The native shell implements bilingual routing/localization, Dashboard, About, Settings entry, All Apps discovery and live filtering, search, language switching, and all fixed/dynamic route forms. Unported module routes show an honest pending page.

原生 shell 已實作雙語 routing／本地化、Dashboard、About、Settings 入口、所有 app 搜尋同即時篩選、搜尋、轉語言，同全部固定／動態路線格式。未移植模組路線會顯示如實 pending 頁。

Package Manager is now an honest **in-progress** native C++ port, not a pending shell and not parity. It exposes nine views (Discover, Updates, Installed, Bundles, Sources, Ignored, Setup, Settings, and Operations), filters for 11 Windows managers, and read-only result queries for Discover, Updates, and Installed. Sources is only a read-only command probe: raw configuration and diagnostics are deliberately withheld until manager-specific secret redaction is proven. Its runtime hardens argument-vector handling, executable/`PATH` resolution, HTTP and captured-output bounds, process-token verification, explicit state, fail-closed integrity checks, and accessibility/UI Automation contracts.

套件管理而家係如實標示為**進行中**嘅原生 C++ 移植，唔再只係 pending shell，亦未達到對等。佢有九個 view（Discover、Updates、Installed、Bundles、Sources、Ignored、Setup、Settings 同 Operations）、11 個 Windows manager filter，同 Discover、Updates、Installed 嘅原生只讀結果查詢。Sources 只係只讀指令探測：逐管理器機密遮罩未證實之前會刻意隱藏原始設定同診斷。runtime 已加固 argument vector、executable／`PATH` 解析、HTTP 同擷取輸出上限、process token 驗證、明確狀態、fail-closed integrity 檢查，同 accessibility／UI Automation contract。

Mutation, Bundles, Ignored, Setup, Settings, the operation queue, .NET update resolution, and PyPI metadata hydration remain incomplete. The complete 1,002-file UniGetUI `main` snapshot at `21116375c8299d1db38a3c3b4c2eb7e18bc97c4e` (2026-07-10, MIT) is exact provenance and a behavior reference only; it is excluded from runtime and is not parity evidence.

套件變更、Bundles、Ignored、Setup、Settings、operation queue、.NET 更新解析同 PyPI metadata hydration 仍然未完成。完整 1,002 檔 UniGetUI `main` snapshot 固定喺 `21116375c8299d1db38a3c3b4c2eb7e18bc97c4e`（2026-07-10，MIT），只係精確來源證明同功能參考；runtime 會排除佢，亦唔係對等證據。

Check Digit Validator is the next real native utility slice. `checkdigit`, `luhn`, and `module.checkdigit` now open a live standard-C++ implementation of Luhn/card-brand detection, ISBN-10/13, EAN-13, UPC-A, and incremental mod-97 IBAN validation. ISBN-13 requires 978/979; IBAN enforces all 89 SWIFT Release 102 country prefixes, exact lengths, and BBAN character classes before checksum evaluation. It preserves input, scheme, and results across language changes, uses no CLR or external side effects, and exposes localized names plus a polite live-region accessibility contract.

檢查碼驗證器係下一批真正原生 utility。`checkdigit`、`luhn` 同 `module.checkdigit` 而家會開即時標準 C++ 實作，支援 Luhn／卡種識別、ISBN-10／13、EAN-13、UPC-A 同增量 mod-97 IBAN 驗證。ISBN-13 只接受 978／979；IBAN 會喺 checksum 之前驗證 SWIFT Release 102 全部 89 個國家 prefix、固定長度同 BBAN 字元類別。轉語言時會保留輸入、格式同結果，唔用 CLR 或外部副作用，亦有本地化名稱同 polite live-region 無障礙合約。

Its functional evidence passes, but the route remains **in progress** because fresh visual capture is `capture-blocked`; no stale or managed screenshot is substituted. · 功能證據全部通過，但最新視覺擷取係 `capture-blocked`，所以 route 仍然標示**進行中**；冇用舊圖或受控版截圖頂替。

## Evidence · 證據

- Native Debug and Release x64 builds: 0 errors · 原生 Debug 同 Release x64 建置：0 errors
- Native core tests: Debug 208/208 and Release 208/208; 48 focused cases cover every Check Digit scheme, official Discover range boundaries, Unicode-confusable rejection, and exact comparison with an independent 89-row SWIFT Release 102 fixture while all prior Package Manager and shell regressions remain green. Managed-oracle parity separately passes 24/24. · 原生核心測試：Debug 208/208、Release 208/208；48 個專項案例涵蓋全部檢查碼格式、官方 Discover range 邊界、拒絕 Unicode 混淆字元，同獨立 89 行 SWIFT Release 102 fixture 精確比較，而之前套件管理同 shell 回歸全部保持綠燈；受控基準對等另外 24/24 通過。
- Elevated process-owned UI Automation shell smoke: 46/46, including all six Check Digit schemes, invalid/malformed state, localized accessible names, stale-detail cleanup, language-state retention, every Check Digit alias, and exact Discover/Updates/Installed alias selection; this is not normal-integrity external-query evidence. · 提權、自有 process UI Automation shell 冒煙：46/46，包括六個檢查碼格式、無效／格式錯誤狀態、本地化無障礙名稱、清除舊 detail、轉語言保留狀態、全部檢查碼 alias，同準確揀返 Discover／Updates／Installed alias；呢個唔係正常 integrity 外部查詢證據。
- Catalog/ledger verifier: 346 fixed routes, 5 dynamic families, 319 registry records, and 22 categories · 目錄／清單驗證：346 固定路線、5 組動態路線、319 registry 記錄同 22 分類
- Foundation Release PE audit: zero COM descriptor and no `coreclr`, `hostfxr`, or `mscoree` import; native-binary evidence only, not feature parity. · 基礎 Release PE 審查：COM descriptor 係零，而且冇 `coreclr`、`hostfxr` 或 `mscoree` import；只係原生 binary 證據，唔係功能對等。

Normal-integrity live external-query evidence is **blocked**: even an interactive scheduled task configured with `RunLevel=Limited` launched the exact native executable elevated. Independent token verification stopped with `candidate smoke token is still elevated`, and the harness failed closed before running an external query.

正常 integrity 即時外部查詢證據仍然**受阻**：就算互動式排程工作設為 `RunLevel=Limited`，同一個原生 executable 開出嚟仍然係提權狀態。獨立 token 驗證以 `candidate smoke token is still elevated` 停止，harness 喺執行外部查詢之前已經 fail closed。

Fresh Dashboard, All Apps, About, Package Manager (`module.packages#updates`), and Check Digit (`checkdigit`) screenshot attempts remain `capture-blocked`. The exact driver blocker is: `CopyFromScreen is unavailable and the PrintWindow fallback produced a blank or near-uniform WinUI client frame; graphics capture is unavailable in this desktop session.` The output was rejected; no stale or synthetic image was substituted, and UI Automation evidence is not a visual pass.

最新 Dashboard、所有 app、About、套件管理（`module.packages#updates`）同檢查碼（`checkdigit`）截圖嘗試仍然係 `capture-blocked`。driver 嘅確實阻礙係：`CopyFromScreen is unavailable and the PrintWindow fallback produced a blank or near-uniform WinUI client frame; graphics capture is unavailable in this desktop session.` 呢個輸出已被拒絕；冇用舊圖或合成圖頂替，而 UI Automation 證據唔等於 visual pass。

## Completion gate · 完成閘門

Cutover requires every route, control, service, companion, launcher, updater, installer, protocol, test, document, wiki/Page mirror, and changed screenshot to pass. A final binary audit must prove there is no CLR, C++/CLI, managed assembly, C# subprocess, or wrapper dependency.

正式切換要求每條路線、control、service、companion、launcher、updater、installer、protocol、測試、文件、wiki／Pages 鏡像同改過嘅截圖全部通過。最後 binary audit 必須證明冇 CLR、C++/CLI、受控 assembly、C# subprocess 或 wrapper 相依。

- [Full migration record · 完整遷移記錄](../Native-Cpp-Rewrite.md)
- [Machine-readable parity ledger · Machine-readable 對等清單](../cpp-port-parity.json)
- [Developer guide · 開發者指南](Developer.md)

[← Wiki Home](Home.md)
