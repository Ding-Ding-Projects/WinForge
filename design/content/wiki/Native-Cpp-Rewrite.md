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

## Evidence · 證據

- Native Debug and Release x64 builds: 0 errors · 原生 Debug 同 Release x64 建置：0 errors
- Native core tests: Debug 160/160 and Release 160/160, including Package Manager alias-to-view routing · 原生核心測試：Debug 160/160、Release 160/160，包括套件管理 alias 對應準確檢視
- Elevated process-owned UI Automation shell smoke: 31/31, including exact Discover/Updates/Installed alias selection; this is not normal-integrity external-query evidence. · 提權、自有 process UI Automation shell 冒煙：31/31，包括準確揀返 Discover／Updates／Installed alias；呢個唔係正常 integrity 外部查詢證據。
- Catalog/ledger verifier: 346 fixed routes, 5 dynamic families, 319 registry records, and 22 categories · 目錄／清單驗證：346 固定路線、5 組動態路線、319 registry 記錄同 22 分類
- Foundation Release PE audit: zero COM descriptor and no `coreclr`, `hostfxr`, or `mscoree` import; native-binary evidence only, not feature parity. · 基礎 Release PE 審查：COM descriptor 係零，而且冇 `coreclr`、`hostfxr` 或 `mscoree` import；只係原生 binary 證據，唔係功能對等。

Normal-integrity live external-query evidence is **blocked**: even an interactive scheduled task configured with `RunLevel=Limited` launched the exact native executable elevated. Independent token verification stopped with `candidate smoke token is still elevated`, and the harness failed closed before running an external query.

正常 integrity 即時外部查詢證據仍然**受阻**：就算互動式排程工作設為 `RunLevel=Limited`，同一個原生 executable 開出嚟仍然係提權狀態。獨立 token 驗證以 `candidate smoke token is still elevated` 停止，harness 喺執行外部查詢之前已經 fail closed。

Fresh Dashboard, All Apps, About, and Package Manager (`module.packages#updates`) screenshot attempts remain `capture-blocked`. The exact driver blocker is: `CopyFromScreen is unavailable and the PrintWindow fallback produced a blank or near-uniform WinUI client frame; graphics capture is unavailable in this desktop session.` The output was rejected; no stale or synthetic image was substituted, and UI Automation evidence is not a visual pass.

最新 Dashboard、所有 app、About 同套件管理（`module.packages#updates`）截圖嘗試仍然係 `capture-blocked`。driver 嘅確實阻礙係：`CopyFromScreen is unavailable and the PrintWindow fallback produced a blank or near-uniform WinUI client frame; graphics capture is unavailable in this desktop session.` 呢個輸出已被拒絕；冇用舊圖或合成圖頂替，而 UI Automation 證據唔等於 visual pass。

## Completion gate · 完成閘門

Cutover requires every route, control, service, companion, launcher, updater, installer, protocol, test, document, wiki/Page mirror, and changed screenshot to pass. A final binary audit must prove there is no CLR, C++/CLI, managed assembly, C# subprocess, or wrapper dependency.

正式切換要求每條路線、control、service、companion、launcher、updater、installer、protocol、測試、文件、wiki／Pages 鏡像同改過嘅截圖全部通過。最後 binary audit 必須證明冇 CLR、C++/CLI、受控 assembly、C# subprocess 或 wrapper 相依。

- [Full migration record · 完整遷移記錄](https://github.com/codingmachineedge/WinForge/blob/main/docs/Native-Cpp-Rewrite.md)
- [Machine-readable parity ledger · Machine-readable 對等清單](https://github.com/codingmachineedge/WinForge/blob/main/docs/cpp-port-parity.json)
- [Developer guide · 開發者指南](#/wiki/Developer)

[← Wiki Home](#/wiki/Home)
