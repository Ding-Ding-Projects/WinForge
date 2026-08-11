# WinForge · 視窗鑄造

WinForge is the canonical **.NET 11 / WinUI 3** Windows 11 control center. It combines 322 registered in-app entries, a large Windows-tweak catalog, and a hyper-realistic pressurized-water-reactor simulator in one self-contained x64 desktop app.

WinForge 係正式嘅 **.NET 11 / WinUI 3** Windows 11 控制中心。佢將 322 個已登記 app 內項目、大型 Windows 調校目錄，同超寫實壓水堆模擬器放喺同一個自包含 x64 桌面 app。

`WinUI 3` · `.NET 11` · `English / 粵語 / bilingual` · `Windows 11 x64`

## Build and install · 建置同安裝

Run [`build.bat`](build.bat) for a self-contained runnable x64 build, or [`build-installer.bat`](build-installer.bat) for the supported Squirrel.Windows delivery. Both scripts bootstrap the required .NET SDKs when missing, accept `/s` or `--silent`, and keep all build output under ignored directories. The installer script produces `release-artifacts/Setup.exe`, `RELEASES`, the versioned full `.nupkg`, optional delta packages, and the portable ZIP. · 用 [`build.bat`](build.bat) 整自包含可運行 x64 build，或者用 [`build-installer.bat`](build-installer.bat) 整正式 Squirrel.Windows 交付。兩個 script 都會喺缺少時自動準備需要嘅 .NET SDK，支援 `/s`／`--silent`，所有 build output 都放喺 ignored directory。installer script 會產生 `release-artifacts/Setup.exe`、`RELEASES`、有版本號嘅 full `.nupkg`、可選 delta package，同 portable ZIP。

For a release-matched local build, pass the same explicit version to both scripts, for example `build.bat /s 1.1.331` followed by `build-installer.bat /s 1.1.331`. The installer script prints every required artifact path and SHA-256; it never publishes, tags, or signs. · 要做同 release 對得上嘅本機 build，就對兩個 script 傳同一個明確版本，例如先行 `build.bat /s 1.1.331`，再行 `build-installer.bat /s 1.1.331`。Installer script 會印出所有必要資產路徑同 SHA-256；佢永遠唔會 publish、tag 或簽名。

