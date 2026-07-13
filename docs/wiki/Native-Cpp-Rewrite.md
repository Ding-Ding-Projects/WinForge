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

Package Manager is reachable, including `#discover`, `#updates`, and `#installed`, but its C++ controls/services are not ported yet. The UniGetUI snapshot is reference/provenance only.

套件管理連 `#discover`、`#updates` 同 `#installed` 都開到，但 C++ controls／services 仲未移植。UniGetUI snapshot 只係參考／來源證明。

## Evidence · 證據

- Native Debug and Release x64 builds: 0 errors · 原生 Debug 同 Release x64 建置：0 errors
- Core tests: 21/21 · 核心測試：21/21
- Process-owned UI Automation shell smoke: 20/20 · 自有 process UI Automation shell 冒煙：20/20
- Catalog/ledger parity: pass · 目錄／清單對等：通過
- Foundation Release PE audit: zero COM descriptor and no `coreclr`, `hostfxr`, or `mscoree` import; native-binary evidence only, not feature parity. · 基礎 Release PE 審查：COM descriptor 係零，而且冇 `coreclr`、`hostfxr` 或 `mscoree` import；只係原生 binary 證據，唔係功能對等。

Fresh Dashboard, All Apps, About, and Package Manager (`module.packages#updates`) screenshot attempts are `capture-blocked`: `CopyFromScreen` is unavailable and `PrintWindow` produces a blank/near-uniform WinUI client frame. The driver rejects that output; no stale or synthetic image was substituted. UI Automation evidence is not a visual pass.

最新 Dashboard、所有 app、About 同套件管理（`module.packages#updates`）截圖嘗試係 `capture-blocked`：`CopyFromScreen` 用唔到，而 `PrintWindow` 只產生空白／接近單色 WinUI client frame。driver 會拒絕呢種輸出；冇用舊圖或合成圖頂替。UI Automation 證據唔等於 visual pass。

## Completion gate · 完成閘門

Cutover requires every route, control, service, companion, launcher, updater, installer, protocol, test, document, wiki/Page mirror, and changed screenshot to pass. A final binary audit must prove there is no CLR, C++/CLI, managed assembly, C# subprocess, or wrapper dependency.

正式切換要求每條路線、control、service、companion、launcher、updater、installer、protocol、測試、文件、wiki／Pages 鏡像同改過嘅截圖全部通過。最後 binary audit 必須證明冇 CLR、C++/CLI、受控 assembly、C# subprocess 或 wrapper 相依。

- [Full migration record · 完整遷移記錄](../Native-Cpp-Rewrite.md)
- [Machine-readable parity ledger · Machine-readable 對等清單](../cpp-port-parity.json)
- [Developer guide · 開發者指南](Developer.md)

[← Wiki Home](Home.md)
