# Package Manager · 套件管理

![Managed production Package Manager reference · 受控正式版套件管理參考](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-packages.png)

> **Screenshot provenance · 截圖來源：** This existing image shows the shipping managed .NET Package Manager. It is a managed production reference, **not current native C++ visual evidence**. Fresh native capture is `capture-blocked`: `CopyFromScreen` is unavailable and `PrintWindow` returns a blank/near-uniform WinUI client frame that the driver rejects. No stale, synthetic, blank, or managed image is substituted as a native screenshot. · 呢張現有圖片顯示發佈中受控 .NET 套件管理器，只係受控正式版參考，**唔係目前原生 C++ 視覺證據**。最新原生截圖係 `capture-blocked`：`CopyFromScreen` 用唔到，而 `PrintWindow` 回傳空白／接近單色嘅 WinUI client frame，driver 會拒絕。唔會用舊圖、合成圖、空白圖或者受控版圖片頂替原生截圖。

## Production and migration status · 正式版同遷移狀態

| Runtime · 執行版本 | Current truth · 目前實況 |
|---|---|
| **Shipping managed .NET app · 發佈中受控 .NET app** | The production Package Manager has substantial functionality across 11 Windows package engines and all nine workflows: discovery/install, updates, installed-package operations, bundles, sources, ignored rules, setup, settings and a shared operation queue. · 正式版套件管理器喺 11 個 Windows 套件引擎同九個流程都有大量實際功能：搜尋／安裝、更新、已安裝套件操作、清單、來源、忽略規則、引擎設定、設定同共用操作佇列。 |
| **C++20/C++/WinRT migration · C++20／C++/WinRT 遷移** | The current batch runs **Discover, Updates, and Installed result queries**, read-only per-row **Details** queries with bounded output rendering, plus a **Sources command probe** whose raw output is withheld. Install/update/uninstall and Update All still create preview-only argv plans in Operations and do not execute package mutations. It is UniGetUI-informed coverage, not a full clone, full parity claim or production cutover. · 目前批次會執行**搜尋安裝、可更新同已安裝結果查詢**、逐列只讀**詳細資料**查詢同有界輸出顯示，另加會隱藏原始輸出嘅**來源指令探測**。安裝／更新／解除安裝同全部更新仍然只會喺操作檢視建立 argv 預覽計劃，唔會執行套件修改。佢係參考 UniGetUI 嘅涵蓋工作，唔係完整複製、完整對等聲稱或者正式版切換。 |

The native shell exposes the same 11 manager choices: WinGet, Scoop, Chocolatey, pip, npm, .NET tools, Windows PowerShell Gallery, PowerShell 7 PSResourceGet, Cargo, Bun and vcpkg. Availability probes unlock only engines whose non-destructive version check succeeds. A visible probe or placeholder does not make the corresponding production workflow native-complete.

原生 shell 顯示同一組 11 個管理器：WinGet、Scoop、Chocolatey、pip、npm、.NET 工具、Windows PowerShell Gallery、PowerShell 7 PSResourceGet、Cargo、Bun 同 vcpkg。只有非破壞性版本探測成功嘅引擎先會解鎖。見到探測或者 placeholder 唔代表相應正式版流程已經原生完成。

## Nine-view truth table · 九個檢視實況表

