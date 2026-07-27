# Reactor Safety & Integrations · 反應堆安全與整合

**EN —** Every real-world side-effect of the reactor is **opt-in, clearly gated, and reversible**. This page collects the safety toggles, the separate simulated feature-power fallback, the OS integrations, the crash-safe autosave, and the public status API that lets other apps depend on the reactor.

**粵語 —** 反應堆對真實世界嘅每一個影響都係**可選、明確開關、可逆轉**。呢頁集合咗安全開關、獨立嘅模擬功能電源後備、作業系統整合、防崩潰自動儲存，以及畀其他 app 依賴反應堆嘅公開狀態 API。

**EN —** All of these live on a **dedicated Reactor Settings page** (⚙ button on the reactor toolbar, or `WinForge.exe --page reactorsettings`), kept separate from the pure-simulation controls. Defaults: feature-bus emergency-diesel fallback **OFF**, ARM real-shutdown **OFF**, Windows-settings link **OFF**, Home Assistant mirror **OFF**, status API **ON**, autosave **ON**, keep-awake **ON**.

**粵語 —** 以上全部都喺一個**獨立嘅反應堆設定頁**（反應堆工具列嘅 ⚙ 掣，或 `WinForge.exe --page reactorsettings`），同純模擬控制分開。預設：功能匯流排應急柴油後備 **OFF**、真實關機 **OFF**、Windows 設定連動 **OFF**、Home Assistant 連動 **OFF**、狀態 API **ON**、自動儲存 **ON**、保持喚醒 **ON**。

**EN —** The page owns one named live timer callback for status-API state and simulated-EDG start, fuel, and lease status across its whole page instance. Every load safely restores the language-change handler; every unload stops the timer and releases that handler. The timer never fills or starts the EDG, changes a toggle default, or invokes reactor, Windows-linkage, Home Assistant, or real-shutdown actions.

**粵語 —** 呢頁喺成個 page instance 只用一個具名 live timer callback 更新狀態 API，同模擬 EDG 嘅啟動、燃油、lease 狀態。每次 load 都會安全噉重新訂閱語言變更 handler；每次 unload 都會停 timer 同解除訂閱。Timer 唔會自動入油或者撻着 EDG、改任何開關預設，亦唔會觸發反應堆、Windows 連動、Home Assistant 或真實關機動作。

**Capture status · 截圖狀態：** Fresh 2026-07-27 captures were taken from the real self-contained WinUI build on dedicated off-screen desktops. The launch/capture path never switched desktops, opened a visible terminal, or took foreground focus. The default settings view, explicitly enabled empty-tank state, 10-second starting state, running/fuel telemetry, dependency recovery page, EDG-powered Cake Factory, search badge, and corrected two-row Reactor toolbar were all inspected at full resolution. · 2026-07-27 已由真實 self-contained WinUI build 喺專用離屏桌面擷取全新截圖；啟動／擷取流程冇切換桌面、冇彈出可見終端，亦冇搶前景焦點。預設設定頁、明確啟用但空缸、10 秒啟動中、運行／油量 telemetry、相依復原頁、EDG 供電蛋糕工廠、搜尋徽章，同修正後兩行反應堆工具列都已按原解像度檢視。