The Squirrel.Windows installer is intentionally unsigned and may trigger an unknown-publisher or SmartScreen warning. Code signing is not used. · Squirrel.Windows installer 刻意冇簽名，可能會觸發 unknown-publisher 或 SmartScreen 警告；本項目唔使用 code signing。

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
- **Shared experience controls · 共用體驗控制** — shared live settings include the emoji-message switch, user-renamable School mode with credential-vault recovery, opt-in serialized notification narration, an offline changelog viewer with date/regex filtering and export, pinned tabs, and local TOTP pairing QR generation. The remaining universal contract is documented as unfinished work rather than implied. · 共用即時設定包括 emoji 訊息開關、可改名並用 credential vault 解鎖嘅 School mode、選擇性序列化通知旁白、附日期／regex 篩選同匯出嘅離線變更紀錄、釘選分頁，同本機 TOTP 配對 QR 產生。其餘共用合約會清楚記錄做未做，唔會扮成已完成。
- **Reviewable notification centre · 可翻查通知中心** — bounded bottom-right cards auto-dismiss information/success, retain warnings/errors, expose accessible actions, and keep a local 200-entry history; app updates and package operations share the same reliable in-app path. · 右下角有界通知卡會自動關閉資訊／成功、保留警告／錯誤、提供無障礙動作，同本機 200 項記錄；app 更新同套件操作共用同一條可靠 app 內路徑。
- **Bounded guided regex builder · 有界引導式正則砌法** — the managed .NET 11 tester constructs literals, character classes, anchors, groups, alternation, and quantifiers; the same full builder is synchronized with eight core/common search surfaces, while plain text remains the default. Raw editing, five flags, session-only samples, live matches/captures, replacement preview, and explicit copy remain local, size-bounded, and timeout-protected. · 正式 .NET 11 測試器可引導砌字面文字、字元類、錨點、群組、二選一同量詞；同一個完整版砌法已同八個核心／共用搜尋介面同步，而純文字繼續做預設。原始編輯、五旗標、只限今次工作階段 sample、即時配對／擷取、替換預覽同明確複製全部只喺本機、有大小上限兼有超時保護。
- **Self-contained delivery · 自包含發佈** — the managed application and Windows App SDK runtime ship together; a separate desktop runtime install is not required.
- **Reliable whole-desktop recording · 可靠全桌面錄影** — Screen Recorder bulk-drains ffmpeg diagnostics, so heavy progress output cannot consume the bounded graceful-save window; forced or unconfirmed stops remain truthful failures. · 螢幕錄影會整批排走 ffmpeg 診斷輸出，繁忙進度唔會食晒有時限嘅正常儲存時間；強制或未確認停止仍然會如實報失敗。
- **Guided Windows maintenance · 引導式 Windows 維護** — System Doctors now completes the audited Windows/System and Maintenance roadmap: full Storage Sense retention, live Filter Keys timings, DISM association templates, bounded Update pause/resume, backup-gated driver rollback, broad Autoruns impact audit, irreversible ResetBase guidance, and selected Store-app repair. · 「系統醫生」而家補齊 Windows／System 同 Maintenance 審核：完整儲存感知保留、即時篩選鍵時間、DISM 關聯範本、有限更新暫停／恢復、先備份後驅動回復、廣泛 Autoruns 影響審核、不可逆 ResetBase 指引，同所選商店 app 修復。
- **Complete guided Media studio · 完整引導式 Media 工作台** — eleven production ffmpeg/ffprobe workflows add measured EBU R128, silence cleanup, two-pass stabilization, black-bar crop, lossless concat, hardware-probed NVENC, target-size encoding, subtitles, chapter splitting, HEIC/JXL batches, and metadata privacy. Every path uses argument vectors, staged outputs, bounded batches, cancellation, and owned scratch cleanup. · 新增 11 個正式 ffmpeg／ffprobe 工作流程，涵蓋 EBU R128、靜音、防震、黑邊、無損合併、NVENC、目標容量、字幕、章節、HEIC／JXL 同 metadata 私隱；全部用參數清單、暫存輸出、有界批次、取消同自家 scratch 清理。
- **Review-first developer, HA, and archive workflows · 先審閱開發、HA 同壓縮檔流程** — parameterized ports/Node/Corepack/Defender/TCP/cache controls, an exact short-lived Home Assistant config-check restart gate, and bounded archive filters/delete plus integrity-before-Recycle-Bin moves close eleven audited gaps with a 44-case pure harness. · 參數化 ports／Node／Corepack／Defender／TCP／快取、準確短效 HA 重啟驗證閘、有界壓縮檔篩選／刪除同完整性測試後先移回收筒，用 44 項純測試補齊十一個審核缺口。
- **Flagship reactor · 旗艦反應堆** — a PWR control-room simulator with point kinetics, thermal hydraulics, turbine and electrical systems, protection logic, fuel and waste services, water treatment, and opt-in external integrations. One canonical in-memory session moves between visible control rooms and a minimal UI-thread background loop: physics and the truthful status API keep advancing after the last control room closes, while audio, Home Assistant output, keep-awake, Windows linkage, and real-shutdown handling remain visible-page-owned and are stopped or restored. · 一個正式記憶體內 session 會喺可見控制室同精簡 UI-thread 背景 loop 之間交接：最後一個控制室關閉後，物理同如實狀態 API 繼續運行；音效、Home Assistant 輸出、保持喚醒、Windows 連動同真實關機處理就只由可見頁面擁有，並會停止或還原。
- **Reactor-powered industrial loads · 反應堆工業負載** — a green-ammonia Haber–Bosch plant and strict-priority grid load-shed dispatcher consume the live simulated bus, fail dark, and preserve reactor safety boundaries. · 綠氨哈柏法工廠同嚴格優先級電網卸載調度器會用即時模擬母線；冇電就停，而且唔會越過反應堆安全界線。
- **Optional playful feature power · 可選玩味功能電源** — nine deliberately power-gated features prefer live nuclear generation, while a default-off persisted permission can allow a simulated EDG. Every app session starts with an empty, stopped generator: while stopped, the operator must manually fill its 60 L tank, start it, and wait 10 seconds. It burns 1.0 L/min while starting or running. Its 250 MWe rating is checked per module, with a hard limit of two concurrent EDG-backed module tabs/instances; a third waits for a lease slot. Generator, fuel, and leases are session-only. Cake Factory keeps its exact-owner page and plant state alive on source loss: powered machinery and progress clocks—including an active CIP cycle—lock or freeze until that same tab regains nuclear power or an EDG outlet, while passive biological change, milk warming/spoilage risk, transport, and order deadlines continue. The other 19 reactor-industrial simulations remain nuclear-only. No real generator, hardware, or Windows power setting is touched. · 九個刻意加電力閘門嘅功能仍然首選即時核電；預設關閉而會保存嘅權限可以容許模擬 EDG。每次 app session 都由空缸兼停機開始，操作員要趁停機手動入滿 60 L 模擬柴油、撻機，再等 10 秒；啟動中或者運行中每分鐘耗油 1.0 L。250 MWe 會逐模組檢查，而且同時最多只可供兩個已開 EDG 模組分頁／instance；第三個要等 lease 位。蛋糕工廠失電時會保留準確 owner 嘅頁面同廠房狀態：有電機器同進度計時——包括進行中嘅 CIP——會鎖住或凍結，直至同一分頁重新取得核電或 EDG 插槽；被動生物變化、牛奶升溫／變壞風險、運輸同訂單期限就會繼續。其餘 19 個反應堆工業模擬繼續只用核電。佢唔會郁真實發電機、硬件或者 Windows 電源設定。

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

