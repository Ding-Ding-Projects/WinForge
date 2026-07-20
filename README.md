# WinForge · 視窗鑄造

WinForge is the canonical **.NET 11 / WinUI 3** Windows 11 control center. It combines 319 registered in-app entries, a large Windows-tweak catalog, and a hyper-realistic pressurized-water-reactor simulator in one self-contained x64 desktop app.

WinForge 係正式嘅 **.NET 11 / WinUI 3** Windows 11 控制中心。佢將 319 個已登記 app 內項目、大型 Windows 調校目錄，同超寫實壓水堆模擬器放喺同一個自包含 x64 桌面 app。

`WinUI 3` · `.NET 11` · `English / 粵語 / bilingual` · `Windows 11 x64`

> The experimental C++20/C++/WinRT port has moved to [codingmachineedge/WinForge-Native](https://github.com/codingmachineedge/WinForge-Native). It is developed and released independently and does not replace this application. · 實驗性 C++20/C++/WinRT 移植版已搬去 [codingmachineedge/WinForge-Native](https://github.com/codingmachineedge/WinForge-Native)，會獨立開發同發佈，唔會取代呢個正式 app。

## Highlights · 重點

- **One control center · 一個控制中心** — system tweaks, files and disks, media, networking, package management, developer tools, accessibility utilities, virtualization, security, and gaming surfaces live in one app.
- **Real integrations · 真正整合** — modules use Windows APIs and tools such as `git`, `gh`, `winget`, `ffmpeg`, 7-Zip, Docker, cloudflared, and WebView2; safety-sensitive actions remain explicit and reviewable.
- **Three persisted language modes · 三種持久語言模式** — English, playful Hong Kong-style Cantonese, and compact bilingual mode.
- **Self-contained delivery · 自包含發佈** — the managed application and Windows App SDK runtime ship together; a separate desktop runtime install is not required.
- **Flagship reactor · 旗艦反應堆** — a PWR control-room simulator with point kinetics, thermal hydraulics, turbine and electrical systems, protection logic, fuel and waste services, water treatment, and opt-in external integrations.

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

Every registered deep link uses `WinForge.exe --page <alias>`. Examples include `dashboard`, `reactor`, `reactorsettings`, `monitor`, `docker`, `torrent`, `proxmox`, `ocr`, `keepass`, and `hexeditor`.

## Verification · 驗證

Run the reactor and dependent-service harness after reactor work:

改過反應堆或者依賴服務後，要跑以下 harness：

```powershell
Remove-Item Env:DOTNET_ROOT -ErrorAction SilentlyContinue
dotnet run --project tests\ReactorSim.Tests -c Debug
```

The current contract is **63/63** scenarios, and the harness returns nonzero if any scenario fails or throws. Use `-- --verify-exit-code-contract` for its fast exit-code self-test.

Visual changes require a fresh inspected screenshot for every changed page. If graphics capture is unavailable, record the exact blocker and keep functional, accessibility, and visual evidence separate.

視覺改動要為每個改過嘅頁面提供最新、已檢視截圖。如果環境擷取唔到畫面，要記低確實阻礙，並將功能、無障礙同視覺證據分開。

## Reactor safety · 反應堆安全

- Meltdown-to-real-PC-shutdown is **off by default** and remains abortable when explicitly armed. · 熔毀觸發真實電腦關機預設係**關閉**，明確啟用後仍然可以中止。
- A new reactor starts held in MODE 5 cold shutdown; the operator must start it. · 新反應堆會保持 MODE 5 冷停堆，要由操作員啟動。
- Waste writes enforce a disk free-space floor and a default 50 GB cap. · 廢料寫入會保留磁碟可用空間底線，預設上限係 50 GB。
- Real-world side effects are opt-in and reversible. · 現實世界副作用全部要明確選擇加入，而且可以還原。

Current reactor evidence and operating procedures are in the [test report](docs/wiki/Reactor-Test-Report.md) and [operating manual](docs/wiki/Nuclear-Reactor-Operating-Manual.md).

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
