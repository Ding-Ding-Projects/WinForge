# WinForge — Handoff reference (per feature) · 交接參考（逐項功能）

## Native Regex Tester integration proof · 原生 Regex Tester 整合證明

`module.regextester` now enumerates at most **100** bounded non-overlapping matches, carries named capture metadata, safely advances zero-length matches under one shared deadline, and previews local replacement. The direct tester is case-sensitive by default; the builder carries PCRE2 `(x)`/`(n)` state to Shell, All Apps, cache-only Package Discover, and Regex Cheatsheet. Replacement is intentionally a local subset only: `$$`, existing `$0`–`$99`, and `${name}`; invalid replacement or the 32 KiB cap fails closed. Debug/Release each passed **403/403** and isolated LowLevel MCP headless UI Automation passed **226/226**. Inspected 852×880 full and 836×841 client frames were blank and discarded, so this is `capture-blocked`. Task `72ce549110b3d235b406de397736e89ecbcdb055` (also the remote feature tip) merged as `f7cba1a4694df705cd483868755af079e6250fda`; after fetch, both plus the merge were proven ancestors of `origin/main` and the expected source/test/docs/Pages/parity/handoff paths were confirmed on remote main before cleanup.

`module.regextester` 而家列舉最多 **100** 個有界非重疊相符、帶住命名 capture metadata、喺同一個 deadline 下安全處理零長度相符，並預覽本機 replacement。直接 Tester 預設 case-sensitive；builder 會將 PCRE2 `(x)`／`(n)` 狀態帶去 Shell、All Apps、只限快取嘅 Package Discover 同 Regex Cheatsheet。replacement 刻意只係本機子集：`$$`、存在嘅 `$0`–`$99` 同 `${name}`；無效 replacement 或 32 KiB cap 都會 fail closed。Debug／Release 都通過 **403/403**，isolated LowLevel MCP headless UI Automation 通過 **226/226**。檢查過嘅 852×880 full 同 836×841 client frame 係空白並已丟棄，所以係 `capture-blocked`。task `72ce549110b3d235b406de397736e89ecbcdb055`（亦係 remote feature tip）以 `f7cba1a4694df705cd483868755af079e6250fda` 合併；fetch 後已證明佢哋加 merge 都係 `origin/main` 嘅 ancestor，亦已確認 expected source／test／docs／Pages／parity／handoff path 都喺 remote main，先做清理。

## Latest native continuation — 2026-07-16

`module.regexcheat` is now a real C++/WinRT route with a pure-C++ 67-row/9-category bilingual reference catalog, eight copy-only ready-made patterns, default literal filtering, and an explicit bounded-PCRE2 local filter. It is the fourth registered native regex search surface and can round-trip a verified pattern through the full builder. .NET-only reference syntax remains documentation and never executes. Debug/Release native tests passed **395/395**, catalog parity passed 346 fixed routes plus five dynamic families, and isolated LowLevel MCP UI Automation passed **224/224**. The inspected 852×880 full and 836×841 client frames were blank, discarded, and recorded as `capture-blocked`; no stale or managed image is used as native evidence.

`module.regexcheat` 而家係真正嘅 C++/WinRT route，有純 C++ 67 項／9 分類雙語參考、八個只可明確複製嘅現成模式、預設 literal 篩選同明確啟用嘅 bounded-PCRE2 本機篩選。佢係第四個已註冊 native regex search surface，亦可以同完整 builder round-trip 已驗證嘅模式。.NET 專用語法只係文件，唔會執行。Debug/Release **395/395**、catalog parity 同隔離 LowLevel MCP UI Automation **224/224** 都通過；852×880 full 同 836×841 client frame 空白、已丟棄，狀態係 `capture-blocked`。

### Git integration proof · Git 整合證明

The native Regex Cheatsheet task commit `24f32ba85eade7244dc839760807ea3ea3d1a5d9` was merged into `main` as `2872b234022188d70f250fdbae3d78a740f68fa8`. After fetching, both that task commit and the remote feature-branch tip were proven ancestors of `origin/main`; `AGENTS.md`, the handoff records, native sources/tests, parity ledger, and wiki/Page mirrors were confirmed in the remote main tree before cleanup.