| View · 檢視 | Shipping managed production · 發佈中受控正式版 | Current native C++ batch · 目前原生 C++ 批次 |
|---|---|---|
| **Discover · 搜尋安裝** | Searches selected managers, filters results, views package details, and installs one or many packages. · 搜尋已選管理器、篩選結果、檢視套件詳細資料，同安裝一個或多個套件。 | Search results remain non-mutating; each row can run a bounded read-only Details query or add a preview-only Install argv plan to Operations. · 搜尋結果仍然唔會修改系統；每列可以執行有界只讀詳細資料查詢，或者將只供預覽嘅安裝 argv 計劃加入操作檢視。 |
| **Updates · 可更新** | Enumerates updates, views package details, updates one manager or all managers, and supports ignore, pin and snooze policies. · 列出更新、檢視套件詳細資料、逐管理器或全部更新，並支援忽略、釘選同暫停政策。 | Update enumeration remains non-mutating; per-row Details runs read-only, while per-row Update and Update All create preview-only argv plans without running package commands. · 更新列舉仍然唔會修改系統；逐列詳細資料只讀執行，而逐列更新同全部更新只會建立 argv 預覽計劃，唔會執行套件指令。 |
| **Installed · 已安裝** | Lists installed packages, views package details, and performs uninstall or other selected operations. · 列出已安裝套件、檢視套件詳細資料，並執行解除安裝或其他已選操作。 | Installed-package enumeration remains non-mutating; Details runs read-only, while Uninstall creates a preview-only argv plan. · 已安裝套件列舉仍然唔會修改系統；詳細資料只讀執行，而解除安裝只會建立 argv 預覽計劃。 |
| **Bundles · 套件清單** | Builds, edits, imports, exports and installs portable package sets. · 建立、編輯、匯入、匯出同安裝可攜套件清單。 | Native bundle snapshot import/export preview is now implemented; full interoperability and parity are still gated. · 原生清單快照匯入／匯出預覽而家已實作；完整互通同對等仍然鎖住。 |
| **Sources · 來源** | Lists, adds, removes and refreshes supported sources, buckets, feeds and repositories. · 列出、新增、移除同重新整理支援嘅來源、bucket、feed 同 repository。 | Command probe only: it runs read-only source commands, then withholds all raw configuration and diagnostics until manager-specific secret redaction is proven. No source rows are shown; add/remove stays disabled. · 只限指令探測：執行只讀來源指令之後，逐管理器機密遮罩未證實之前會隱藏全部原始設定同診斷。唔會顯示來源資料列；新增／移除保持停用。 |
| **Ignored · 已忽略** | Reviews and removes version pins, all-version ignores and timed snoozes. · 檢視同移除版本釘選、全部版本忽略同限時暫停。 | Gated placeholder. · 鎖住嘅 placeholder。 |
| **Setup · 設定引擎** | Checks availability and bootstraps missing engines or dependencies through the production coordinator. · 檢查可用性，再經正式版協調器安裝欠缺引擎或者相依。 | Non-destructive availability probes only; bootstrap install remains gated. This probe surface is not full Setup parity. · 只限非破壞性可用性探測；bootstrap 安裝仍然鎖住。探測介面唔等於完整 Setup 對等。 |
| **Settings · 設定** | Persists schedules, notifications, concurrency, manager paths/arguments, proxy, backup and install defaults. · 保存排程、通知、同時操作數、管理器路徑／參數、代理、備份同安裝預設。 | Native JSON persistence now remembers package view, search text, sort mode, and manager-filter choices; broader per-manager defaults, backup, and restore remain gated. · 原生 JSON 持久化而家會記住套件檢視、搜尋文字、排序模式同管理器篩選；更完整逐管理器預設、備份同還原仍然鎖住。 |
| **Operations · 操作佇列** | Shows active, queued and completed work with captured output, cancellation, ordering and retry. · 顯示進行中、排隊同已完成操作，附輸出、取消、次序同重試。 | Recent native probe/query history, Details query events, and preview-only mutation plans now persist locally and can be cleared from the tab, but the real coordinator, queued mutation execution, cancel, reorder and retry remain gated. · 最近嘅原生探測／查詢歷史、詳細資料查詢事件同只供預覽嘅修改計劃而家可以本機保存，同埋可喺分頁清除；但真正協調器、排隊修改執行、取消、重新排序同重試仍然鎖住。 |

There are therefore three enabled native result-query paths, bounded read-only Details queries, one source-command probe, and preview-only install/update/uninstall plan generation. Bundles now has a native snapshot import/export preview, while Ignored, Setup bootstrap and the real Operations queue remain gated; all install/update/uninstall/source/bulk execution remains separately gated. Settings is now a small native persistence slice rather than a bare placeholder, and Operations now keeps a recent local history snapshot instead of a transient-only log.

