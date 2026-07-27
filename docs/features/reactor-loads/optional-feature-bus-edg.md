# Optional feature-bus emergency diesel · 可選功能匯流排應急柴油

## Purpose · 用途

**EN —** The reactor dependency is deliberately a playful gate for a small set of WinForge features. Live nuclear generation remains the preferred source, but the operator may explicitly allow a simulated emergency diesel generator (EDG) to energize that local feature bus when nuclear power is unavailable.

**粵語 —** 一小撮 WinForge 功能嘅反應堆相依本身係玩味閘門。即時核電仍然係首選；不過核電不可用時，操作員可以明確容許一部模擬應急柴油發電機（EDG），為本機功能匯流排頂住先——唔使每次開個工具都先考核電牌。

This is a local game mechanic. It does **not** start or control a real generator, change a Windows power plan, actuate hardware, or alter the public reactor-status feed. · 呢個只係本機遊戲機制，**唔會**啟動或控制真實發電機、改 Windows 電源計劃、驅動硬件，亦唔會竄改公開反應堆狀態。

## Policy and lifecycle · 政策同生命週期

| Contract · 合約 | Behaviour · 行為 |
|---|---|
| Default · 預設 | Emergency-diesel fallback is **OFF**. Strict nuclear gating therefore remains the default. · 應急柴油後備預設係 **OFF**，所以預設仍然要核電。 |
| Persisted permission · 持久權限 | The **Allow emergency-diesel fallback** choice is saved and restored on the next app launch. Turning it off immediately stops the simulated EDG, releases every lease, and clears its session fuel. · **容許應急柴油後備**嘅選擇會保存並喺下次開 app 還原；關閉權限亦會即刻停止模擬 EDG、釋放全部 lease，同清空今次 session 嘅燃油。 |
| Manual fuel and start · 手動入油同啟動 | Permission never fuels or starts the EDG. Every app session begins empty and stopped. **Fill diesel tank** is available only while stopped and fills the simulated tank to **60 L**. The operator must then press **Start emergency diesel** and wait the full **10-second** start sequence before fallback power is available. · 有權限都唔會自動入油或者撻着 EDG；每次 app session 都由空缸兼停機開始。**為柴油缸入滿油**只會喺停機時提供，並將模擬油缸加到 **60 L**；之後操作員要按 **啟動應急柴油發電機**，再等足 **10 秒**先有後備電。 |
| Session-only state · 只限今次 session | Fuel quantity, starting/running progress, and owner-token module leases are not persisted. The EDG burns **1.0 L/min while starting or running**. Fuel exhaustion stops it and releases its leases. A manual stop also releases leases but retains the remaining session fuel for a later manual restart. · 燃油量、啟動／運行進度同 owner-token 模組 lease 都唔會保存。EDG **啟動中或者運行中每分鐘耗油 1.0 L**；燒光油就停機兼釋放 lease。手動停機亦會釋放 lease，但會保留今次 session 餘下嘅油，之後可以再手動撻機。 |
| Source priority · 電源優先次序 | A healthy live nuclear snapshot that meets the selected entry's threshold always wins. EDG is evaluated only when that nuclear path is unavailable. · 健康即時核電只要達到所選項目門檻就一定優先；核電路徑不可用時先會評估 EDG。 |
| Capacity and leases · 容量同 lease | The EDG's **250 MWe** rating is checked against each module's threshold. Separately, at most **two concurrently open module tabs/instances** may hold owner-token EDG leases. Rechecking the same module in the same tab is idempotent; navigating that tab swaps or releases its lease. A third EDG-backed tab waits until an existing owner closes, navigates away, or otherwise releases its slot. · EDG 嘅 **250 MWe** 額定值會逐模組門檻檢查；另外，同時最多只可有**兩個已開模組分頁／instance**持有 owner-token EDG lease。同一分頁重試同一模組唔會重複佔位；分頁轉頁時會交換或者釋放 lease。第三個 EDG 分頁要等現有 owner 關閉、轉頁或者釋放個位。 |

The controls live on **Reactor Settings** (`WinForge.exe --page reactorsettings`) and on the feature-power recovery page shown when an eligible entry has no usable source. · 控制喺**反應堆設定**（`WinForge.exe --page reactorsettings`），亦會喺合資格項目冇可用電源時顯示嘅功能電源復原頁出現。

## Headless visual evidence · 無頭視覺證據

All captures below come from the real self-contained WinUI build on dedicated off-screen desktops; the automation never switched to those desktops, opened a visible terminal, or stole focus. · 以下截圖全部由真實 self-contained WinUI build 喺專用離屏桌面擷取；自動化冇切換去嗰啲桌面、冇彈出可見終端，亦冇搶焦點。

