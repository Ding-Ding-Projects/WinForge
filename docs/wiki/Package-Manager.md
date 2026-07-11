# Package Manager · 套件管理

![Package Manager](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-packages.png)

**EN —** WinForge's native package-management workspace covers 11 managers: WinGet, Scoop, Chocolatey, pip, npm, .NET tools, Windows PowerShell Gallery, PowerShell 7 PSResourceGet, Cargo, Bun and vcpkg. It combines discovery, updates, installed-package management, portable bundles, source management, ignored-update rules, engine setup, background settings and a shared operation queue.

**粵語 —** WinForge 原生套件管理工作區支援 11 個管理器：WinGet、Scoop、Chocolatey、pip、npm、.NET 工具、Windows PowerShell Gallery、PowerShell 7 PSResourceGet、Cargo、Bun 同 vcpkg。介面集合搜尋、更新、已安裝套件、可攜清單、來源管理、忽略更新規則、引擎設定、背景設定同共用操作佇列。

## Nine views · 九個檢視

| View · 檢視 | What it does · 功能 |
|---|---|
| **Discover · 搜尋安裝** | Search selected available managers together, filter the results and install one or many packages. · 同時搜尋已選而可用嘅管理器、篩選結果，同安裝一個或多個套件。 |
| **Updates · 可更新** | Show eligible updates, update per manager or all managers, and ignore, pin or snooze selected updates. · 顯示可用更新、逐管理器或全部更新，亦可忽略、釘住或暫停指定更新。 |
| **Installed · 已安裝** | List installed packages and run uninstall or other selected operations. · 列出已安裝套件，並可解除安裝或執行其他已選操作。 |
| **Bundles · 套件清單** | Build, edit, import, export and install portable package sets. · 建立、編輯、匯入、匯出同安裝可攜套件清單。 |
| **Sources · 來源** | List, add, remove and refresh supported sources, buckets, feeds and repositories. · 列出、新增、移除同重新整理支援嘅來源、bucket、feed 同 repository。 |
| **Ignored · 已忽略** | Review and remove version pins, all-version ignores and timed snoozes. · 檢視同移除版本釘選、全部版本忽略同限時暫停。 |
| **Setup · 設定引擎** | Check manager availability and bootstrap missing engines or common dependencies. · 檢查管理器係咪可用，同安裝欠缺嘅引擎或常用相依。 |
| **Settings · 設定** | Configure scheduling, update gates, notifications, concurrency, manager paths/arguments, proxy, backup and global install defaults. · 設定排程、更新閘、通知、同時操作數、管理器路徑／參數、代理、備份同全域安裝預設。 |
| **Operations · 操作佇列** | Inspect active, queued and completed work, read captured output, cancel queued/running work, and retry failed or cancelled work. · 檢視進行中、排隊同已完成操作、閱讀輸出、取消排隊／執行中操作，以及重試失敗或已取消操作。 |

## Operations and saved options · 操作同已儲存選項

- A shared bounded-concurrency coordinator handles row actions, multi-select batches, bundle installs and scheduled updates. It suppresses duplicate live operations and keeps completion history with status and output. · 共用、有同時操作上限嘅協調器統一處理單列操作、多選批次、清單安裝同排程更新；會避免重複進行相同操作，並保留狀態同輸出歷史。
- Install options can be saved as global defaults or per-package overrides. Supported settings include operation-specific custom arguments, elevation, interactive mode, hash/prerelease flags, scope, architecture, version, install location, update/uninstall policy, pre/post hooks and process shutdown rules. · 安裝選項可存成全域預設或逐套件覆寫，包括逐操作自訂參數、管理員權限、互動模式、雜湊／預覽版旗標、範圍、架構、版本、安裝位置、更新／解除安裝政策、前後指令鈎同關閉程序規則。
- Package rows expose details, operation options and a copyable command preview; saved options are also applied consistently by batches, bundles and automation. · 套件列可開詳細資料、操作選項同複製指令預覽；已儲存選項亦會一致套用到批次、清單同自動化。

## Bundles, scheduling and source safety · 清單、排程同來源安全

- Bundles support JSON, `.ubundle`, YAML and XML, preserve current per-package options, log explicitly local/incompatible entries, validate manager/package references, and show version or custom-command/argument/process warnings before install. PowerShell install scripts can also be exported. · 清單支援 JSON、`.ubundle`、YAML 同 XML，保留目前逐套件選項、記錄明確本機／不相容項目、驗證管理器同套件參照，並會喺安裝前顯示版本或自訂指令／參數／程序警告；亦可匯出 PowerShell 安裝腳本。
- The background scheduler checks all available managers, respects ignored/snoozed updates, minimum-update age, metered-network, battery and Battery Saver gates, and blocks an automatic WinGet update when a detectable installer host change triggers the security check. · 背景排程器會檢查所有可用管理器，遵守忽略／暫停更新、最低更新年齡、流量計費網絡、電池同慳電模式閘；如果偵測到 WinGet 安裝程式主機有變，安全檢查會阻止自動更新。
- Source mutations are limited to supported managers. Source names use an allow-list, URLs must be credential-free HTTP(S), standard-source removal is called out for confirmation, and managers that require elevation request it only for the mutation. · 只會為支援嘅管理器修改來源；來源名稱用允許清單，網址必須係冇內嵌帳密嘅 HTTP(S)，移除標準來源前會特別確認，而需要提升權限嘅管理器只會喺修改時要求提升。

## UniGetUI provenance · UniGetUI 來源依據

The complete tracked [Devolutions/UniGetUI](https://github.com/Devolutions/UniGetUI) tree is vendored at `ThirdParty/UniGetUI` and pinned to commit `21116375c8299d1db38a3c3b4c2eb7e18bc97c4e`. UniGetUI itself is MIT-licensed; bundled third-party material remains under its separate upstream notices. The snapshot is provenance for auditing and native feature-parity work. `ThirdParty/**` is excluded from WinForge's build and publish inputs: the upstream UI/framework, IPC and telemetry projects are not compiled, embedded or launched. The runtime package manager is WinForge's own bilingual WinUI 3 UI and managed services over the actual package-manager engines; it does not claim literal UniGetUI runtime identity.

完整追蹤嘅 [Devolutions/UniGetUI](https://github.com/Devolutions/UniGetUI) 原始碼樹存放喺 `ThirdParty/UniGetUI`，並固定到 commit `21116375c8299d1db38a3c3b4c2eb7e18bc97c4e`。UniGetUI 本身採用 MIT 授權；隨附第三方材料仍然跟返上游各自嘅 notice。快照用途係審核同原生功能對等參考。`ThirdParty/**` 已排除喺 WinForge 建置同發佈輸入之外；上游 UI／framework、IPC 同 telemetry 專案唔會被編譯、嵌入或啟動。實際執行嘅套件管理器係 WinForge 自己嘅雙語 WinUI 3 介面同受管理服務，操作真正套件管理引擎，唔會聲稱同 UniGetUI 執行階段完全相同。

---
[← Module index · 模組索引](Home) · [README](https://github.com/codingmachineedge/WinForge/blob/main/README.md) · [Screenshots](Screenshots)
