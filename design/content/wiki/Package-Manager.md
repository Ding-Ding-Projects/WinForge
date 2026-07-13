# Package Manager · 套件管理

![Managed production Package Manager reference · 受控正式版套件管理參考](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-packages.png)

> **Screenshot provenance · 截圖來源：** This existing image is a managed .NET production reference, **not current native C++ visual evidence**. Fresh native capture is `capture-blocked`: `CopyFromScreen` is unavailable and `PrintWindow` returns a blank or near-uniform WinUI client frame that the driver rejects. No stale, blank, synthetic, or managed image is substituted as a native screenshot. · 呢張現有圖片只係受控 .NET 正式版參考，**唔係目前原生 C++ 視覺證據**。最新原生擷取係 `capture-blocked`：`CopyFromScreen` 用唔到，而 `PrintWindow` 回傳空白或者接近單色嘅 WinUI client frame，driver 會拒絕。唔會用舊圖、空白圖、合成圖或者受控版圖片頂替原生截圖。

## Production and native migration · 正式版同原生遷移

The shipping managed Package Manager remains the production implementation. It has substantial functionality across 11 Windows package engines and nine workflows: discovery/install, updates, installed-package operations, bundles, sources, ignored rules, setup, settings, and a shared operation queue.

發佈中受控套件管理器仍然係正式版本。佢喺 11 個 Windows 套件引擎同九個流程都有大量實際功能：搜尋／安裝、更新、已安裝套件操作、清單、來源、忽略規則、引擎設定、設定同共用操作佇列。

The C++20/C++/WinRT migration is honestly **in progress**, not a full UniGetUI clone, parity claim, or production cutover. The native shell exposes all nine views and the same 11 manager choices—WinGet, Scoop, Chocolatey, pip, npm, .NET tools, Windows PowerShell Gallery, PowerShell 7 PSResourceGet, Cargo, Bun, and vcpkg—but only three read-only result queries and one source-command probe are active:

C++20／C++/WinRT 遷移會如實標示為**進行中**，唔係完整 UniGetUI 複製品、對等聲稱或者正式版切換。原生 shell 顯示全部九個檢視，同一組 11 個管理器——WinGet、Scoop、Chocolatey、pip、npm、.NET 工具、Windows PowerShell Gallery、PowerShell 7 PSResourceGet、Cargo、Bun 同 vcpkg——但目前只開放三條只讀結果查詢同一個來源指令探測：

| View · 檢視 | Current native C++ truth · 目前原生 C++ 實況 |
|---|---|
| **Discover · 搜尋安裝** | Read-only search; install and bulk buttons remain disabled. · 只讀搜尋；安裝同批次按鈕保持停用。 |
| **Updates · 可更新** | Read-only enumeration; per-row and Update All mutations remain disabled. · 只讀列出更新；逐列同全部更新操作保持停用。 |
| **Installed · 已安裝** | Read-only enumeration; uninstall remains disabled. · 只讀列出已安裝套件；解除安裝保持停用。 |
| **Sources · 來源** | Command probe only: read-only source commands run, but all raw configuration/diagnostics are withheld until manager-specific secret redaction is proven; no source rows are shown and add/remove remains disabled. · 只限指令探測：會執行只讀來源指令，但逐管理器機密遮罩未證實之前會隱藏全部原始設定／診斷；唔會顯示來源資料列，新增／移除保持停用。 |
| **Bundles · 套件清單** | Gated placeholder. · 鎖住嘅 placeholder。 |
| **Ignored · 已忽略** | Gated placeholder. · 鎖住嘅 placeholder。 |
| **Setup · 設定引擎** | Non-destructive availability probes only; bootstrap remains gated. · 只限非破壞性可用性探測；bootstrap 仍然鎖住。 |
| **Settings · 設定** | Gated placeholder. · 鎖住嘅 placeholder。 |
| **Operations · 操作佇列** | Transient read-only event log only; the real coordinator/history/cancel/reorder/retry workflow remains gated. · 只限暫時只讀事件記錄；真正協調器／歷史／取消／重新排序／重試流程仍然鎖住。 |

All install, update, uninstall, source, and bulk mutations remain separately gated. Native queries use reviewed argument-vector builders, bounded parsers, allowlisted HTTPS endpoints, and a contained Win32 process runner. External manager commands fail closed while WinForge is elevated; no upstream UniGetUI executable or UI is launched.