| State · 狀態 | Evidence · 證據 |
|---|---|
| Fallback default OFF · 後備預設關閉 | [Reactor Settings](../../screenshot-reactorsettings.png) |
| Permission enabled, empty tank · 已容許、空油缸 | [Enabled state](../../screenshot-reactorsettings-enabled.png) |
| Manual fill followed by 10-second start · 手動入油後 10 秒啟動 | [Starting state](../../screenshot-reactorsettings-starting.png) |
| Running with fuel and 0/2 lease telemetry · 運行、有油、0/2 lease telemetry | [Running state](../../screenshot-reactorsettings-running.png) |
| No-source recovery page · 冇電源復原頁 | [Feature-power recovery](../../screenshot-reactor-feature-power-required.png) |
| Search result requirement badge · 搜尋結果電源徽章 | [Search results](../../screenshot-reactor-feature-power-search.png) |
| Cake Factory on one exact-owner EDG lease · 蛋糕工廠佔用一個準確 owner EDG lease | [Powered Cake Factory](../../screenshot-cakefactory-feature-power.png) |
| Corrected Reactor toolbar with all controls visible · 修正後全部控制可見嘅反應堆工具列 | [Reactor control surface](../../screenshot-reactor.png) |

## Eligible feature gates · 合資格功能閘門

Eight ordinary modules can hold one of the two owner-token EDG lease slots while their tab/instance is open. The source and 250 MWe per-module threshold are checked when entry is attempted. If both slots belong to other open module instances, the next tab stays on the recovery page until a slot is released. · 八個一般模組嘅分頁／instance 開住期間可以佔用兩個 owner-token EDG lease 位其中一個；嘗試進入時會檢查電源同逐模組 250 MWe 門檻。如果兩個位已經畀其他已開模組 instance 佔用，下一個分頁會留喺復原頁，等有位釋放。

| Module · 模組 | Nuclear entry threshold · 核電進入門檻 | EDG behaviour · EDG 行為 |
|---|---:|---|
| Ollama · 本地大模型 | 80 MWe | Holds one lease while EDG-backed and open · 用 EDG 開住時佔一個 lease |
| Blender (3D / Render) · Blender（3D／算圖） | 180 MWe | Holds one lease while EDG-backed and open · 用 EDG 開住時佔一個 lease |
| Docker | 55 MWe | Holds one lease while EDG-backed and open · 用 EDG 開住時佔一個 lease |
| WSL & VM Launcher · WSL 與 VM 啟動器 | 120 MWe | Holds one lease while EDG-backed and open · 用 EDG 開住時佔一個 lease |
| VirtualBox Manager · VirtualBox 管理 | 150 MWe | Holds one lease while EDG-backed and open · 用 EDG 開住時佔一個 lease |
| Packer (Image Builder) · Packer（映像建置器） | 210 MWe | Holds one lease while EDG-backed and open · 用 EDG 開住時佔一個 lease |
| Minecraft Server · Minecraft 伺服器 | 65 MWe | Holds one lease while EDG-backed and open · 用 EDG 開住時佔一個 lease |
| Android Emulator · Android 模擬器 | 95 MWe | Holds one lease while EDG-backed and open · 用 EDG 開住時佔一個 lease |

**Cake Factory & Farm · 蛋糕工廠與農場** has a 35 MWe threshold and is different: while EDG-backed and open, it occupies one lease and consumes feature-bus power continuously. Its in-process snapshot uses healthy nuclear generation when available, otherwise a running, fuelled, permitted EDG. Stopping the EDG or exhausting its fuel immediately removes fallback power; powered farm machinery, factory, bakery, utility, cold-chain equipment, and dispatch controls pause or lock out. Passive crop/animal condition, milk warming and bacterial/spoilage risk, transport, and order deadlines continue to evolve. Merely entering the page earlier is not enough. · 蛋糕工廠與農場嘅門檻係 35 MWe，而且玩法唔同：用 EDG 開住時會佔一個 lease，並持續消耗功能匯流排電力。程式內快照有健康核電就用核電，否則先用已容許、已入油兼運行中嘅 EDG。停機或者燒光油會即刻移除後備電；有電農場機器、工廠、烘焙、公用工程、冷鏈設備同出貨控制會暫停或鎖住，但被動農作／動物狀況、牛奶升溫同細菌／變壞風險、運輸同訂單期限會繼續變化。之前入到頁都唔代表可以無油焗蛋糕。