原生 Regex Cheatsheet task commit `24f32ba85eade7244dc839760807ea3ea3d1a5d9` 已經以 `2872b234022188d70f250fdbae3d78a740f68fa8` 合併入 `main`。fetch 後已證明 task commit 同 remote feature-branch tip 都係 `origin/main` 嘅 ancestor；清理前亦已確認 `AGENTS.md`、handoff records、原生 sources／tests、parity ledger 同 wiki／Pages mirrors 都喺 remote main tree。

A complete map of every module/feature: what it does, how to open it, the page + service files, and the
**real engine it wraps**. WinForge is a bilingual (English + 粵語) WinUI 3 / .NET 11 suite for Windows 11.
**No redirects** — every shipping feature runs in-app and wraps a real engine/API. A genuine C++20/C++/WinRT rewrite now lives beside this managed oracle; its foundation is routable and verified, but feature parity remains evidence-gated and is not yet claimed.

完整列出每個模組／功能：做乜、點開、頁面同服務檔案、同埋包住嘅真實引擎。WinForge 係雙語（英文 + 粵語）
嘅 WinUI 3 / .NET 11 Windows 11 套件。**唔跳轉** — 每個發佈中功能都喺 app 內運行、包住真實引擎／API。而家亦有真正 C++20/C++/WinRT 重寫同受控 oracle 並存；基礎 shell 已可路由同驗證，但功能對等仍要逐項證據把關，未聲稱完成。

## Build / run / release · 建置／運行／發佈
- Build: `dotnet build -c Debug -p:Platform=x64` (must stay 0 errors).
- Native build: `msbuild WinForge.Native.sln /restore /m /p:Configuration=Debug /p:Platform=x64` (the local driver discovers the installed MSVC toolset).
- Native route/unit evidence: `tests\native\WinForge.Core.Tests\bin\x64\Debug\WinForge.Core.Tests.exe`; native live shell evidence: `powershell -ExecutionPolicy Bypass -File eng\native\Invoke-NativeShellSmoke.ps1`.
- Native launch: `powershell -ExecutionPolicy Bypass -File .agents\skills\run-winforge\driver.ps1 -Native -Page <id> -NoCapture`. See [Native C++ Rewrite](Native-Cpp-Rewrite.md); a native route is not a port-complete claim.
- Exe: `bin\x64\Debug\net11.0-windows10.0.26100.0\win-x64\WinForge.exe`. Self-contained (`WindowsAppSDKSelfContained=true`, `WindowsPackageType=None`).
- Launch a page directly: `WinForge.exe --page <id>` (see `docs/CLI.md` for every id) · master search: `--page search:<q>` · headless docs: `--export-docs docs\features`.
- Window: **windowed by default (~82% screen), F11 toggles full screen** (saved). Closing **hides to the system tray** (right-click tray → Quit) so the background clipboard monitor keeps running.
- Theme: Settings → App theme (Light/Dark/System), saved. Language: Settings → Language (Bilingual/Cantonese/English).
- CI: `.github/workflows/release.yml` builds + publishes a **new GitHub Release on every push** (`v1.0.<run#>`) with a portable zip + Inno Setup installer (`installer/WinForge.iss`).

## Suite modules · 套件模組