The current contract is **67/67** scenarios, and the harness returns nonzero if any scenario fails or throws. Use `-- --verify-exit-code-contract` for its fast exit-code self-test.

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

Run the process-free Windows maintenance contract after changing the guided System Doctors workflows:

改過「系統醫生」引導式 Windows 維護流程後，要跑無副作用合約：

```powershell
dotnet run --project tests\SystemMaintenanceCore.Tests -c Debug
```

The harness covers **22/22** validation, argument-vector, bounded timing, update-pause, conservative driver rollback, Store-app identity, and startup-impact cases without touching the registry, drivers, DISM, or app data. · Harness 有 **22/22** 個驗證、參數向量、有限時間、更新暫停、保守驅動回復、商店 app 身份同開機影響 case；唔會郁 registry、驅動、DISM 或 app 資料。

Run the Regex Builder safety harness after changing the builder, .NET regex evaluation, search-pattern contract, limits, flags, or replacement preview:

改過正則砌法、.NET regex 運算、搜尋 pattern 合約、上限、旗標或者替換預覽之後，要跑：

```powershell
dotnet run --project tests\RegexBuilder.Tests -c Debug
```

The current contract is **33/33**, covering guided tokens, syntax failures, Unicode, multiline anchors, capture groups, no-match, zero-width progress, result/size caps, adversarial timeout, plain-text-versus-regex behavior, all synchronized flags/session state, every integrated surface, the full shared control, and the complete classified XAML inventory. · 目前合約係 **33/33**，覆蓋引導 token、語法錯誤、Unicode、多行錨點、擷取群組、無配對、零寬度安全推進、結果／大小上限、對抗式超時、純文字對 regex 行為、全部同步旗標／session 狀態、每個已整合介面、完整版共用控制，同完整 XAML 分類清單。

Run the pure roadmap-workflow harness after changing Developer & Terminal, the Home Assistant restart gate, or the bespoke Archives create/delete workflows:

改過開發與終端機、Home Assistant 重啟安全閘，或者壓縮檔專用建立／刪除流程之後，要跑：

```powershell
dotnet run --project tests\RoadmapWorkflowCore.Tests -c Debug
```

The contract is **44/44** and does not terminate a process, alter Defender/TCP/cache state, modify a real archive, or contact Home Assistant. · 合約係 **44/44**，唔會終止程序、改 Defender／TCP／快取狀態、修改真實壓縮檔，亦唔會連去 Home Assistant。

Run the application-wide notification-centre contract after changing notice lifetimes, history, replacement, persistence, actions, or shell hosting:

改過通知顯示時間、記錄、取代、保存、動作或者外殼 host 之後，要跑：

    dotnet run --project tests\NotificationCenter.Tests\NotificationCenter.Tests.csproj -c Debug

The current contract is **16/16** and does not launch the app or perform a system operation. · 目前合約係 **16/16**，唔會啟動 app 或執行系統操作。

