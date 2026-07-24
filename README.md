# WinForge · 視窗鑄造

WinForge is the canonical **.NET 11 / WinUI 3** Windows 11 control center. It combines 322 registered in-app entries, a large Windows-tweak catalog, and a hyper-realistic pressurized-water-reactor simulator in one self-contained x64 desktop app.

WinForge 係正式嘅 **.NET 11 / WinUI 3** Windows 11 控制中心。佢將 322 個已登記 app 內項目、大型 Windows 調校目錄，同超寫實壓水堆模擬器放喺同一個自包含 x64 桌面 app。

`WinUI 3` · `.NET 11` · `English / 粵語 / bilingual` · `Windows 11 x64`

> The experimental C++20/C++/WinRT port has moved to [codingmachineedge/WinForge-Native](https://github.com/codingmachineedge/WinForge-Native). It is developed and released independently and does not replace this application. · 實驗性 C++20/C++/WinRT 移植版已搬去 [codingmachineedge/WinForge-Native](https://github.com/codingmachineedge/WinForge-Native)，會獨立開發同發佈，唔會取代呢個正式 app。

## Highlights · 重點

- **One control center · 一個控制中心** — system tweaks, files and disks, media, networking, package management, developer tools, accessibility utilities, virtualization, security, and gaming surfaces live in one app.
- **Real integrations · 真正整合** — modules use Windows APIs and tools such as `git`, `gh`, `winget`, `ffmpeg`, 7-Zip, Docker, cloudflared, and WebView2; safety-sensitive actions remain explicit and reviewable.
- **Native audio routing · 原生音訊路由** — the in-app mixer uses Core Audio for device, master, and per-app volume controls; stream-routing failures degrade safely, and returning an app to the system default is explicit and bilingual. · App 內混音器用 Core Audio 控制裝置、主音量同逐個 app 音量；串流路由失敗會安全降級，將 app 還原到系統預設亦有清楚雙語提示。
- **Auditable package management · 可審核套件管理** — the in-app [Package Manager](docs/wiki/Package-Manager.md) covers 11 engines and nine review-first views with one source-aware queue, secure portable bundles and sources, fail-aware atomic saves, credential-free proxy command construction, validated vcpkg triplets, and narrow-safe bilingual controls. · App 內[套件管理器](docs/wiki/Package-Manager.md)涵蓋 11 個引擎同九個先檢視後執行畫面，附共用來源-aware queue、安全可攜清單／來源、如實回報失敗嘅原子儲存、唔會將認證放入指令嘅 proxy 設定、已驗證 vcpkg triplet，同窄畫面安全雙語控制。
- **Managed AWS console · 受管理 AWS 主控台** — in-process AWS SDK workspaces provide account/Region isolation, cross-service discovery, native S3 controls, and guarded EC2 instance lifecycle management; the CLI remains an optional long-tail workbench. · 程式內 AWS SDK 工作區提供帳戶／Region 隔離、跨服務探索、原生 S3 控制，同受保護 EC2 執行個體生命週期管理；CLI 只係選用長尾工作台。
- **Dew-compatible local history · Dew 相容本機歷史** — a native, bilingual workspace snapshots a file or folder into adjacent Git history, reviews changes, restores rollback-safely, and exports password/header-encrypted 7z archives without launching the upstream app or placing secrets on a command line. · 原生雙語工作區會將檔案或資料夾影成旁置 Git 歷史、檢視變更、安全 rollback 還原，同匯出密碼及檔名加密 7z；唔會啟動上游 app，亦唔會將秘密放入命令列。
- **Three persisted language modes · 三種持久語言模式** — English, playful Hong Kong-style Cantonese, and compact bilingual mode.
- **Independent funny levels · 英粵分開搞笑等級** — persisted 1–5 tone controls for English and Cantonese provide a live safe-copy preview; errors, security, destructive actions, accessibility wording, and other operational text stay exact at every level. · 英文同粵語各自有持久化 1–5 級語氣控制同安全文案即時預覽；錯誤、安全、破壞性操作、無障礙同其他操作文字喺任何級別都保持準確。
- **Bounded guided regex builder · 有界引導式正則砌法** — the managed .NET 11 tester constructs literals, character classes, anchors, groups, alternation, and quantifiers; raw editing, flags, samples, live matches/captures, replacement preview, and explicit copy remain local, size-bounded, and timeout-protected. · 正式 .NET 11 測試器可引導砌字面文字、字元類、錨點、群組、二選一同量詞；原始編輯、旗標、sample、即時配對／擷取、替換預覽同明確複製全部只喺本機、有大小上限兼有超時保護。
- **Self-contained delivery · 自包含發佈** — the managed application and Windows App SDK runtime ship together; a separate desktop runtime install is not required.
- **Reliable whole-desktop recording · 可靠全桌面錄影** — Screen Recorder bulk-drains ffmpeg diagnostics, so heavy progress output cannot consume the bounded graceful-save window; forced or unconfirmed stops remain truthful failures. · 螢幕錄影會整批排走 ffmpeg 診斷輸出，繁忙進度唔會食晒有時限嘅正常儲存時間；強制或未確認停止仍然會如實報失敗。
- **Complete guided Media studio · 完整引導式 Media 工作台** — eleven production ffmpeg/ffprobe workflows add measured EBU R128, silence cleanup, two-pass stabilization, black-bar crop, lossless concat, hardware-probed NVENC, target-size encoding, subtitles, chapter splitting, HEIC/JXL batches, and metadata privacy. Every path uses argument vectors, staged outputs, bounded batches, cancellation, and owned scratch cleanup. · 新增 11 個正式 ffmpeg／ffprobe 工作流程，涵蓋 EBU R128、靜音、防震、黑邊、無損合併、NVENC、目標容量、字幕、章節、HEIC／JXL 同 metadata 私隱；全部用參數清單、暫存輸出、有界批次、取消同自家 scratch 清理。
- **Review-first developer, HA, and archive workflows · 先審閱開發、HA 同壓縮檔流程** — parameterized ports/Node/Corepack/Defender/TCP/cache controls, an exact short-lived Home Assistant config-check restart gate, and bounded archive filters/delete plus integrity-before-Recycle-Bin moves close eleven audited gaps with a 44-case pure harness. · 參數化 ports／Node／Corepack／Defender／TCP／快取、準確短效 HA 重啟驗證閘、有界壓縮檔篩選／刪除同完整性測試後先移回收筒，用 44 項純測試補齊十一個審核缺口。
- **Flagship reactor · 旗艦反應堆** — a PWR control-room simulator with point kinetics, thermal hydraulics, turbine and electrical systems, protection logic, fuel and waste services, water treatment, and opt-in external integrations.
- **Reactor-powered industrial loads · 反應堆工業負載** — a green-ammonia Haber–Bosch plant and strict-priority grid load-shed dispatcher consume the live simulated bus, fail dark, and preserve reactor safety boundaries. · 綠氨哈柏法工廠同嚴格優先級電網卸載調度器會用即時模擬母線；冇電就停，而且唔會越過反應堆安全界線。

The complete bilingual module and button reference starts at [the wiki home](docs/wiki/Home.md). Generated feature pages live under `docs/wiki/features/`, while focused architecture, operating, verification, and evidence-backed [roadmap reconciliation](docs/roadmap-audits/README.md) records live under `docs/` and `docs/wiki/`.

完整雙語模組同按鈕參考由 [wiki 首頁](docs/wiki/Home.md)開始。自動產生嘅功能頁喺 `docs/wiki/features/`，架構、操作、驗證同有證據嘅[路線圖對帳](docs/roadmap-audits/README.md)記錄就喺 `docs/` 同 `docs/wiki/`。

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

Screenshot runs accept only bounded PNG paths on fixed or removable local drives, request a DEBUG-only capture of WinForge's live visual tree, flush a unique partial image, and validate it before atomic promotion. Pixels are composited against the window's actual theme and encoded opaque. The driver deletes stale output when a new attempt starts and never reads raw desktop pixels, so neither an old image nor a window overlapping WinForge can masquerade as current evidence; an HWND-targeted `PrintWindow` attempt is the bounded fallback. · 截圖 run 只接受 fixed／removable 本機 drive 上有限長度嘅 PNG 路徑，要求 DEBUG-only WinForge 即時 visual tree capture，先 flush 唯一 partial 圖，再驗證同原子升格。Pixels 會按視窗實際 theme 合成並輸出不透明圖。新擷取開始時 driver 會刪除舊 output，亦永遠唔會讀取原始 desktop pixels，所以舊圖同遮住 WinForge 嘅其他視窗都唔可以扮今次證據；有限後備只會針對自家 HWND 呼叫 `PrintWindow`。

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

Run both recorder lifecycle harnesses after changing screen capture, ffmpeg process ownership, redirected streams, or Stop behavior:

改過螢幕擷取、ffmpeg process ownership、redirected stream 或 Stop 行為後，要跑兩個 recorder lifecycle harness：

```powershell
dotnet run --project tests\RecorderRegistrySafety.Tests -c Debug
dotnet run --project tests\ScreenRecorderLifecycle.Tests -c Debug
```

The process-free seam currently passes **10/10**; the deterministic self-hosted stderr fixture passes **1/1** and protects the real bulk-drain/quit/exit path without measuring `cmd.exe` loop scheduling. · Process-free seam 目前 **10/10**；deterministic self-hosted stderr fixture **1/1**，會保護真實 bulk-drain／quit／exit 流程，唔會誤測 `cmd.exe` loop 排程。

Run the Regex Builder safety harness after changing the builder, .NET regex evaluation, search-pattern contract, limits, flags, or replacement preview:

改過正則砌法、.NET regex 運算、搜尋 pattern 合約、上限、旗標或者替換預覽之後，要跑：

```powershell
dotnet run --project tests\RegexBuilder.Tests -c Debug
```

The current contract is **13/13**, covering guided tokens, syntax failures, Unicode, multiline anchors, capture groups, no-match, zero-width progress, result/size caps, adversarial timeout, and plain-text-versus-regex behavior. · 目前合約係 **13/13**，覆蓋引導 token、語法錯誤、Unicode、多行錨點、擷取群組、無配對、零寬度安全推進、結果／大小上限、對抗式超時，同純文字對 regex 行為。

Run the pure roadmap-workflow harness after changing Developer & Terminal, the Home Assistant restart gate, or the bespoke Archives create/delete workflows:

改過開發與終端機、Home Assistant 重啟安全閘，或者壓縮檔專用建立／刪除流程之後，要跑：

```powershell
dotnet run --project tests\RoadmapWorkflowCore.Tests -c Debug
```

The contract is **44/44** and does not terminate a process, alter Defender/TCP/cache state, modify a real archive, or contact Home Assistant. · 合約係 **44/44**，唔會終止程序、改 Defender／TCP／快取狀態、修改真實壓縮檔，亦唔會連去 Home Assistant。

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

## Browser Control workbench · 瀏覽器控制工作台

Browser Control now provides configurable app/kiosk URLs, real Chrome/Edge profile selection, installed-PWA launch, flags and policy pages, safe selected-profile cache cleanup, isolated proxy/throwaway/feature/debug sessions, and review-first winget install/update. All user values cross the browser boundary as separate validated arguments; remote debugging binds to loopback and isolated session directories are lifecycle-cleaned.

瀏覽器控制而家有可設定 App／Kiosk 網址、真實 Chrome／Edge 設定檔、已裝 PWA、flags／policy、安全快取清理、隔離 Proxy／用完即棄／功能／除錯 session，同先確認 winget 安裝／更新。全部用戶值都係獨立驗證參數；遠端除錯只綁 loopback，隔離資料夾亦會按生命週期清理。

See the [workbench guide](docs/wiki/Browser-Control-Workbench.md) and [feature/security reference](docs/features/browser-control/browser-workbench.md). · 詳情請睇[工作台指南](docs/wiki/Browser-Control-Workbench.md)同[功能／安全參考](docs/features/browser-control/browser-workbench.md)。

## Documentation · 文件

- [Wiki home · Wiki 首頁](docs/wiki/Home.md)
- [Developer guide · 開發者指南](docs/wiki/Developer.md)
- [CLI reference · CLI 參考](docs/CLI.md)
- [Reactor hub · 反應堆中心](docs/wiki/Reactor-Hub.md)
- [Native-port relocation · 原生移植版搬遷](docs/Native-Cpp-Rewrite.md)
- [Roadmap · 路線圖](ROADMAP.md)
- [Core roadmap capability audit · 核心路線圖功能審核](docs/audits/roadmap-core-capability-audit-2026-07-24.md)
- [Current handoff · 最新交接](handoff-summary.md)

## License · 授權

Released under the [MIT License](LICENSE), as-is and without warranty. · 以 [MIT License](LICENSE) 按現狀發佈，不附任何保證。