| Module · 模組 | `--page` | Page | Service(s) | Engine / mechanism | Status |
|---|---|---|---|---|---|
| Dashboard · 概覽 | `dashboard` | `Pages/DashboardPage` | — | system summary, tiles, search | ✅ |
| Git & GitHub · Git 與 GitHub | `git` | `Pages/GitHubModule` | `Services/GitService` | git + gh CLI, chunked uploader (111 ops) | ✅ |
| Archives · 壓縮檔 | `archives` | `Pages/ArchivesModule` | `Services/ArchiveService` | 7-Zip (create/extract/test/bench, 100 ops) | ✅ |
| Media · 媒體 | `media` | `Pages/MediaModule` | `Services/MediaService` | ffmpeg (convert/trim/gif, 60 ops) | ✅ |
| Registry Editor · 登錄編輯器 | `registry` | `Pages/RegistryEditor` | `Services/RegistryHelper` | live registry browse/edit | ✅ |
| Services · 服務 | `services` | `Pages/ServicesModule` | `Services/ServiceManager` | CIM + *-Service; **Actions dropdown** | ✅ |
| Scheduled Tasks · 排程工作 | `tasks` | `Pages/ScheduledTasksModule` | `Services/TaskSchedulerManager` | Get-ScheduledTask; **Actions dropdown** | ✅ |
| Devices · 裝置 | `devices` | `Pages/DevicesModule` | `Services/DeviceManager` | Get-PnpDevice; enable/disable (confirm) | ✅ |
| Startup Apps · 開機程式 | `startup` | `Pages/StartupModule` | `Services/StartupManager` | Run keys + StartupApproved + folders | ✅ |
| Batch Rename · 批次改名 | `rename` | `Pages/RenameModule` | `Services/RenameEngine` | regex/sequence file rename | ✅ |
| Bulk File Ops · 批次檔案操作 | `bulkops` | `Pages/BulkOpsModule` | `Services/BulkFileOps` | SHFileOperation (copy/move/recycle) | ✅ |
| Duplicate Finder · 重複檔案搜尋 | `duplicates` | `Pages/DuplicatesModule` | `Services/DuplicateFinder` | size + hash dedupe | ✅ |
| Disk Analyser · 磁碟分析 | `disk` | `Pages/DiskAnalyzerModule` | `Services/DiskAnalyzer` | folder-size tree | ✅ |
| Drives · 磁碟機 | `drives` | `Pages/DrivesModule` | `Services/DriveService` | volumes, mount ISO/VHD | ✅ |
| App Uninstaller · 應用程式解除安裝 | `uninstall` | `Pages/AppUninstallerModule` | `Services/UninstallManager` | Get/Remove-AppxPackage | ✅ |
| Window Manager · 視窗管理 | `windows` | `Pages/WindowManagerModule` | `Services/WindowManager` | EnumWindows + SetWindowPos (zones) | ✅ |
| Keyboard Remapper · 鍵盤重新對應 | `keyboard` | `Pages/KeyboardModule` | `Services/KeyboardRemapper` | Scancode Map registry | ✅ |
| Hosts Editor · hosts 編輯器 | `hosts` | `Pages/HostsEditorModule` | `Services/HostsService` | hosts file IO + flush DNS | ✅ |
| Mouse & Pointer · 滑鼠與指標 | `mouse` | `Pages/MouseModule` | `Services/MouseSettings` | SystemParametersInfo (live) | ✅ |
| Screen Recorder · 螢幕錄影 | `recorder` | `Pages/ScreenRecorderModule` | `Services/ScreenRecorder` | ffmpeg gdigrab (whole desktop) | ✅ |
| System Monitor · 系統監察 | `monitor` | `Pages/SystemMonitorModule` | `Services/SystemMonitor` | GetSystemTimes/NetworkInterface; per-proc priority/affinity/EcoQoS | ✅ |
| Connections · 連線 | `connections` | `Pages/ConnectionsModule` | `Services/ConnectionsService` | iphlpapi (TCPView-style) | ✅ |
| Event Viewer · 事件檢視器 | `events` | `Pages/EventViewerModule` | `Services/EventLogService` | Get-WinEvent | ✅ |
| Volume Mixer · 音量混合器 | `mixer` | `Pages/VolumeMixerModule` | `Services/AudioMixer` | Core Audio (WASAPI) COM | ✅ |
| Context Menu · 右鍵選單 | `contextmenu` | `Pages/ContextMenuModule` | `Services/ContextMenuService` | HKCU shell verbs | ✅ |
| Awake · 保持喚醒 | `awake` | `Pages/AwakeModule` | `Services/AwakeService` | SetThreadExecutionState | ✅ |
| Color Picker · 螢幕取色 | `colorpicker` | `Pages/ColorPickerModule` | `Services/ColorPickService` | WH_MOUSE_LL hook + GetPixel | ✅ |
| Environment Variables · 環境變數 | `envvars` | `Pages/EnvVarsModule` | `Services/EnvVarService` | Environment.*Variable; per-entry PATH editor | ✅ |
| Clipboard · 剪貼簿 | `clipboard` | `Pages/ClipboardModule` | `Services/ClipboardService` | Clipboard.ContentChanged + **local git repo** + opencode commit msgs | ✅ |
| Package Manager · 套件管理 | `packages` | `Pages/PackageManagerModule` | `Services/PackageService` | winget (UniGetUI-style) + `AutoInstall` | ✅ |
| Android (ADB) · Android（ADB） | `adb` | `Pages/AndroidAdbModule` | `Services/AdbService` | adb (devices/APK/shell/logcat/screencap/reboot); auto-installs adb | ✅ |
| VPN & Mesh · VPN 與網狀網 | `vpn` | `Pages/VpnMeshModule` | `Services/NordVpnService`, `Services/TailscaleService` | NordVPN.exe + tailscale CLIs; auto-install | ✅ |
| Search results · 搜尋結果 | `search:<q>` | `Pages/SearchResultsPage` | `Services/ModuleRegistry` | master search — pages + live tweak toggles | ✅ |
| Settings · 設定 | `settings` | `Pages/SettingsPage` | `Services/SettingsStore` | language, theme, import/export | ✅ |
| About · 關於 | `about` | `Pages/AboutPage` | — | about/version | ✅ |