所以目前有三條已開放原生結果查詢路徑、有界只讀詳細資料查詢、一個來源指令探測，以及只供預覽嘅安裝／更新／解除安裝計劃產生。其餘五個 shell 檢視——套件清單、已忽略、Setup bootstrap、設定同真正操作佇列——仍然鎖住，而所有安裝／更新／解除安裝／來源／批次執行亦另外鎖住。

## Shipping managed functionality · 發佈中受控功能

The following capabilities describe the **managed production implementation**, not the current native batch:

以下功能描述**受控正式版實作**，唔係目前原生批次：

- A bounded-concurrency coordinator handles row actions, multi-select batches, bundle installs and scheduled updates; it suppresses duplicate live work and retains status, output, cancellation and retry history. · 有同時操作上限嘅協調器會處理單列動作、多選批次、清單安裝同排程更新；避免重複操作，並保留狀態、輸出、取消同重試歷史。
- Global defaults and per-package overrides cover operation-specific arguments, elevation, interactive mode, hash/prerelease flags, scope, architecture, version, install location, update/uninstall policy, pre/post hooks and process shutdown rules. · 全域預設同逐套件覆寫涵蓋逐操作參數、提升權限、互動模式、雜湊／預覽版旗標、範圍、架構、版本、安裝位置、更新／解除安裝政策、前後鈎同關閉程序規則。
- Bundles support JSON, `.ubundle`, YAML and XML, preserve package options and source identity, validate references, flag incompatible entries and export PowerShell install scripts. · 清單支援 JSON、`.ubundle`、YAML 同 XML，保留套件選項同來源身份、驗證參照、標示不相容項目，亦可匯出 PowerShell 安裝腳本。
- `PackageSourcePolicy` is the managed command boundary: it emits only manager-valid source forms, preserves safe metadata where a CLI has no source selector, and rejects local, unknown, malformed or unsupported sources before preview, queueing or execution. · `PackageSourcePolicy` 係受控版指令邊界：只輸出管理器有效來源形式；CLI 冇來源選擇器時保留安全中繼資料；本機、未知、格式錯誤或者唔支援來源會喺預覽、排隊或者執行之前拒絕。
- Manager-specific behavior includes mutually exclusive `.NET --global`/`--tool-path`, version-aware PowerShell 7 cleanup, direct Bun registry search/global-manifest reads, and validated vcpkg/npm identifiers. · 各管理器保障包括互斥嘅 `.NET --global`／`--tool-path`、識別版本嘅 PowerShell 7 舊版清理、直接 Bun registry 搜尋／全域 manifest 讀取，同已驗證 vcpkg／npm ID。
- The background scheduler respects ignored/snoozed updates, minimum age, metered network, battery and Battery Saver gates, and detectable WinGet installer-host changes. · 背景排程器遵守忽略／暫停更新、最低年齡、流量計費網絡、電池同慳電模式閘，亦會檢查可偵測嘅 WinGet installer host 變更。

## Native safety and accessibility · 原生安全同無障礙

- Native queries use reviewed argument-vector builders, bounded parsers, allowlisted HTTPS endpoints and a contained process runner; no upstream UniGetUI executable or UI is launched. · 原生查詢使用經審核嘅 argument-vector builder、有界解析器、准許清單 HTTPS endpoint 同受控 process runner；完全唔會啟動上游 UniGetUI executable 或 UI。
- Details uses the same validated command builders and contained runtime as the other read-only queries, then renders bounded command output in the result area. Install, Update, Uninstall, and Update All remain preview-only argv plans and never start package-manager mutation processes. · 詳細資料會用同一套已驗證 command builder 同受控 runtime 執行，只喺結果區顯示有界指令輸出。安裝、更新、解除安裝同全部更新仍然只係 argv 預覽計劃，永遠唔會啟動套件修改 process。
- External manager commands fail closed while WinForge is elevated. Mutations remain disabled until explicit consent, elevation brokering, cancellation and batch coordination are proven at normal integrity. · WinForge 提升權限時，外部管理器指令會 fail closed；明確同意、提升權限 broker、取消同批次協調喺正常權限驗證完成之前，修改操作保持停用。
- Only successful, fully resolved parser results become current package rows. Partial PyPI candidates and unresolved .NET update rows stay out of the result list and retain an honest per-engine diagnostic. · 只有成功兼完整解析嘅結果先會成為目前套件列；部分 PyPI 候選同未解析 .NET 更新列唔會放入結果清單，並保留誠實嘅逐引擎診斷。
- Changing a manager filter, search text or view invalidates and clears the old result generation. Each manager exposes a deterministic completion state with verified row count. · 更改管理器篩選、搜尋文字或者檢視會令舊結果 generation 失效兼清除；每個管理器都有穩定完成狀態同已驗證資料列數目。
- A persistent bilingual live region announces probe/query start, completion, failure and invalidation using polite or assertive UI Automation notifications. · 持久雙語 live region 會用 polite 或 assertive UI Automation 通知探測／查詢開始、完成、失敗同失效。

