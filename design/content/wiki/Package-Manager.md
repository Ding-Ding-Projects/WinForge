# Package Manager · 套件管理

![Package Manager · 套件管理](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-packages.png)

WinForge's canonical .NET Package Manager is an in-app workspace for discovering, installing, updating, reviewing, and removing packages across Windows package engines. Open it with `WinForge.exe --page packages`.

WinForge 正式 .NET 套件管理器係 app 內工作區，可以經多個 Windows 套件引擎搜尋、安裝、更新、檢視同移除套件。用 `WinForge.exe --page packages` 開啟。

## Workspaces · 工作區

| View · 檢視 | Managed behavior · 正式功能 |
|---|---|
| **Discover · 搜尋安裝** | Search selected engines, filter results, inspect details, and explicitly install selected packages. · 搜尋已揀引擎、篩選結果、睇詳細資料，再明確安裝所選套件。 |
| **Updates · 可更新** | Enumerate updates, inspect package details, update selected items, and apply ignore/pin/snooze rules. · 列出更新、睇詳細資料、更新所選項目，同套用忽略／釘選／暫停規則。 |
| **Installed · 已安裝** | Review installed packages and explicitly choose supported package operations. · 檢視已安裝套件，再明確揀支援嘅套件操作。 |
| **Bundles · 套件清單** | Build, edit, import, export, and review portable package sets. · 建立、編輯、匯入、匯出同檢視可攜套件清單。 |
| **Sources · 來源** | Review and manage supported feeds, buckets, and repositories. · 檢視同管理支援嘅 feed、bucket 同 repository。 |
| **Ignored · 已忽略** | Review and remove version pins, all-version ignores, and timed snoozes. · 檢視同移除版本釘選、全部版本忽略同限時暫停。 |
| **Setup · 設定引擎** | Check package-engine availability and review bootstrap/dependency setup. · 檢查套件引擎可用性，同檢視 bootstrap／dependency 設定。 |
| **Settings · 設定** | Persist schedules, notifications, manager paths, proxy, backup, and install defaults. · 保存排程、通知、管理器路徑、proxy、backup 同安裝預設。 |
| **Operations · 操作記錄** | Track queued, active, completed, failed, and cancelled package work. · 追蹤排隊、執行中、完成、失敗同已取消套件工作。 |

## Package engines · 套件引擎

The workspace supports WinGet, Scoop, Chocolatey, pip, npm, .NET tools, Windows PowerShell Gallery, PowerShell 7 PSResourceGet, Cargo, Bun, and vcpkg where the corresponding engine is available.

當相應引擎可用時，工作區支援 WinGet、Scoop、Chocolatey、pip、npm、.NET tools、Windows PowerShell Gallery、PowerShell 7 PSResourceGet、Cargo、Bun 同 vcpkg。

## Safety and failure behavior · 安全同失敗行為

- Package mutations are explicit; review surfaces must not silently execute a package command. · 套件修改一定要明確；檢視介面唔可以靜默執行套件指令。
- WinForge refuses interactive package execution while elevated when its normal-integrity boundary cannot be maintained. · WinForge 提權時，如果保持唔到正常 integrity 界線，就會拒絕互動套件執行。
- Manager availability is probed before dependent actions are enabled. A missing engine is shown as a setup dependency, not treated as success. · 啟用相依動作前會先探測管理器；欠缺引擎會顯示成設定 dependency，唔會當成功。
- User-facing errors remain redacted and must not expose credentials, tokens, or unsafe command construction. · 對使用者顯示嘅錯誤要遮蔽，唔可以洩露認證資料、token 或唔安全 command 組合。
- Cancellation and retry apply to owned package operations; WinForge must not terminate unrelated external processes. · 取消同重試只適用於 WinForge 自己嘅套件操作，唔可以終止不相關外部 process。

## Configuration · 設定

Package Manager preferences are stored through the application's normal settings/persistence services. Secrets or credentials must use the existing DPAPI-backed stores and must never be written to logs, screenshots, command lines, URLs, or repository files.

套件管理器偏好會經 app 正常 settings／persistence service 保存。秘密或認證資料一定要用既有 DPAPI store，絕對唔可以寫入 log、截圖、command line、URL 或 repository file。

## Independent C++ port · 獨立 C++ 移植版

The experimental C++ Package Manager work and its historical parity evidence now belong to [codingmachineedge/WinForge-Native](https://github.com/codingmachineedge/WinForge-Native). They are not the shipping behavior documented on this page.

實驗性 C++ 套件管理工作同歷史 parity 證據而家屬於 [codingmachineedge/WinForge-Native](https://github.com/codingmachineedge/WinForge-Native)，唔係呢頁記錄嘅正式 app 行為。

[← Wiki Home](#/wiki/Home)