所有安裝、更新、解除安裝、來源同批次修改仍然另外鎖住。原生查詢使用經審核嘅 argument-vector builder、有界解析器、准許清單 HTTPS endpoint 同受控 Win32 process runner。WinForge 提升權限時，外部管理器指令會 fail closed；完全唔會啟動上游 UniGetUI executable 或 UI。

## Evidence and blockers · 證據同阻礙

- The expanded native suite passes **208/208 in Debug and 208/208 in Release**. Every prior Package Manager regression remains, including singular/plural alias-to-view routing; the additional checks cover Check Digit. · 擴充後原生套件 Debug **208/208**、Release **208/208** 通過；之前全部套件管理回歸都保留，包括單數／複數 alias 對應準確檢視，新增檢查就覆蓋檢查碼。
- The elevated, process-owned UI Automation shell passes **46/46**. Every prior Package Manager assertion remains for routing—including exact Discover/Updates/Installed alias selection—filters, view/state contracts, fail-closed elevation behavior, and accessibility; added assertions cover Check Digit accessibility hardening. It does not prove live external queries. · 提權、只控制自己 process 嘅 UI Automation shell **46/46** 通過；之前全部套件管理 assertion 都保留，涵蓋導覽（包括準確揀返 Discover／Updates／Installed alias）、篩選、檢視／狀態合約、fail-closed 提權行為同無障礙，新增 assertion 就覆蓋檢查碼無障礙加固。唔代表真實外部查詢已通過。
- Normal-integrity live smoke is blocked before package execution: even an interactive `RunLevel=Limited` task received a token that failed the standard-user proof. The harness stopped safely; no live-query pass is claimed. · 正常權限 live smoke 喺套件執行之前受阻：即使互動式工作設為 `RunLevel=Limited`，收到嘅 token 仍然未通過標準使用者證明。harness 已安全停止；唔會聲稱 live query 通過。
- Native visual evidence remains `capture-blocked` for the exact reason recorded above. · 原生視覺證據仍然因上面記錄嘅確實原因標示為 `capture-blocked`。

Remaining UniGetUI-informed work includes the capability/maintenance/log model; persisted sorting, views, media/share, and open-location actions; durable queue ordering/history/retry; complete bundle/download/installed-detection interoperability; secure opt-in/elevation/batch consent; backup and per-manager settings; actionable notifications/tray commands; and authenticated API/CLI/headless/deep-link/widget surfaces. Linux- and macOS-only managers are outside this Windows product's scope.

尚欠嘅 UniGetUI 參考工作包括 capability／維護／日誌模型；持久化排序、檢視、媒體／分享同開啟位置動作；可保存佇列次序／歷史／重試；完整清單／下載／已安裝偵測互通；安全 opt-in／提升權限／批次同意；備份同逐管理器設定；可操作通知／系統匣指令；以及已驗證身份嘅 API／CLI／headless／deep-link／widget 介面。只供 Linux 同 macOS 嘅管理器唔屬於呢個 Windows 產品範圍。

## Deep links and provenance · 深層連結同來源依據

`--page package-discover`, `--page package-updates`, and `--page package-installed` resolve to `module.packages#discover`, `module.packages#updates`, and `module.packages#installed`. Routing is verified, but route availability alone is not parity evidence. · 三條 `--page` 指令會解析去對應 `module.packages` deep link；導覽已驗證，但淨係有 route 唔代表功能對等。

The complete 1,002-file tracked [Devolutions/UniGetUI](https://github.com/Devolutions/UniGetUI) tree is vendored at `ThirdParty/UniGetUI`, pinned to upstream `main` commit `21116375c8299d1db38a3c3b4c2eb7e18bc97c4e` (2026-07-10) under the MIT license. `ThirdParty/**` is excluded from WinForge build and publish inputs. The snapshot is exact provenance and a behavior inventory only—not an embedded runtime, copied identity, or parity claim.

完整 1,002 檔 [Devolutions/UniGetUI](https://github.com/Devolutions/UniGetUI) tracked tree 存放喺 `ThirdParty/UniGetUI`，按 MIT 授權固定到上游 `main` commit `21116375c8299d1db38a3c3b4c2eb7e18bc97c4e`（2026-07-10）。`ThirdParty/**` 已排除喺 WinForge 建置同發佈輸入之外。snapshot 只係精確來源依據同行為清單，唔係內嵌 runtime、複製身份或者對等聲稱。

---
[← Module index · 模組索引](Home) · [README](https://github.com/codingmachineedge/WinForge/blob/main/README.md) · [Native rewrite · 原生重寫](Native-Cpp-Rewrite) · [Screenshots](Screenshots)
