# Package Manager · 套件管理

![Package Manager](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-packages.png)

**EN —** WinForge's native package-management workspace covers 11 managers: WinGet, Scoop, Chocolatey, pip, npm, .NET tools, Windows PowerShell Gallery, PowerShell 7 PSResourceGet, Cargo, Bun and vcpkg. Its nine views cover discovery, updates, installed packages, bundles, sources, ignored rules, setup, settings and the shared operation queue.

**粵語 —** WinForge 原生套件管理工作區支援 11 個管理器：WinGet、Scoop、Chocolatey、pip、npm、.NET 工具、Windows PowerShell Gallery、PowerShell 7 PSResourceGet、Cargo、Bun 同 vcpkg。九個檢視涵蓋搜尋、更新、已安裝套件、清單、來源、忽略規則、引擎設定、背景設定同共用操作佇列。

## Tray shortcuts and deep links · 系統匣捷徑同深層連結

- The system-tray **Discover packages**, **Updates**, and **Installed packages** commands bring WinForge forward and select that exact Package Manager view, instead of the generic default. · 系統匣嘅 **搜尋安裝**、**可更新** 同 **已安裝** 指令會帶返 WinForge 去前景，並直接揀返對應嘅套件管理檢視，而唔係一般預設頁。
- Use `--page package-discover`, `--page package-updates`, or `--page package-installed` for fresh automation. They resolve to native `module.packages#discover`, `module.packages#updates`, and `module.packages#installed` routes; no upstream executable or UI is launched. · 自動化可用 `--page package-discover`、`--page package-updates` 或 `--page package-installed`。佢哋會解析去原生 `module.packages#discover`、`module.packages#updates` 同 `module.packages#installed` route；完全唔會啟動上游 executable 或 UI。
- The smoke inventory records these three additional aliases under `module.packages`, with no unmapped alias or structural routing issue. · Smoke inventory 已將呢三個新增 alias 記錄喺 `module.packages` 之下，冇任何 unmapped alias 或結構導覽問題。

## Manager-specific correctness · 各管理器操作保障

- Setup dependencies use the shared coordinator and are marked installed only after success. · Setup 相依會經共用協調器執行，淨係成功先標記做已安裝。
- .NET tool operations choose either `--global` or `--tool-path`, never both. · .NET 工具操作只會用 `--global` 或 `--tool-path` 其中一個，唔會兩個一齊用。
- PowerShell 7 honors requested versions and removes older versions only after a successful update. · PowerShell 7 會跟指定版本，亦只會喺更新成功後先清走舊版本。
- Bun searches the npm registry directly and reads global state from `~/.bun/install/global`, without requiring npm. · Bun 直接搜尋 npm registry，再由 `~/.bun/install/global` 讀取全域狀態，唔需要 npm。
- Safe IDs include vcpkg feature commas and npm tildes, while shell metacharacters stay blocked. · 安全 ID 支援 vcpkg feature 逗號同 npm 波浪號，shell 特殊字元仍然會被阻擋。
- Selected package sources are preserved through preview, multi-select/bundle identity, queueing and execution by `PackageSourcePolicy`. It emits only manager-valid source forms (`--source`, a Scoop bucket-qualified id, fixed trusted registry endpoints, or supported PowerShell repository parameters); empty sources keep the configured default, and no-selector update/remove operations retain validated source metadata without inventing a flag. Local, unknown, malformed and unsupported source text is rejected before it can reach a command. · `PackageSourcePolicy` 會保留所揀套件來源，經過預覽、多選／清單身份、排隊同執行。佢只會輸出管理器有效嘅來源形式（`--source`、Scoop bucket-qualified id、固定可信 registry endpoint，或者支援嘅 PowerShell repository 參數）；空白來源保留已設定預設，冇來源選擇器嘅更新／移除操作會保留已驗證來源中繼資料而唔會亂加旗標。本機、未知、格式錯誤同唔支援嘅來源文字未到指令之前就會被拒絕。

The complete pinned `ThirdParty/UniGetUI` source remains provenance for audit and native parity work only. Its UI, IPC and telemetry are not compiled or launched. · 完整釘選嘅 `ThirdParty/UniGetUI` 原始碼只作審核同原生功能對等參考；上游 UI、IPC 同 telemetry 唔會被編譯或啟動。

---
[← Module index · 模組索引](Home) · [README](https://github.com/codingmachineedge/WinForge/blob/main/README.md) · [Screenshots](Screenshots)