## Evidence and current blockers · 證據同目前阻礙

- **Complete native test executable:** **261/261 in Debug and 261/261 in Release** (219 core route/package-manager checks plus 42 parser checks). Every prior Package Manager case remains, and the added assertion proves unsafe Details queries fail before process creation. · **完整原生測試 executable：** Debug **261/261**、Release **261/261**（219 個 core route／package-manager 檢查加 42 個 parser 檢查）。之前全部套件管理案例都保留，而新增 assertion 證明唔安全嘅詳細資料查詢會喺 process 建立之前 fail closed。
- **Elevated process-owned UI Automation smoke:** **93/93**. Every prior Package Manager assertion remains, including exact `package-discover`, `package-updates`, and `package-installed` view selection, the 11 manager filters, nine-view shell, busy/ready/result states, fail-closed elevation behavior and accessibility identifiers. It does **not** prove live external manager queries. · **提升權限、只控制自己 process 嘅 UI Automation smoke：** **93/93**。之前全部套件管理 assertion 都保留，包括 `package-discover`、`package-updates` 同 `package-installed` 準確檢視、11 個管理器篩選、九檢視 shell、busy／ready／result 狀態、fail-closed 提升權限行為同無障礙 ID。**唔代表**真實外部管理器查詢已通過。
- **Normal-integrity live external smoke:** blocked before package execution. Even an interactive scheduled task registered with `RunLevel=Limited` received a Windows token that failed the standard-user proof, so the harness stopped rather than weakening the gate. No live-query pass is claimed. · **正常權限 live 外部 smoke：** 喺執行套件之前已受阻。即使互動式排程工作登記做 `RunLevel=Limited`，Windows 回傳嘅 token 仍然未能通過標準使用者證明，所以 harness 會停止，唔會削弱安全閘；唔會聲稱 live query 已通過。
- **LowLevel MCP headless desktop:** the checkout exists at `C:\Users\Administrator\Documents\GitHub\lowlevel-computer-use-mcp`, but no LowLevel MCP tools are exposed in this Codex session. The documented cheap fallback is also blocked here: `uv` is not installed, the system `python` is only the Microsoft Store alias, and bundled Python cannot import `mcp`. · **LowLevel MCP 無頭 desktop：** checkout 存在於 `C:\Users\Administrator\Documents\GitHub\lowlevel-computer-use-mcp`，但呢個 Codex session 冇曝露 LowLevel MCP tools。文件所講嘅 cheap fallback 喺呢度亦受阻：冇安裝 `uv`，系統 `python` 只係 Microsoft Store alias，而 bundled Python 未能 import `mcp`。
- **Native visual capture:** `capture-blocked`. `CopyFromScreen` is unavailable; `PrintWindow` returns a blank/near-uniform WinUI client frame; the driver rejects it. No native PNG was created, inspected or substituted. · **原生視覺截圖：** `capture-blocked`。`CopyFromScreen` 用唔到；`PrintWindow` 回傳空白／接近單色 WinUI client frame；driver 會拒絕。冇原生 PNG 產生、檢查或者被頂替。

## Remaining UniGetUI-informed parity gaps · 尚欠 UniGetUI 參考對等項目

