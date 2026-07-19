# WinForge — Handoff reference (per feature) · 交接參考（逐項功能）

A complete map of every module/feature: what it does, how to open it, the page + service files, and the
**real engine it wraps**. WinForge is a bilingual (English + 粵語) WinUI 3 / .NET 11 suite for Windows 11.
**No redirects** — every shipping feature runs in-app and wraps a real engine/API. A genuine C++20/C++/WinRT rewrite now lives beside this managed oracle; its foundation is routable and verified, but feature parity remains evidence-gated and is not yet claimed.

完整列出每個模組／功能：做乜、點開、頁面同服務檔案、同埋包住嘅真實引擎。WinForge 係雙語（英文 + 粵語）
嘅 WinUI 3 / .NET 11 Windows 11 套件。**唔跳轉** — 每個發佈中功能都喺 app 內運行、包住真實引擎／API。而家亦有真正 C++20/C++/WinRT 重寫同受控 oracle 並存；基礎 shell 已可路由同驗證，但功能對等仍要逐項證據把關，未聲稱完成。

## Build / run / release · 建置／運行／發佈
- Build: `dotnet build -c Debug -p:Platform=x64` (must stay 0 errors).
- Native build: `msbuild WinForge.Native.sln /restore /m /p:Configuration=Debug /p:Platform=x64` (the local driver discovers the installed MSVC toolset).
- Native route/unit evidence: `tests\native\WinForge.Core.Tests\bin\x64\Debug\WinForge.Core.Tests.exe`; native live shell evidence: `powershell -ExecutionPolicy Bypass -File eng\native\Invoke-NativeShellSmoke.ps1`.
- Native launch: `powershell -ExecutionPolicy Bypass -File .agents\skills\run-winforge\driver.ps1 -Native -Page <id> -NoCapture`. See [`docs/Native-Cpp-Rewrite.md`](Native-Cpp-Rewrite.md) and [`docs/cpp-port-parity.json`](cpp-port-parity.json); a native route is not a port-complete claim.
- Native Namespaced UUID: `uuid5`/`uuidv5`/`module.uuidv5` now use standard-C++ `UuidV5` plus a C++/WinRT renderer for RFC 4122 network-order v3/v5 output, managed D/N/B/P/X custom namespaces, UTF-16 replacement, managed U+180E whitespace parity, bulk CRLF output, route reset, localization, and explicit-only clipboard behavior. Fresh Debug/Release builds have 0 errors, core is **773/773**, focused UIA is **20/20**, catalog parity is 346+5, and the installer contract passes. LowLevel MCP is unavailable; the fresh driver rejected a blank fallback, so the row remains `in-progress` / `capture-blocked` pending controlled integration aggregate evidence. · 原生具名空間 UUID：`uuid5`／`uuidv5`／`module.uuidv5` 而家用標準 C++ `UuidV5` 同 C++/WinRT renderer；保留 RFC 4122 network-order v3/v5、managed D/N/B/P/X 自訂 namespace、UTF-16 replacement、managed U+180E 空白對等、bulk CRLF、route reset、雙語同只限明確剪貼簿操作。最新 Debug／Release build 0 errors、core **773/773**、專項 UIA **20/20**、catalog parity 346+5 同 installer contract 已通過；今次冇可呼叫 LowLevel MCP，driver 拒絕空白 fallback，所以受控整合 aggregate 證據前保持 `in-progress`／`capture-blocked`。
- Branch-local full native shell evidence for Namespaced UUID is **437/437 (437 passed, 0 failed)** with no UUID-worktree app process left; after controlled rebase/integration, rerun it before treating the aggregate as main evidence. · 具名空間 UUID branch-local 完整 native shell 係 **437/437（437 passed、0 failed）**，冇殘留 UUID worktree app process；受控 rebase／整合後要重跑先可以當 main aggregate 證據。
- Native Reference Text: `nato`/`phonetic`, `boxtext`, and `entities`/`htmlentities` now use the standard-C++ `ReferenceText` core plus C++/WinRT renderers. Fresh Debug/Release builds have 0 errors, core is **759/759**, focused UIA is **29/29**, catalog parity covers 346+5, and full native shell smoke is **417/417**. LowLevel MCP is unavailable in this session; the fresh `htmlentities` driver rejected a blank fallback, so the three routes remain `in-progress` / `capture-blocked`. · 原生參考文字：`nato`／`phonetic`、`boxtext` 同 `entities`／`htmlentities` 而家用標準 C++ `ReferenceText` core 同 C++/WinRT renderer。最新 Debug／Release build 0 errors、core **759/759**、專項 UIA **29/29**、catalog parity 346+5、完整 native shell **417/417**；今次冇可呼叫 LowLevel MCP，`htmlentities` driver 拒絕空白 fallback，所以三條 route 保持 `in-progress`／`capture-blocked`。
- Exe: `bin\x64\Debug\net11.0-windows10.0.26100.0\win-x64\WinForge.exe`. Self-contained (`WindowsAppSDKSelfContained=true`, `WindowsPackageType=None`).
- Launch a page directly: `WinForge.exe --page <id>` (see `docs/CLI.md` for every id) · master search: `--page search:<q>` · headless docs: `--export-docs docs\features`.
- Window: **windowed by default (~82% screen), F11 toggles full screen** (saved). Closing **hides to the system tray** (right-click tray → Quit) so the background clipboard monitor keeps running.
- Theme: Settings → App theme (Light/Dark/System), saved. Language: Settings → Language (Bilingual/Cantonese/English).
- Native CI: `.github/workflows/native-release.yml` is the sole publisher and releases exactly the portable ZIP plus `WinForge-Native-Setup.exe`. Branch pushes publish native-only prereleases. Fresh current-main promotion explicitly applies Latest + stable + non-draft and verifies exact SHA, stable native metadata, and exactly two assets through `/releases/latest`. Every release run enforces the invariant; noncurrent runs restore a verified current-main stable native candidate when GitHub points to managed or invalid Latest. Pull requests validate without publishing.
- Current hosted enforcement (superseding the historical pending wording below): `1732af0b458454f144d1ac32be222b9e9015e5c5` passed [29694567950](https://github.com/codingmachineedge/WinForge/actions/runs/29694567950), proving staged site-data no-op handling and native-only prerelease `native-v1.0.55`. Main `03b1e66f21b07c28fe2bd0b30ef60fe4af134e59` passed [29695569334](https://github.com/codingmachineedge/WinForge/actions/runs/29695569334) and published stable, non-draft [native-v1.0.57](https://github.com/codingmachineedge/WinForge/releases/tag/native-v1.0.57); `/releases/latest` returned that exact SHA and exactly `WinForge-Native-Setup.exe` plus `WinForge-native-x64-1.0.57.zip`, with no managed app. Site-data [29695569335](https://github.com/codingmachineedge/WinForge/actions/runs/29695569335) staged first and logged `No change to design/winforge-data.js.` · 目前 hosted 證明已取代下面嘅歷史 pending 字樣：`1732af0b` 證明 site-data no-op，main `03b1e66f`／run 29695569334 發佈 stable `native-v1.0.57`；`/releases/latest` 準確只有原生 setup 同 ZIP，冇 managed app。
- Completed text-analysis release proof: feature `fc2b76e52171e4f81ab1d15f9fb1da5818791171` passed branch run `29673079883` and published exact-SHA prerelease `native-v1.0.43`; merge `f7a9eec44aeffdf829f5c07f5eeb364f08a7677f` passed `main` run `29673310778` and published stable exact-SHA `native-v1.0.44`. Both releases have exactly setup + portable ZIP. Independent download verification matched digests and found 292 ZIP entries, 48 PEs, zero CLR/apphost/forbidden managed/build artifacts, and AMD64 PE32+ `WinForge.exe`.
- Latest-channel correction: `gh release create --latest` left `/releases/latest` at historical managed `v1.0.256`. A later bare `gh release edit native-v1.0.44 --latest` was insufficient because release 44 was observed as `prerelease=true` at that edit timestamp, so Latest fell back to managed. Explicit `gh release edit native-v1.0.44 --latest --prerelease=false --draft=false` restored stable/non-draft release 44 and exact `/releases/latest` at `f7a9eec44aeffdf829f5c07f5eeb364f08a7677f` with two native assets.
- Historical corrective branch proof: commit `53da21446a5c2cded97e1387f3e36f557770a3c5` passed run `29673965179` and published `native-v1.0.47` with official Node 24 actions and exactly two native assets, but its then-current noncurrent check did not prove the invariant. That historical limitation is superseded by `1732af0b` and main `03b1e66f` / run `29695569334` above; automatic enforcement is now hosted-proven.

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
| App Uninstaller / 原生解除安裝器 | uninstall | native C++/WinRT module.uninstall; managed page remains oracle | WinForge.Core/AppUninstaller + Windows PackageManager | current-user Store/UWP inventory; local literal/Regex cache search; review/Confirm; normal-integrity gate; no local-data deletion | in progress: LowLevel headless UI and visual capture blocked |
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
| Package Manager · 套件管理 | `packages` | `Pages/PackageManagerModule`, `Pages/PackageDetailsDialog`, `Pages/PackageSettingsDialog`, `Pages/BundleWorkspaceDialog` | `Services/PackageManagers`, `Services/PackageOperationCoordinator`, `Services/PackageOperations`, `Services/InstallOptions`, `Services/BundleService`, `Services/PackageUpdateScheduler`, `Services/SourceManager` | Native WinUI workspace over 11 manager CLIs; 9 views; shared queue/history/cancel/retry; saved options; secure bundles/scheduler/sources. Pinned `ThirdParty/UniGetUI` is provenance only, not runtime. | ✅ |
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
- **Package operations:** all package row, batch, bundle and scheduled install/update/uninstall paths use `PackageOperationCoordinator`, which applies saved global/per-package `InstallOptions`, bounded concurrency, duplicate suppression, cancellation, notifications and history. `PackageService.AutoInstall(id)` remains the winget bootstrap path used by other modules' engine bars.
- **UniGetUI provenance:** the complete tracked upstream tree is pinned under `ThirdParty/UniGetUI` at `21116375c8299d1db38a3c3b4c2eb7e18bc97c4e` and excluded from build/publish inputs. UniGetUI carries its MIT license; bundled third-party material retains separate notices. Its upstream UI/framework, IPC and telemetry are not compiled, embedded or launched.
- **Docs:** `Services/DocsExporter` writes per-feature Markdown into **per-module subfolders** under `docs/features/`.

## Pending queue · 待辦 (see `docs/ROADMAP.md`)
UX pass (remaining: parse tabular command output into tables) → more 7z/zip features → custom-program runner → full export/import incl. the clipboard git repo → Docker/GitHub config sync → app logo → continued native package-manager parity review against the pinned UniGetUI provenance snapshot. The build loop delivers these as tested WinForge features without compiling or launching the upstream application.

_Auto-maintained alongside the WinForge build loop · 由 WinForge 建置迴圈一齊維護_
