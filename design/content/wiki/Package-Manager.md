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

## Responsive and accessible controls · 響應式同無障礙控制

Search and view controls occupy their own row; filters and actions move to a horizontally scrollable toolbar. Manager and batch strips remain scrollable, action targets are at least 44×44 pixels, section headings expose heading semantics, and dynamic selection/output controls have programmatic names. The bilingual page was inspected at both 1049×646 and 720×650 without overlapping or off-screen action controls. · 搜尋同 view control 會用獨立一行；filter 同 action 放入可橫向捲動 toolbar。管理器同批次列可以捲動，action target 最少 44×44 像素，section heading 有 heading semantics，動態選取／輸出控制亦有程式化名稱。雙語頁已喺 1049×646 同 720×650 檢視，冇重疊或者走出畫面嘅 action control。

![Package Manager narrow layout · 套件管理窄版面](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-packages-narrow.png)

## Safety and failure behavior · 安全同失敗行為

- Package mutations are explicit; review surfaces must not silently execute a package command. · 套件修改一定要明確；檢視介面唔可以靜默執行套件指令。
- WinForge refuses interactive package execution while elevated when its normal-integrity boundary cannot be maintained. · WinForge 提權時，如果保持唔到正常 integrity 界線，就會拒絕互動套件執行。
- Manager availability is probed before dependent actions are enabled. A missing engine is shown as a setup dependency, not treated as success. · 啟用相依動作前會先探測管理器；欠缺引擎會顯示成設定 dependency，唔會當成功。
- User-facing errors remain redacted and must not expose credentials, tokens, or unsafe command construction. · 對使用者顯示嘅錯誤要遮蔽，唔可以洩露認證資料、token 或唔安全 command 組合。
- Cancellation and retry apply to owned package operations; WinForge must not terminate unrelated external processes. · 取消同重試只適用於 WinForge 自己嘅套件操作，唔可以終止不相關外部 process。
- Proxy settings accept only a credential-free HTTP(S) authority. Paths, queries, fragments, raw percent expansion, control characters, and embedded credentials fail closed. · Proxy 只接受唔含認證嘅 HTTP(S) authority；path、query、fragment、原始百分號展開、控制字元同內嵌認證全部 fail closed。
- A vcpkg triplet is a bounded token containing only letters, numbers, dots, underscores, and dashes; invalid input is rejected before persistence or command construction. · vcpkg triplet 係有界 token，只可以用字母、數字、點、底線同橫線；無效輸入喺保存或者建立指令前已經拒絕。
- Bundle saves are written to a unique same-directory staging file and swapped into place only after the complete payload exists. A failed save leaves the previous file unchanged, keeps the editor dirty, and reports a bilingual non-blocking error instead of false success. · 套件清單會先寫到同一資料夾嘅唯一暫存檔，完整寫好先交換入位；失敗會保留舊檔、保持編輯器未儲存，並用雙語非阻塞錯誤如實回報。

## Configuration · 設定

Package Manager preferences are stored through the application's normal settings/persistence services. Secrets or credentials must use the existing DPAPI-backed stores and must never be written to logs, screenshots, command lines, URLs, or repository files.

套件管理器偏好會經 app 正常 settings／persistence service 保存。秘密或認證資料一定要用既有 DPAPI store，絕對唔可以寫入 log、截圖、command line、URL 或 repository file。

Invalid structured settings produce a bilingual inline error and restore the last valid value. WinForge no longer collects proxy usernames/passwords; detected legacy values remain DPAPI-protected until the user chooses **Forget saved credentials** or resets package settings. They are never used in a URL or process argument. Authenticated proxy credentials must be configured in the operating-system or package-manager credential store. · 無效結構化設定會顯示雙語 inline error，並還原上一個有效值。WinForge 已經唔再收集 proxy 使用者名稱／密碼；偵測到嘅舊值會保持 DPAPI 保護，直到用戶揀 **刪除已保存認證** 或重設套件設定，而且絕對唔會用喺 URL／process argument。認證 proxy 要用 Windows 或套件管理器 credential store。

![Credential-free proxy and validated vcpkg settings · 無 credential proxy 同已驗證 vcpkg 設定](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-package-settings-proxy.png)

The dialog's App Settings actions stack as full-width 44-pixel targets, so the longest bilingual labels remain visible at a 720-pixel window width. · Dialog 嘅 App Settings action 會用 44 像素全闊直排，所以 720 像素視窗都睇得晒最長雙語 label。

![Narrow-safe App Settings actions · 窄畫面安全 App Settings actions](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-package-settings-actions.png)

## Verification · 驗證

- Package-manager core harness: **30/30 passed**, including atomic create/replace/failure-preservation saves and malicious proxy/triplet rejection; the detailed bundle record is [Portable package bundles](https://github.com/codingmachineedge/WinForge/blob/main/docs/features/package-management/portable-package-bundles.md). · 套件管理 core harness **30/30 通過**，包括原子建立／取代／失敗保留儲存同惡意 proxy／triplet 拒絕；詳細記錄見[可攜套件清單](https://github.com/codingmachineedge/WinForge/blob/main/docs/features/package-management/portable-package-bundles.md)。
- Exact solution build and self-contained publish: exit 0, build with **0 errors**. · 完整 solution build 同自包含 publish exit 0，build **零 errors**。
- XAML literal safety passed; 2,875/2,875 handlers and 1,922/1,922 direct actions resolved with zero lifecycle mismatches or actionable markers. · XAML literal safety 通過；2,875/2,875 handler 同 1,922/1,922 direct action 全部 resolve，零 lifecycle mismatch／actionable marker。
- Fresh LowLevel MCP headless captures were visually inspected at 1049×646 and 720×650, including the proxy/vcpkg and final App Settings dialog sections; the exact owned app window was closed, the desktop reported zero windows, and its handle was released afterward. · 最新 LowLevel MCP headless capture 已喺 1049×646／720×650 人手檢視，包括 proxy／vcpkg 同最底 App Settings dialog；之後已關閉準確自家 app 視窗、確認 desktop 零視窗並釋放 handle。

## Independent C++ port · 獨立 C++ 移植版

The experimental C++ Package Manager work and its historical parity evidence now belong to [codingmachineedge/WinForge-Native](https://github.com/codingmachineedge/WinForge-Native). They are not the shipping behavior documented on this page.

實驗性 C++ 套件管理工作同歷史 parity 證據而家屬於 [codingmachineedge/WinForge-Native](https://github.com/codingmachineedge/WinForge-Native)，唔係呢頁記錄嘅正式 app 行為。

[← Wiki Home](#/wiki/Home)