Cake Factory is deliberately not navigated away or reconstructed when its exact owner token loses power. The live page and its in-memory plant state stay intact; powered commands are rejected and powered machinery/progress timers freeze, including an in-progress CIP wash/rinse/drain cycle. Passive simulation clocks do not freeze: unchilled milk warms and bacterial/spoilage risk grows, biological condition continues to evolve, cow/supply transport still advances at its unpowered rate, and customer order deadlines keep counting down. When that same tab regains healthy nuclear power or an EDG outlet, powered work resumes from the preserved state. Another tab's lease cannot power it. · 蛋糕工廠嘅準確 owner token 失電時，WinForge 刻意唔會將頁面導走或者重建。即時頁面同記憶體內廠房狀態會完整保留；有電指令會被拒絕，有電機器／進度計時亦會凍結，包括進行中嘅 CIP 清洗／沖洗／排水週期。被動模擬時鐘唔會凍結：冇冷凍嘅牛奶會升溫並增加細菌／變壞風險、生物狀況繼續變化、奶牛／供應貨車運輸仍會按失電速度前進，而客戶訂單期限繼續倒數。同一分頁重新取得健康核電或 EDG 插槽後，有電流程會由保留狀態繼續；第二個分頁嘅 lease 唔可以借電畀佢。

## Canonical nuclear source and visible-safety boundary · 正式核電來源同可見安全界線

The preferred nuclear source does not freeze merely because the last Reactor page is closed. All Reactor pages share one canonical in-memory simulation. When no control room is visible, a minimal UI-thread loop advances only simulation physics, command-line auto-start progression, the session clock, and the truthful local status API. Page-owned audio, Home Assistant driving, keep-awake, Windows-settings linkage, and real-shutdown handling stop or restore instead of running invisibly. · 首選核電來源唔會因為最後一個反應堆頁關閉就凍結。全部反應堆頁共用一個正式記憶體內模擬；冇控制室可見時，精簡 UI-thread loop 只會推進模擬物理、命令列自動啟動流程、session 時鐘同如實本機狀態 API。頁面擁有嘅音效、Home Assistant 驅動、保持喚醒、Windows 設定連動同真實關機處理會停止或還原，唔會暗中繼續。

`WinForge.exe --auto-start-reactor` opens Reactor and applies the auto-start preset exactly once to the canonical app session. Closing and reopening the page does not reapply it. Parallel Reactor pages remain live read-only observers: gauges/status keep rendering, but plant controls and companion launchers are disabled. Authority demotion closes mutating HTML/full-control-room, startup-checklist, and SCRAM-widget companions while read-only widgets may remain. A demoted meltdown page keeps its live overlay without ABORT/reset and auto-collapses it when the authoritative driver resets the shared plant. Real-shutdown ARM is separate: it requires a fully loaded visible control room that can expose ABORT. Its deadline and accepted/refused OS result are truthful session-global state, but any foreground page/window handoff automatically aborts an active countdown; the last control-room close also disarms and resets it. Once Windows accepts the request, ABORT is hidden because cancellation is no longer possible. · `WinForge.exe --auto-start-reactor` 會開啟反應堆，並對今次正式 app session 準確套用一次自動啟動 preset；關頁再開唔會重複套用。並行開住嘅其他反應堆頁會保持即時唯讀：儀錶／狀態繼續更新，但機組控制同 companion 啟動掣會停用。Authority 降級會關閉可改動模擬嘅 HTML／完整控制室、起動 checklist 同 SCRAM widget companion，但唯讀 widget 可以留低。降級頁面嘅熔毀 overlay 會保持即時顯示但冇 ABORT／重設，authoritative driver 重設共用機組時會自動收起。真實關機 ARM 係另一條安全路徑：必須有已完成載入、可顯示 ABORT 嘅可見控制室。Deadline 同 OS 接受／拒絕結果係如實嘅全 session 狀態，但任何前景頁面／視窗交接都會自動中止進行中嘅倒數；關閉最後一個控制室亦會解除武裝兼重設。Windows 一接受要求就會收起 ABORT，因為已經唔再可以取消。

## Nuclear-only industrial simulations · 只限核電嘅工業模擬

The EDG fallback does **not** power the other **19** reactor-industrial simulations. They retain their own direct live-reactor semantics: Grid Dispatch Center · 電網調度中心; Hydrogen Electrolysis · 氫電解制氫廠; AI Training Cluster · AI 訓練叢集; Supercomputer (HPC) · 超級電腦（HPC）; Compute Mine · 運算礦場; Aluminium Smelter · 鋁冶煉廠; Nuclear Data Center · 核能資料中心; Particle Collider · 粒子對撞機; Reactor Bank · 反應堆銀行; Seawater Desalination · 海水淡化廠; EV Fast-Charge Depot · 電動車快充站; Pumped-Storage Hydro · 抽水蓄能; District Heating · 區域供熱; Carbon Capture (DAC) · 碳捕集; Vertical Farm · 垂直農場; Arc-Furnace Steel Mill · 電弧爐煉鋼廠; Electric Cement Kiln · 電熱水泥迴轉窯; Ammonia / Fertilizer Plant · 核電合成氨（肥料）廠; and Grid Load-Shed Dispatcher · 電網卸載調度台.