Visual changes require a fresh inspected screenshot for every changed page. If graphics capture is unavailable, record the exact blocker and keep functional, accessibility, and visual evidence separate.

視覺改動要為每個改過嘅頁面提供最新、已檢視截圖。如果環境擷取唔到畫面，要記低確實阻礙，並將功能、無障礙同視覺證據分開。

## Reactor safety · 反應堆安全

- Meltdown-to-real-PC-shutdown is **off by default**, in-memory only, and requires a visible control room. An ARM request made elsewhere returns to Reactor and arms from a low-priority UI callback only after loading has completed and ABORT can be shown. The 10-second deadline and accepted/refused OS outcome are truthful session-global state, but any foreground page/window handoff automatically aborts an active countdown; closing the last control room also disarms and resets it. Once Windows accepts the shutdown request, ABORT is hidden because that OS request can no longer be cancelled; a refusal is shown and never retried without an explicit disarm/re-arm. · 熔毀觸發真實電腦關機預設係**關閉**、只存記憶體，而且必須有可見控制室；喺其他頁提出 ARM 會先返回反應堆，等載入完成兼可以顯示 ABORT 後，先由低優先次序 UI callback 真正啟用。10 秒 deadline 同 OS 接受／拒絕結果係如實嘅全 session 狀態，但任何前景頁面／視窗交接都會自動中止進行中嘅倒數；關閉最後一個控制室亦會解除武裝兼重設。Windows 一接受關機要求就會收起 ABORT，因為 OS 要求已經唔可以取消；拒絕就會如實顯示，未有明確解除武裝再重新 ARM 前唔會重試。
- Parallel Reactor pages remain live read-only observers: gauges and status continue rendering, but plant controls and companion launchers are disabled. Authority demotion closes mutating HTML/full-control-room, startup-checklist, and SCRAM-widget companions while read-only widgets may remain; any active shutdown countdown is aborted before the handoff. A demoted meltdown page keeps its live overlay without ABORT/reset controls and collapses it automatically when the authoritative driver resets the shared plant. · 並行開住嘅其他反應堆頁會保持即時唯讀：儀錶同狀態繼續更新，但機組控制同 companion 啟動掣會停用。Authority 降級會關閉可改動模擬嘅 HTML／完整控制室、起動 checklist 同 SCRAM widget companion；唯讀 widget 可以繼續留低，而任何進行中嘅關機倒數都會喺交接前中止。降級頁面嘅熔毀 overlay 會保持即時顯示，但冇 ABORT／重設控制；authoritative driver 重設共用機組時，overlay 亦會自動收起。
- A new reactor starts held in MODE 5 cold shutdown; the operator must start it. · 新反應堆會保持 MODE 5 冷停堆，要由操作員啟動。
- Waste writes enforce a disk free-space floor and a default 50 GB cap. · 廢料寫入會保留磁碟可用空間底線，預設上限係 50 GB。
- Real-world side effects are opt-in and reversible. · 現實世界副作用全部要明確選擇加入，而且可以還原。
- Simulated feature-bus EDG fallback is **off by default**. Permission persists, but generator state, the 60 L simulated fuel tank, and module-instance leases never do: each app session requires a manual fill and fresh 10-second start, with fuel burning at 1.0 L/min while starting or running. Nuclear stays preferred; at most two open module tabs/instances may hold EDG leases. Cake Factory preserves its plant state on exact-owner power loss while powered machinery and progress clocks, including CIP, pause until power returns; passive biological, spoilage, transport, and order-time processes continue. The simulation never controls real equipment or changes Windows power. · 模擬功能匯流排 EDG 後備預設**關閉**；權限會保存，但發電機狀態、60 L 模擬油缸同模組 instance lease 永遠唔保存。每次 app session 都要重新手動入油兼等 10 秒啟動，而啟動中或者運行中每分鐘耗油 1.0 L。核電繼續優先；同時最多兩個已開模組分頁／instance 可以持有 EDG lease。蛋糕工廠喺準確 owner 失電時會保留廠房狀態，而包括 CIP 在內嘅有電機器同進度計時會暫停，直至電力恢復；被動生物、變壞、運輸同訂單時間流程就會繼續。模擬永遠唔會控制真實設備或者改 Windows 電源。

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