- Capability model, manager enablement, maintenance state and logs. · Capability 模型、管理器啟用、維護狀態同日誌。
- Parsed/rich details fields, media/share actions and open-install-location behavior. · 已解析嘅豐富詳細資料欄位、媒體／分享動作同開啟安裝位置行為。
- Queue reorder, run-next/run-last, retry modes, persistent history, and unified source/download tracking. · 佇列重新排序、下一個／最後執行、重試模式、持久歷史，同統一來源／下載追蹤。
- Bundle row and bulk actions, downloads, installed detection and broader interoperability. · 清單逐列／批次動作、下載、已安裝偵測同更廣泛互通。
- Secure opt-in gates, elevation broker, batch consent and mutation lifecycle. · 安全 opt-in 閘、提升權限 broker、批次同意同修改生命週期。
- Backup, restore and complete per-manager settings. · 備份、還原同完整逐管理器設定。
- Actionable verb-aware notifications and tray commands. · 可操作兼識別動詞嘅通知同系統匣指令。
- Authenticated API/CLI/headless surfaces, richer deep links and widgets. · 已驗證身份嘅 API／CLI／headless 介面、更豐富 deep link 同 widget。

Linux- and macOS-only managers are outside this Windows product's scope and are not counted as WinForge parity gaps. · 只供 Linux 同 macOS 嘅管理器唔屬於呢個 Windows 產品範圍，唔會計做 WinForge 對等缺口。

## Tray shortcuts and deep links · 系統匣捷徑同深層連結

- In the managed production app, tray commands for **Discover packages**, **Updates** and **Installed packages** bring WinForge forward and select the exact view. · 喺受控正式版，系統匣嘅**搜尋安裝**、**可更新**同**已安裝**指令會帶返 WinForge 去前景，並揀返準確檢視。
- Automation can use the `package-*` and `packages-*` alias family for `discover`, `updates`, `installed`, `bundles`, `sources`, `ignored`, `setup`, `settings`, and `operations`, or `--page module.packages#bundles`; these resolve to the matching `module.packages` deep links. Native routing is present, but route availability alone is not a parity claim. · 自動化可用 `package-*` 同 `packages-*` 呢組別名去叫 `discover`、`updates`、`installed`、`bundles`、`sources`、`ignored`、`setup`、`settings` 同 `operations`，或者用 `--page module.packages#bundles`；會解析去相應嘅 `module.packages` deep link。原生導覽已存在，但淨係有 route 唔代表功能對等。

## UniGetUI provenance · UniGetUI 來源依據

The complete tracked [Devolutions/UniGetUI](https://github.com/Devolutions/UniGetUI) tree is vendored at `ThirdParty/UniGetUI`, pinned exactly to commit `21116375c8299d1db38a3c3b4c2eb7e18bc97c4e` from upstream `main` (snapshot date 2026-07-10). UniGetUI is MIT-licensed; its original notice is preserved at `ThirdParty/UniGetUI/LICENSE`, while bundled third-party material retains separate notices. `ThirdParty/**` is excluded from WinForge build and publish inputs: upstream UI/framework, IPC and telemetry are not compiled, embedded or launched. The snapshot is provenance and a behavior inventory for audited, UniGetUI-informed reimplementation—not a claim that WinForge ships UniGetUI or has cloned every feature.

完整追蹤嘅 [Devolutions/UniGetUI](https://github.com/Devolutions/UniGetUI) 原始碼樹存放喺 `ThirdParty/UniGetUI`，準確固定到上游 `main` commit `21116375c8299d1db38a3c3b4c2eb7e18bc97c4e`（快照日期 2026-07-10）。UniGetUI 採用 MIT 授權，原始 notice 保留喺 `ThirdParty/UniGetUI/LICENSE`；隨附第三方材料仍然跟返各自 notice。`ThirdParty/**` 已排除喺 WinForge 建置同發佈輸入之外；上游 UI／framework、IPC 同 telemetry 唔會被編譯、嵌入或者啟動。快照只係來源依據同行為清單，用嚟做經審核、參考 UniGetUI 嘅重新實作——唔代表 WinForge 直接發佈 UniGetUI，亦唔會聲稱已複製每項功能。

---
[← Module index · 模組索引](Home) · [README](https://github.com/codingmachineedge/WinForge/blob/main/README.md) · [Screenshots](Screenshots)
