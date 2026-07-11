# Package Manager · 套件管理

![Package Manager](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-packages.png)

**EN —** WinForge's native package-management workspace covers 11 managers: WinGet, Scoop, Chocolatey, pip, npm, .NET tools, Windows PowerShell Gallery, PowerShell 7 PSResourceGet, Cargo, Bun and vcpkg. Its nine views cover discovery, updates, installed packages, bundles, sources, ignored rules, setup, settings and the shared operation queue.

**粵語 —** WinForge 原生套件管理工作區支援 11 個管理器：WinGet、Scoop、Chocolatey、pip、npm、.NET 工具、Windows PowerShell Gallery、PowerShell 7 PSResourceGet、Cargo、Bun 同 vcpkg。九個檢視涵蓋搜尋、更新、已安裝套件、清單、來源、忽略規則、引擎設定、背景設定同共用操作佇列。

## Manager-specific correctness · 各管理器操作保障

- Setup dependencies use the shared coordinator and are marked installed only after success. · Setup 相依會經共用協調器執行，淨係成功先標記做已安裝。
- .NET tool operations choose either `--global` or `--tool-path`, never both. · .NET 工具操作只會用 `--global` 或 `--tool-path` 其中一個，唔會兩個一齊用。
- PowerShell 7 honors requested versions and removes older versions only after a successful update. · PowerShell 7 會跟指定版本，亦只會喺更新成功後先清走舊版本。
- Bun searches the npm registry directly and reads global state from `~/.bun/install/global`, without requiring npm. · Bun 直接搜尋 npm registry，再由 `~/.bun/install/global` 讀取全域狀態，唔需要 npm。
- Safe IDs include vcpkg feature commas and npm tildes, while shell metacharacters stay blocked. · 安全 ID 支援 vcpkg feature 逗號同 npm 波浪號，shell 特殊字元仍然會被阻擋。

The complete pinned `ThirdParty/UniGetUI` source remains provenance for audit and native parity work only. Its UI, IPC and telemetry are not compiled or launched. · 完整釘選嘅 `ThirdParty/UniGetUI` 原始碼只作審核同原生功能對等參考；上游 UI、IPC 同 telemetry 唔會被編譯或啟動。

---
[← Module index · 模組索引](Home) · [README](https://github.com/codingmachineedge/WinForge/blob/main/README.md) · [Screenshots](Screenshots)
