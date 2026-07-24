# WinForge · 視窗鑄造

WinForge is the canonical **.NET 11 / WinUI 3** Windows 11 control center. It combines 322 registered in-app entries, a large Windows-tweak catalog, and a hyper-realistic pressurized-water-reactor simulator in one self-contained x64 desktop app.

WinForge 係正式嘅 **.NET 11 / WinUI 3** Windows 11 控制中心。佢將 322 個已登記 app 內項目、大型 Windows 調校目錄，同超寫實壓水堆模擬器放喺同一個自包含 x64 桌面 app。

`WinUI 3` · `.NET 11` · `English / 粵語 / bilingual` · `Windows 11 x64`

> The experimental C++20/C++/WinRT port has moved to [codingmachineedge/WinForge-Native](https://github.com/codingmachineedge/WinForge-Native). It is developed and released independently and does not replace this application. · 實驗性 C++20/C++/WinRT 移植版已搬去 [codingmachineedge/WinForge-Native](https://github.com/codingmachineedge/WinForge-Native)，會獨立開發同發佈，唔會取代呢個正式 app。

## Highlights · 重點

- **One control center · 一個控制中心** — system tweaks, files and disks, media, networking, package management, developer tools, accessibility utilities, virtualization, security, and gaming surfaces live in one app.
- **Real integrations · 真正整合** — modules use Windows APIs and tools such as `git`, `gh`, `winget`, `ffmpeg`, 7-Zip, Docker, cloudflared, and WebView2; safety-sensitive actions remain explicit and reviewable.
- **Managed AWS console · 受管理 AWS 主控台** — in-process AWS SDK workspaces provide account/Region isolation, cross-service discovery, native S3 controls, and guarded EC2 instance lifecycle management; the CLI remains an optional long-tail workbench. · 程式內 AWS SDK 工作區提供帳戶／Region 隔離、跨服務探索、原生 S3 控制，同受保護 EC2 執行個體生命週期管理；CLI 只係選用長尾工作台。
- **Dew-compatible local history · Dew 相容本機歷史** — a native, bilingual workspace snapshots a file or folder into adjacent Git history, reviews changes, restores rollback-safely, and exports password/header-encrypted 7z archives without launching the upstream app or placing secrets on a command line. · 原生雙語工作區會將檔案或資料夾影成旁置 Git 歷史、檢視變更、安全 rollback 還原，同匯出密碼及檔名加密 7z；唔會啟動上游 app，亦唔會將秘密放入命令列。
- **Three persisted language modes · 三種持久語言模式** — English, playful Hong Kong-style Cantonese, and compact bilingual mode.
- **Self-contained delivery · 自包含發佈** — the managed application and Windows App SDK runtime ship together; a separate desktop runtime install is not required.
- **Flagship reactor · 旗艦反應堆** — a PWR control-room simulator with point kinetics, thermal hydraulics, turbine and electrical systems, protection logic, fuel and waste services, water treatment, and opt-in external integrations.
- **Reactor-powered industrial loads · 反應堆工業負載** — a green-ammonia Haber–Bosch plant and strict-priority grid load-shed dispatcher consume the live simulated bus, fail dark, and preserve reactor safety boundaries. · 綠氨哈柏法工廠同嚴格優先級電網卸載調度器會用即時模擬母線；冇電就停，而且唔會越過反應堆安全界線。

The complete bilingual module and button reference starts at [the wiki home](docs/wiki/Home.md). Generated feature pages live under `docs/wiki/features/`, while focused architecture, operating, and verification records live under `docs/` and `docs/wiki/`.

完整雙語模組同按鈕參考由 [wiki 首頁](docs/wiki/Home.md)開始。自動產生嘅功能頁喺 `docs/wiki/features/`，架構、操作同驗證記錄就喺 `docs/` 同 `docs/wiki/`。

## Build · 建置

Requirements: Windows 11, the .NET 11 SDK, and the WinUI/Windows App SDK build workload.

需求：Windows 11、.NET 11 SDK，同 WinUI／Windows App SDK 建置 workload。

```powershell
dotnet build WinForge.sln -c Debug -p:Platform=x64
```

The compile gate is exit code 0 with zero errors; warning counts are not a fixed contract.

編譯 gate 係 exit code 0 同零 errors；warning 數量唔係固定合約。

## Run · 執行

A plain Debug build is framework-dependent in this workspace. Publish the app self-contained before launching it:

呢個 workspace 嘅普通 Debug build 依賴 framework。啟動前要先做自包含 publish：

```powershell
dotnet publish WinForge.csproj -c Debug -r win-x64 --self-contained true `
  -p:Platform=x64 -p:WindowsAppSDKSelfContained=true
```

For build-if-needed, deep-link launch, process-owned cleanup, and an optional screenshot, use the repository driver:

想自動按需要建置、直接開指定頁、只清理自己開嘅 process，同選擇性截圖，可以用 repo driver：

```powershell
powershell -ExecutionPolicy Bypass -File .agents\skills\run-winforge\driver.ps1 `
  -Page dashboard -Out winforge-dashboard.png

# Launch-only smoke check · 只做啟動 smoke check
powershell -ExecutionPolicy Bypass -File .agents\skills\run-winforge\driver.ps1 `
  -Page reactor -NoCapture
```

Every registered deep link uses `WinForge.exe --page <alias>`. Examples include `dashboard`, `reactor`, `ammonia`, `loadshed`, `reactorsettings`, `monitor`, `docker`, `torrent`, `proxmox`, `ocr`, `keepass`, and `hexeditor`.

## Verification · 驗證

Run the reactor and dependent-service harness after reactor work:

改過反應堆或者依賴服務後，要跑以下 harness：

```powershell
Remove-Item Env:DOTNET_ROOT -ErrorAction SilentlyContinue
dotnet run --project tests\ReactorSim.Tests -c Debug
```

The current contract is **65/65** scenarios, and the harness returns nonzero if any scenario fails or throws. Use `-- --verify-exit-code-contract` for its fast exit-code self-test.

Run the dedicated Dew Encryption compatibility, path-safety, restore, watcher, and archive suite after Dew work:

改過 Dew Encryption 後，要跑相容性、路徑安全、還原、watcher 同 archive 專用測試：

```powershell
dotnet run --project tests\DewEncryption.Tests -c Debug
```

The current Dew contract is **23/23** tests, including writable and extracted read-only historical-deletion restores. Its watcher case uses a named 45-second loaded-host commit budget and proves rapid writes debounce into one commit containing the final value. · 目前 Dew 合約係 **23/23**，包括可寫同 extracted read-only 歷史刪除還原；watcher case 用具名 45 秒 loaded-host commit budget，並證明快速連續寫入只會 debounce 成一個包含最終值嘅 commit。

Visual changes require a fresh inspected screenshot for every changed page. If graphics capture is unavailable, record the exact blocker and keep functional, accessibility, and visual evidence separate.

視覺改動要為每個改過嘅頁面提供最新、已檢視截圖。如果環境擷取唔到畫面，要記低確實阻礙，並將功能、無障礙同視覺證據分開。

## Reactor safety · 反應堆安全

- Meltdown-to-real-PC-shutdown is **off by default** and remains abortable when explicitly armed. · 熔毀觸發真實電腦關機預設係**關閉**，明確啟用後仍然可以中止。
- A new reactor starts held in MODE 5 cold shutdown; the operator must start it. · 新反應堆會保持 MODE 5 冷停堆，要由操作員啟動。
- Waste writes enforce a disk free-space floor and a default 50 GB cap. · 廢料寫入會保留磁碟可用空間底線，預設上限係 50 GB。
- Real-world side effects are opt-in and reversible. · 現實世界副作用全部要明確選擇加入，而且可以還原。

Current reactor evidence and operating procedures are in the [test report](docs/wiki/Reactor-Test-Report.md) and [operating manual](docs/wiki/Nuclear-Reactor-Operating-Manual.md).

## Command Palette extensions · 指令面板擴充套件

WinForge supports user-managed declarative Command Palette packs. New packs are disabled by default and may open a registered module, open an HTTP(S) URL, or copy bounded text. A pack can also opt into a fully qualified local-drive, SHA-256-pinned `.exe` host for richer actions and structured native pages; UNC, network-share, and device paths are rejected. WinForge reloads current pack enablement, re-verifies and leases the executable through process creation, refuses hosts while elevated, and accepts only a bounded JSON-lines response surface. The explicitly trusted executable is process-isolated, **not sandboxed**.

WinForge 支援由用戶管理嘅宣告式指令面板套件。新套件預設停用，只可以開啟已註冊模組、HTTP(S) 網址，或者複製有限長度文字。套件亦可以明確選用本機磁碟完整路徑、SHA-256 釘選嘅 `.exe` 主機；UNC、網絡分享同裝置路徑會拒絕。WinForge 每次操作都會重新讀取啟用狀態、重新驗證並鎖住可執行檔直到建立程序、提升權限時拒絕主機，而且只接受有限 JSON-lines 回應介面。用戶明確信任嘅可執行檔只有程序隔離，**唔係沙箱**。

See the bilingual [extension-pack guide](docs/wiki/Command-Palette-Extensions.md) and [host protocol](docs/wiki/Command-Palette-Extension-Protocol.md). · 詳情請睇雙語[擴充套件指南](docs/wiki/Command-Palette-Extensions.md)同[主機協定](docs/wiki/Command-Palette-Extension-Protocol.md)。

## Documentation · 文件

- [Wiki home · Wiki 首頁](docs/wiki/Home.md)
- [Developer guide · 開發者指南](docs/wiki/Developer.md)
- [CLI reference · CLI 參考](docs/CLI.md)
- [Reactor hub · 反應堆中心](docs/wiki/Reactor-Hub.md)
- [Native-port relocation · 原生移植版搬遷](docs/Native-Cpp-Rewrite.md)
- [Roadmap · 路線圖](ROADMAP.md)
- [Current handoff · 最新交接](handoff-summary.md)

## License · 授權

Released under the [MIT License](LICENSE), as-is and without warranty. · 以 [MIT License](LICENSE) 按現狀發佈，不附任何保證。