換句話講，EDG **唔會**幫以上 **19** 個反應堆工業模擬「搭便車」；佢哋繼續直接跟即時核電狀態運行，柴油後備只限前面列出嘅九個玩味功能閘門。

## Operator flow · 操作流程

1. Try the eligible module. If its nuclear threshold is already satisfied, WinForge opens it on nuclear power. · 嘗試開啟合資格模組；核電已達門檻就直接用核電開。
2. If the feature-power recovery page appears, either recover the reactor or explicitly allow EDG fallback. · 如果見到功能電源復原頁，可以恢復反應堆，或者明確容許 EDG 後備。
3. While the EDG is stopped, press **Fill diesel tank** to fill its 60 L simulated tank. Press **Start emergency diesel**, wait 10 seconds for rated output, then press **Retry app**. Fuel burns at 1.0 L/min throughout starting and running. · EDG 停機時按 **為柴油缸入滿油**，將模擬油缸加到 60 L；按 **啟動應急柴油發電機**，等 10 秒達額定輸出，再按 **重試 app**。啟動中同運行中都會每分鐘耗油 1.0 L。
4. Keep no more than two EDG-backed module tabs/instances open. A third waits until one of the first two closes, navigates away, or releases its owner-token lease. · 同時最多開住兩個 EDG 後備模組分頁／instance；第三個要等前面其中一個關閉、轉頁或者釋放 owner-token lease。
5. Keep the EDG fuelled and running while Cake Factory needs fallback power. If exact-owner power is lost, leave the tab open: its state is preserved while powered machinery and CIP progress freeze, then resume after the same tab reacquires power. Passive biological/spoilage/transport and order-deadline processes continue during the outage. Stop the EDG when finished; remaining fuel stays available only for this app session, and a later app session always starts empty and needs another manual fill/start. · 蛋糕工廠仲要靠後備電時要保持 EDG 有油兼運行。準確 owner 失電時可以保持分頁開住：廠房狀態會保留，而有電機器同 CIP 進度會凍結，等同一分頁重新取得電力後再繼續；被動生物／變壞／運輸同訂單期限流程喺停電期間仍會繼續。用完可以停 EDG；餘油只會留喺今次 app session，下次 session 一定由空缸開始，要再手動入油／啟動。

## Verification · 驗證

`dotnet run --project tests/ReactorSim.Tests -c Debug` currently passes **67/67** scenarios and exits 0; the fast contract confirms 67/67→0 and 66/67→1. Focused cases cover default-off strict nuclear gating, nuclear preference, manual 60 L fuelling, 1.0 L/min starting/running consumption, stopped/starting/running/fuel-exhausted and insufficient-output states, the exact 10-second transition, persisted permission versus session-only generator/fuel/owner-lease state, the nine-module threshold catalog, exact owner/module isolation, idempotent reacquisition, the two-concurrent-module-instance lease cap, atomic parallel contention with exactly two winners, third-entry waiting, ordinary-module exemption, continuous Cake Factory power loss, and exact CIP-progress freeze/resume. The separate Reactor Settings lifecycle harness passes **10/10**, covering live read-only parallel observers, plant-input lockout, mutating-companion cleanup and safe meltdown-overlay synchronization on authority demotion, the canonical foreground/background handoff, session-global truthful shutdown outcome, automatic countdown abort on foreground authority changes, last-owner restoration, post-load visible ARM request, and once-per-session command-line auto-start contracts. · 專項 harness 目前 **67/67** 全綠並以 0 退出；快速合約亦確認 67/67→0、66/67→1。覆蓋預設關閉嘅嚴格核電閘門、核電優先、手動加滿 60 L、啟動中／運行中每分鐘耗油 1.0 L、EDG 停機／啟動／運行／冇油同輸出不足、準確 10 秒轉態、持久權限對只限 session 發電機／燃油／owner lease 狀態、九個模組門檻清單、準確 owner／module 隔離、重複取得唔會重複佔位、兩個同時模組 instance lease 上限、並行爭位時準確兩個贏家、第三個等位、一般模組豁免、蛋糕工廠持續失電，以及準確 CIP 進度凍結／恢復。另一個反應堆設定生命週期 harness **10/10** 全綠，覆蓋並行頁面即時唯讀觀察、機組輸入鎖定、authority 降級時清理可改動 companion 同安全同步熔毀 overlay、正式前景／背景交接、全 session 如實關機結果、前景 authority 改變時自動中止倒數、最後 owner 還原、載入後可見 ARM 要求，同每 session 一次命令列自動啟動合約。