**System tray** — `Services/TrayService` (raw Shell_NotifyIcon + message-only window): keeps WinForge running when the window is closed; tray menu Open / Quit.

## Tweak catalog · 調校目錄
Data-driven: `Catalog/*Tweaks.cs` files build `TweakDefinition`s via the `Services/Tweak` factory (RegToggle/CustomToggle/RegChoice/Action/Shell/Powershell/Cmd/Info). `Catalog/TweakCatalog.cs` aggregates them per `Catalog/Categories.cs` category. Rendered by `Controls/TweakCard` (shows EN+粵語, reads live state via `GetIsOn`, full scrollable monospace output for actions). ~1140 tweaks/ops across 22 categories incl. **Winaero Tweaks** (45) and **Debloat & Annoyances**.

## Key architecture · 主要架構
- **Language modes:** `Services/Loc` — `Loc.I.Pick(en, zh)` returns bilingual text in Bilingual mode, or a single string in Cantonese/English modes.
- **Registry:** `Services/RegistryHelper` (RegRoot HKCU/HKLM/HKCR/HKU, Get/Set/Delete/SubKeys).
- **Shell:** `Services/ShellRunner` (Run/RunCmd/RunPowershell/CapturePowershell).
- **Nav:** `MainWindow` NavigationView with collapsible groups; `Navigator.GoToModule/GoToCategory` resolve tags **recursively**; `MapType` fallback. `Services/ModuleRegistry` powers page-search.
- **Touchless install:** `PackageService.AutoInstall(id)` = winget silent + refresh process PATH; wired into engine-bars (adb, NordVPN, Tailscale).
- **Docs:** `Services/DocsExporter` writes per-feature Markdown into **per-module subfolders** under `docs/features/`.

## Pending queue · 待辦 (see `docs/ROADMAP.md`)
UX pass (remaining: Devices Actions dropdown, parse tabular command output into tables) → Smart App Uninstaller (icons + size + deep clean) → auto-install everywhere + kill remaining redirects → more 7z/zip features → custom-program runner → full export/import incl. the clipboard git repo → Docker/GitHub config sync → app logo → UniGetUI source port. The 5-min loop builds these one tested+pushed module at a time.

_Auto-maintained alongside the WinForge build loop · 由 WinForge 建置迴圈一齊維護_