![Reactor Settings with emergency-diesel fallback off](https://raw.githubusercontent.com/Ding-Ding-Projects/WinForge/main/docs/screenshot-reactorsettings.png)

![Running emergency diesel with live fuel and two-slot telemetry](https://raw.githubusercontent.com/Ding-Ding-Projects/WinForge/main/docs/screenshot-reactorsettings-running.png)

See [Regex Cheatsheet & Reactor Settings Lifecycle](RegexCheat-ReactorSettings-Lifecycle.md) for the focused lifecycle proof. · 專注嘅生命週期證明請睇[正則速查同反應堆設定生命週期](RegexCheat-ReactorSettings-Lifecycle.md)。

---

## Optional feature-bus emergency diesel · 可選功能匯流排應急柴油

**EN —** Nuclear generation is intentionally optional for nine playful feature gates, but fallback is not automatic or free. **Allow emergency-diesel fallback** is **OFF by default**. That permission persists when changed; simulated fuel, generator state, and owner-token module leases do not. Every app launch begins with an empty, stopped EDG. While stopped, the operator must manually fill its **60 L** simulated tank, start it, and wait the full **10 seconds** before it can energize the feature bus. It burns **1.0 L/min while starting or running**. Disabling permission stops it, releases leases, and clears the session fuel immediately.

**粵語 —** 九個玩味功能閘門可以選擇唔用核電，但後備唔會自動送上門，亦唔係撳掣即有。**容許應急柴油後備**預設係 **OFF**；改過嘅權限會保存，但模擬燃油、發電機狀態同 owner-token 模組 lease 唔會。每次重新開 app 都由空缸兼停機開始，操作員要趁停機手動將模擬油缸加滿 **60 L**、撻機，再等足 **10 秒**先可以為功能匯流排供電。啟動中或者運行中每分鐘耗油 **1.0 L**；關閉權限亦會即刻停機、釋放 lease 同清空今次 session 嘅油。

- **Nuclear stays preferred · 核電繼續優先：** if the live reactor status is healthy and meets the selected entry's MWe threshold, WinForge uses it even while the EDG is running. · 即時反應堆健康兼達到所選項目 MWe 門檻時，就算 EDG 運行中都會用核電。
- **250 MWe per module; two leases · 逐模組 250 MWe；兩個 lease：** each candidate module must fit the EDG's 250 MWe threshold. At most two concurrently open module tabs/instances may hold owner-token EDG leases; a third waits until an existing owner closes, navigates away, or releases its slot. Rechecking the same module in the same tab is idempotent. · 每個候選模組都要符合 EDG 嘅 250 MWe 門檻；同時最多兩個已開模組分頁／instance 可以持有 owner-token EDG lease，第三個要等現有 owner 關閉、轉頁或者釋放個位。同一分頁重試同一模組唔會重複佔位。
- **Eight ordinary leased modules · 八個一般租電模組：** Ollama, Blender, Docker, WSL & VM Launcher, VirtualBox Manager, Packer, Minecraft Server, and Android Emulator each occupy one slot while open on EDG fallback. · Ollama、Blender、Docker、WSL 與 VM、VirtualBox、Packer、Minecraft 伺服器同 Android 模擬器用 EDG 後備開住時，各自佔一個位。
- **Cake Factory is continuous · 蛋糕工廠持續用電：** its 35 MWe gate can use one exact-owner EDG lease, and its farm/factory simulation continuously reads that source. If nuclear is unavailable and that EDG source stops or exhausts its fuel, the page and factory state stay alive while powered machinery and progress—including an active CIP cycle—lock or freeze at their exact state. Passive biological change, milk warming/spoilage risk, transport, and order clocks continue. Restoring power to the same tab resumes powered work from that state. · 佢嘅 35 MWe 閘門可以用一個準確 owner 嘅 EDG lease，而且農場／工廠模擬會持續讀取該電源；冇核電而該 EDG 電源停機或者燒光油時，頁面同工廠狀態會保留，有電機器同進度（包括進行中嘅 CIP）會鎖住或者停喺原本狀態；被動生物變化、牛奶升溫／變壞風險、運輸同訂單時鐘就會繼續。同一分頁恢復供電後，有電流程會由嗰個狀態繼續。
- **Industrial boundary · 工業界線：** the other **19 reactor-industrial simulations remain nuclear-only**. The EDG does not backfeed Ammonia, Load Shed, or any of those direct live-reactor models. · 其餘 **19 個反應堆工業模擬繼續只用核電**；EDG 唔會反送電去合成氨、卸載調度或者任何直接讀即時反應堆嘅模型。

The EDG, 60 L diesel fill, 1.0 L/min consumption, and leases are in-process game state only. A manual stop releases every lease but retains the remaining fuel for this app session; a later app launch still starts empty. Nothing here starts real plant equipment, touches a physical generator or fuel system, changes Windows power settings, or replaces the truthful public reactor-status snapshot. Cake Factory receives a private synthetic feature-bus snapshot only while fallback is permitted, fuelled, running, and leased. · EDG、60 L 柴油加注、每分鐘 1.0 L 消耗同 lease 都只係程式內遊戲狀態。手動停機會釋放全部 lease，但餘油只會留喺今次 app session；下次開 app 仍然由空缸開始。呢度永遠唔會啟動真實廠房設備、掂實體發電機或燃油系統、改 Windows 電源設定，亦唔會取代如實嘅公開反應堆狀態。只有後備已容許、有油、運行中兼取得 lease 時，蛋糕工廠先會收到私有合成功能匯流排快照。

---

## Canonical session and visible-safety boundary · 正式 session 同可見安全界線

**EN —** The reactor has one canonical in-memory simulation session for the app process. Its physics, mission clock, command-line auto-start latch, and truthful real-shutdown deadline/outcome state are shared rather than recreated per page. When one or more Reactor pages are visible, the most recently loaded page is the sole foreground driver. Opening or closing another visible control room changes that responsibility without resetting the plant, but safety takes priority over continuity: any active real-shutdown countdown is automatically aborted before authority changes and is never carried into the new page.

**粵語 —** 每個 app process 只有一個正式嘅記憶體內反應堆模擬 session。物理、任務時鐘、命令列自動起動 latch，同如實嘅真實關機期限／結果狀態都係共用，唔會每頁重新建立。有一個或以上反應堆頁面可見時，最新載入嗰頁係唯一前景 driver。開啟或者關閉另一個可見控制室會交接責任而唔重設機組，但安全優先過連續性：任何進行中嘅真實關機倒數都會喺 authority 改變前自動中止，絕對唔會帶去新頁面。

**EN —** Other visible Reactor pages remain live read-only observers rather than frozen copies: gauges, status, annunciators, mimic, trends, protection panels, synchronized control positions, and the status-API card keep rendering, but plant/scenario/SCRAM controls and companion launchers are disabled. On authority demotion, mutating HTML/full-control-room, startup-checklist, and SCRAM-widget companions close immediately; read-only core-power, status, and startup-gauge widgets may remain. The countdown abort happens before that authority transfer. A demoted meltdown page keeps a live overlay with no ABORT/reset controls; it synchronizes and collapses when the authoritative driver resets the shared plant, preventing stale overlay state on later promotion.

**粵語 —** 其他可見反應堆頁唔係凍結副本，而係即時唯讀觀察頁：儀錶、狀態、警示、流程圖、趨勢、保護面板、同步控制位置同狀態 API 卡會繼續更新，但機組／情景／SCRAM 控制同 companion 啟動掣會停用。Authority 降級時，可改動模擬嘅 HTML／完整控制室、起動 checklist 同 SCRAM widget companion 會即刻關閉；唯讀爐心功率、狀態同起動儀錶 widget 可以留低。倒數會喺 authority 交接之前先中止。降級頁面嘅熔毀 overlay 會保持即時顯示但冇 ABORT／重設控制；authoritative driver 重設共用機組時會同步兼收起，避免之後升級時殘留舊 overlay。

**EN —** If no Reactor page is visible, a minimal UI-thread session loop keeps only the simulated physics, automatic-start progression, mission clock, and truthful public status API current. Synthesized audio and every real-world integration remain page-owned: no hidden page may keep the PC awake, mirror Home Assistant, retain Windows SystemLink changes, or carry a real-shutdown arm. Closing the last Reactor page automatically disarms and cancels any pending countdown, publishes Home Assistant entities off, releases Awake, restores SystemLink originals, and stops reactor audio while the background simulation/API continue. Persisted opt-in settings remain configured, but they do not silently keep their real effects active.

**粵語 —** 如果冇反應堆頁面可見，一個最小化 UI-thread session loop 只會繼續更新模擬物理、自動起動進度、任務時鐘同如實公開狀態 API。合成音效同所有真實世界整合仍然由頁面持有：隱藏頁唔可以繼續令電腦保持喚醒、鏡像 Home Assistant、保留 Windows SystemLink 改動，或者帶住真實關機 ARM。關閉最後一個反應堆頁面會自動解除 ARM 並取消任何未完成倒數、將 Home Assistant entity 發佈為 off、釋放 Awake、還原 SystemLink 原值，同停止反應堆音效；背景模擬／API 就會繼續。已保存嘅可選設定仍然保留，但唔會靜雞雞繼續造成真實影響。

**EN —** An ARM request made from Reactor Settings while no control room is visible stays harmless: WinForge returns to Reactor, waits until the destination control room has finished loading, then uses a low-priority UI callback to arm only when the ABORT control can be exposed. The `--auto-start-reactor` command-line option likewise targets the canonical session and applies its startup preset at most once per app session, so reopening Reactor cannot replay it.

**粵語 —** 冇控制室可見時由反應堆設定提出 ARM，要求會保持無害：WinForge 會返回反應堆，等目的地控制室完成載入，再用低優先次序 UI callback，肯定可以顯示 ABORT 控制先 ARM。`--auto-start-reactor` 命令列選項同樣針對正式 session，而且每個 app session 最多只套用一次起動 preset，所以重開反應堆唔會重播。

---

## Meltdown → real shutdown (ARM toggle) · 熔毀 → 真實關機（ARM 開關）

**EN —** The **ARM REAL SHUTDOWN** toggle is **OFF by default** and can be active only while a Reactor control room is visible. With it off, a meltdown only shows a simulated overlay and the message *"Real shutdown is OFF — your PC is safe."* If explicitly armed, a meltdown starts one **session-global, 10 s abortable deadline** for an actual Windows shutdown. The deadline belongs to the current foreground authority: opening another Reactor page/window, handing authority back to an older one, or closing the last control room automatically aborts it; the operator must explicitly arm again for any new countdown. State is flushed *before* the request is issued. Whether Windows accepts or refuses that request is stored as truthful session-global state: acceptance hides ABORT because the accepted OS request can no longer be cancelled, while refusal shows a failure and will not retry until the operator disarms and explicitly re-arms.

**粵語 —** **ARM REAL SHUTDOWN** 開關**預設 OFF**，而且只可以喺反應堆控制室可見時生效。關閉時，熔毀只播模擬畫面同顯示*「Real shutdown is OFF — your PC is safe.」*。明確 ARM 後熔毀會為真實 Windows 關機開一個**全 session 共用、10 秒可中止期限**。期限只屬於當時嘅前景 authority：開另一個反應堆頁面／視窗、將 authority 交返舊頁，或者關閉最後一個控制室，都會自動中止；要有新倒數，操作員必須明確重新 ARM。發出要求*之前*會先寫入狀態。Windows 接受定拒絕要求都會保存成如實嘅全 session 狀態：接受後會收起 ABORT，因為已獲 OS 接受嘅要求唔再可以取消；拒絕就會顯示失敗，而且要先解除武裝再明確重新 ARM 先會重試。

---

## Keep PC awake while generating · 發電時保持喚醒

**EN —** When the generator is on-load and a Reactor page is visible, the reactor holds the PC awake (the keep-awake pill turns gold). It **releases the instant you SCRAM or trip** the generator offline, and also when the last Reactor page closes—even though background simulation and API publication continue.

**粵語 —** 發電機併網兼有反應堆頁面可見時，反應堆會保持電腦喚醒（喚醒指示變金色）。一旦你 **SCRAM 或跳脫**令發電機離線，或者關閉最後一個反應堆頁面，都會即刻放開——就算背景模擬同 API 發佈繼續，電腦亦再次可以正常睡眠。

---

## Reactor ↔ Windows-settings linkage · 反應堆 ↔ Windows 設定連動

**EN —** An **opt-in, reversible** linkage can tie reactor state to Windows settings. It is never silent: it is opt-in, has a visible switch, and can be undone. Its original values are restored when the last Reactor page closes. The **Always-On Reactor** option (also opt-in) registers a logon task so the reactor relaunches if closed — it has a clearly visible OFF switch and is never hidden or unkillable.

**粵語 —** 一個**可選、可逆轉**嘅連動可以將反應堆狀態同 Windows 設定綁定。它從不靜默：可選、有明顯開關、可撤銷；關閉最後一個反應堆頁面時會還原原本值。**常駐反應堆**選項（同樣可選）會註冊登入工作，令反應堆關閉後重新啟動——它有明顯嘅關閉開關，永遠唔會隱藏或無法終止。

---

## Crash-safe autosave · 防崩潰自動儲存

**EN —** A crash/shutdown-safe autosave snapshots reactor state (power, precursors, temps, pressures, xenon, rods, boron, mode, setpoints, alarms) every few seconds with **atomic writes + a `.bak` fallback**, and flushes on app exit, crash, session-ending, and **before** any armed real shutdown — so a reopened reactor resumes where it left off.

**粵語 —** 防崩潰／關機自動儲存每隔幾秒快照反應堆狀態（功率、先驅核、溫度、壓力、氙、控制棒、硼、模式、設定點、警報），採用**原子寫入加 `.bak` 後備**，並喺 app 退出、崩潰、工作階段結束，以及任何已開啟嘅真實關機**之前**寫入——所以重開嘅反應堆會由上次嘅位置續行。

---

## Public status API · 公開狀態 API

**EN —** The reactor publishes a public status feed so **other apps can depend on it**. A lightweight client, [`Sdk/ReactorStatusClient.cs`](../../Sdk/ReactorStatusClient.cs), reads the live status (mode, power, alarms, etc.) without coupling to the WinUI app. The minimal session loop keeps physics and this truthful feed current after the last Reactor page closes; it does not synthesize nuclear output from the optional EDG and does not keep any page-owned real-world integration alive.

**粵語 —** 反應堆發布公開狀態饋送，畀**其他 app 依賴它**。一個輕量客戶端 [`Sdk/ReactorStatusClient.cs`](../../Sdk/ReactorStatusClient.cs) 讀取即時狀態（模式、功率、警報等），無需同 WinUI app 耦合。關閉最後一個反應堆頁面後，最小化 session loop 仍會更新物理同呢個如實饋送；佢唔會用可選 EDG 假扮核電輸出，亦唔會令任何頁面持有嘅真實世界整合繼續運作。

---

### Reactor pages · 反應堆頁面導覽
[🏠 Reactor Hub · 反應堆總覽](Nuclear-Reactor.md) · [Overview · 總覽](Reactor-Overview.md) · [Control Room · 控制室](Reactor-Control-Room.md) · [Operating Procedures · 操作程序](Reactor-Operating-Procedures.md) · [Emergencies & Scenarios · 緊急與情景](Reactor-Emergencies-and-Scenarios.md) · [Fuel & Waste · 燃料與廢料](Reactor-Fuel-and-Waste.md) · [Water Treatment · 水處理](Reactor-Water-Treatment.md) · [Safety & Integrations · 安全與整合](Reactor-Safety-and-Integrations.md) · [Operating Manual · 操作手冊](Nuclear-Reactor-Operating-Manual.md) · [Test Report · 測試報告](Reactor-Test-Report.md)

*English + 繁體中文／粵語*
