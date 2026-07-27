# Reactor Test Report · 反應堆測試報告

**EN —** This is the current headless verification report for the real C# reactor engine and its dependent fuel, waste, water, feature-power/EDG gating, industrial-load, and cake-factory services, plus the focused Reactor Settings/session lifecycle contract. The harnesses compile or inspect the production service/page sources directly; they do not substitute a mock reactor.

**粵語 —** 呢份係現時真實 C# 反應堆引擎，同燃料、廢料、水處理、功能電源／EDG 閘門、工業負載、蛋糕工廠相依服務，加埋專項反應堆設定／session 生命週期合約嘅無介面驗證報告。測試框架會直接編譯或者檢查正式服務／頁面程式碼，唔係用假反應堆代替。

**Latest verified run · 最新已驗證執行：** 2026-07-27

```powershell
dotnet run --project tests/ReactorSim.Tests -c Debug
dotnet run --project tests/ReactorSettingsLifecycle.Tests -c Debug
```

## CI exit-code contract · CI 退出碼規約

**EN —** The normal harness run emits a per-scenario PASS/FAIL line and ends with an explicit summary. It exits **0 only when all scenarios pass**; any failed assertion or caught scenario exception makes the process exit **1**. CI must treat any nonzero exit as a failed reactor verification rather than relying on the printed count alone. For a fast deterministic regression of the mapping itself (without executing reactor scenarios), run:

**粵語 —** 正常測試框架運行會列印每個情景嘅 PASS／FAIL 行，同埋最後清楚嘅總結。**全部情景通過**先會退出 **0**；任何斷言失敗或者捉到嘅情景例外都會令程序退出 **1**。CI 一見到非零 exit 就要當反應堆驗證失敗，唔可以淨係靠睇列印嘅數目。想快速而確定噉回歸 mapping 本身（唔會跑反應堆情景），請行：

```powershell
dotnet run --project tests/ReactorSim.Tests -c Debug -- --verify-exit-code-contract
```

**2026-07-24 industrial-load visual evidence · 2026-07-24 工業負載視覺證據 —** `capture-blocked`. Both `ammonia` and `loadshed` launched in fresh WinUI windows on a dedicated LowLevel headless desktop, but the inspected 1574×887 client captures were solid black. The repository driver independently rejected a blank/near-uniform fallback, and switching the headless desktop visible failed with access denied. No invalid capture is published or claimed as a visual pass. · 兩頁都成功喺專用 LowLevel headless desktop 開出新 WinUI 視窗，但已檢視嘅 1574×887 client capture 全黑；repo driver 亦獨立拒絕空白／近乎單色 fallback，而切換到可見 desktop 就 access denied。冇無效圖片會發佈或者當視覺通過。

**Build / harness · 建置／測試框架：** 0 compile errors · 0 個編譯錯誤

**Result · 結果： 67 / 67 scenarios PASS · 67 / 67 個情景全部通過**

**Focused lifecycle result · 專項生命週期結果： 10 / 10 tests PASS · 10 / 10 項測試全部通過**

**EN —** The lifecycle harness is a safe source-contract test. It proves ownership, ordering, and cleanup without starting Home Assistant, changing Windows settings, keeping the PC awake, or requesting a real OS shutdown.

**粵語 —** 生命週期 harness 係安全嘅原始碼合約測試；佢會證明 ownership、次序同清理，而唔會啟動 Home Assistant、改 Windows 設定、令電腦保持喚醒，或者提出真實 OS 關機要求。

> **EN —** The original P1–P5 realism findings are resolved. The suite now proves both ends of the operating envelope: a fresh fully-rodded core stays subcritical at **−1018 pcm**, and a fully hot plant sustains a high-power thermal equilibrium without emergency cooling, SCRAM, runaway, or meltdown.
>
> **粵語 —** 最初 P1–P5 寫實度問題已解決。測試而家驗證運行範圍兩端：新鮮爐心全棒插入時保持 **−1018 pcm** 次臨界；全熱機組亦可以持續維持高功率熱平衡，唔需要應急冷卻、唔會 SCRAM、唔會失控、唔會熔毀。

---

## Coverage summary · 覆蓋摘要

| Group · 組別 | Passing scenarios · 通過數目 | What is covered · 覆蓋內容 |
|---|---:|---|
| Reactor physics, startup, persistence, and protection · 反應堆物理、起動、持久化、保護 | **17** | Cold hold, backward-Euler stability, normal/easy/automatic startup, sustained at-power balance, snapshot restore, SCRAM, shutdown margin, decay heat, overpower, and xenon. · 冷停堆保持、後向歐拉穩定性、正常／簡易／自動起動、持續高功率平衡、快照還原、SCRAM、停堆裕度、衰變熱、超功率同氙暫態。 |
| Accident injection coverage · 事故注入覆蓋 | **1** | Directly exercises and asserts all **16** `ReactorScenario` enum values. · 直接執行同斷言全部 **16** 個 `ReactorScenario` enum 值。 |
| Fuel lifecycle · 燃料生命週期 | **3** | Fabricate/validate/tamper, load-consumes-file, and forged-fuel harm versus safe inspection. · 製造／驗證／竄改、入料即刪檔、偽冒燃料損堆同安全檢查。 |
| Waste storage safety · 廢料儲存安全 | **2** | Capacity cap and disk free-space floor. · 容量上限同磁碟剩餘空間安全下限。 |
| Water treatment · 水處理 | **2** | Ultrapure chemistry and empty-tank availability degradation. · 超純水化學同水箱耗盡後可用性下降。 |
| Feature-power and reactor-dependent app gating · 功能電源同反應堆相依 app 閘門 | **3** | Default-off strict nuclear gating, nuclear preference, all nine per-module thresholds, ordinary-module exemption, stopped-only manual 60 L diesel fill, 1.0 L/min starting/running consumption, stopped/starting/running/fuel-exhausted states, exact 10-second manual start, persisted permission versus session-only generator/fuel/owner leases, exact owner/module isolation, idempotent reacquisition, the two-concurrent-module-instance lease cap, atomic parallel contention with exactly two winners, third-entry waiting, and Cake Factory's continuous exact-owner fallback snapshot. · 預設關閉嘅嚴格核電閘門、核電優先、全部九個逐模組門檻、普通模組豁免、只限停機時手動加滿 60 L、啟動中／運行中每分鐘耗油 1.0 L、EDG 停機／啟動／運行／燃油耗盡、準確 10 秒手動啟動、持久權限對只限 session 發電機／燃油／owner lease、準確 owner／module 隔離、重複取得唔會重複佔位、兩個同時模組 instance lease 上限、並行爭位時準確兩個贏家、第三個等位，以及蛋糕工廠持續準確 owner 後備快照。 |
| Reactor industrial loads · 反應堆工業負載 | **2** | Ammonia pressurisation/production/power-loss behavior and strict-priority feeder dispatch, cold-bus accounting, unserved energy, anti-flap reclose, and duplicate-tick stability. · 合成氨加壓／生產／失電行為，同嚴格優先級饋線調度、冷母線計數、未供電能量、防拍翼重合閘、重複 tick 穩定性。 |
| Cake-factory dependency chain · 蛋糕工廠相依鏈 | **37** | Continuous feature-bus power gating, page/model preservation on exact-owner source loss, powered-action lockout, exact active-CIP progress freeze/resume, manual production, ingredient provenance, factory processes, QA, maintenance, dispatch, signed files, credits, and sanitation. · 持續功能匯流排供電閘門、準確 owner 失電時保留頁面／模型、有電動作鎖定、進行中 CIP 精準進度凍結／恢復、手動生產、原料來源、工廠流程、品質檢驗、維修、出貨、簽署檔案、額度同清潔。 |
| **Total · 總數** | **67** | **All pass · 全部通過** |

---

## Key reactor evidence · 主要反應堆證據

| Scenario · 情景 | Result · 結果 | Measured evidence · 量度證據 |
|---|---|---|
| Cold-shutdown held · 冷停堆保持 | PASS · 通過 | Five minutes at source level; MODE 5, 35 °C fuel, no meltdown. · 五分鐘維持源中子水平；MODE 5、燃料 35 °C、冇熔毀。 |
| Startup integrator stability · 起動積分穩定性 | PASS · 通過 | Backward-Euler remains finite, has no sign oscillation, and does not hit the numerical clamp. · 後向歐拉保持有限值、冇正負振盪、冇撞數值上限。 |
| Fully-rodded startup margin · 全棒插入起動裕度 | PASS · 通過 | Fresh core reads **−1018 pcm**, remains subcritical, and accumulates no damage. · 新鮮爐心讀數 **−1018 pcm**，保持次臨界，冇累積損傷。 |
| Sustained high-power equilibrium · 持續高功率平衡 | PASS · 通過 | After full-plant settling, **0.836→0.835 RTP** over eight observed minutes; fuel **992.4→992.5 °C**; Tavg **293.4 °C**; RCS **15.46 MPa**; reactivity approximately 0 pcm. No ECCS, accumulator injection, SCRAM, or meltdown. · 全機組穩定後觀察八分鐘：**0.836→0.835 RTP**；燃料 **992.4→992.5 °C**；Tavg **293.4 °C**；RCS **15.46 MPa**；反應性約 0 pcm。冇 ECCS、冇蓄壓器注入、冇 SCRAM、冇熔毀。 |
| SCRAM mechanism · 緊急停堆機構 | PASS · 通過 | Trip latches, release delay holds, then gravity rod-drop begins; rods do not unrealistically snap in on one tick. · 跳脫鎖定、釋放延遲成立，之後控制棒靠重力落下；唔會一個 tick 瞬間全插。 |
| SCRAM shutdown margin · SCRAM 停堆裕度 | PASS · 通過 | Fully-rodded tripped core remains **−1018 pcm** and does not melt. · 跳堆後全棒插入保持 **−1018 pcm**，唔會熔毀。 |
| Decay heat and xenon · 衰變熱同氙 | PASS · 通過 | Decay heat charges at power and decays after trip; `XenonRestart` preserves and decays the axial xenon peak. · 衰變熱喺功率運行時累積、跳堆後衰減；`XenonRestart` 會保留並衰減軸向氙峰。 |
| Protection and accidents · 保護同事故 | PASS · 通過 | Power Range Flux Hi initiates automatic SCRAM; every one of the 16 accident/training enum values is exercised. · 高功率量程中子通量會自動 SCRAM；全部 16 個事故／訓練 enum 值都有執行。 |
| Canonical session & visible safety · 正式 session 同可見安全 | PASS · 通過 | The separate **10/10** lifecycle harness proves one shared simulation, newest-visible sole driver, live read-only parallel observers whose gauges/status keep rendering with plant inputs disabled, and a minimal no-page UI-thread loop for physics, auto-start progression, clock, and truthful API only. Authority demotion closes mutating HTML/full-control-room, startup-checklist, and SCRAM-widget companions while read-only widgets may remain; its live meltdown overlay exposes no ABORT/reset and auto-collapses after the driver resets shared state. Page-owned audio/Home Assistant/Awake/SystemLink/real-shutdown effects stop or restore after the last visible owner. Off-page ARM is consumed only after the destination has loaded and a low-priority callback can expose ABORT. The shutdown deadline plus accepted/refused OS outcome are session-global and truthful, but any foreground handoff automatically aborts an active countdown before authority changes. OS acceptance hides ABORT because cancellation is no longer possible; refusal persists without automatic retry. Command-line auto-start applies once per session. · 獨立 **10/10** 生命週期 harness 證明一個共用模擬、最新可見頁做唯一 driver、並行頁面保持即時唯讀觀察（儀錶／狀態繼續更新而機組輸入停用），以及冇頁面時只為物理、自動起動進度、時鐘同如實 API 運行嘅最小 UI-thread loop。Authority 降級會關閉可改動模擬嘅 HTML／完整控制室、起動 checklist 同 SCRAM widget companion，但唯讀 widget 可以留低；降級頁面嘅熔毀 overlay 冇 ABORT／重設，而且 driver 重設共用狀態後會自動收起。最後可見 owner 離開後，頁面持有嘅音效／Home Assistant／Awake／SystemLink／真實關機效果會停止或還原。離頁 ARM 只會喺目的地載入完成兼低優先次序 callback 可以顯示 ABORT 後消耗。關機 deadline 同 OS 接受／拒絕結果係如實嘅全 session 狀態，但任何前景交接都會喺 authority 改變之前自動中止進行中倒數。OS 接受後會收起 ABORT，因為已經唔可以取消；拒絕會保留而唔會自動重試。命令列自動起動每 session 只套用一次。 |
| Feature-power source and leases · 功能電源選擇同 lease | PASS · 通過 | With fallback off, all nine gates retain strict nuclear behavior. With fallback allowed, healthy threshold-satisfying nuclear power remains preferred. Each module must fit the EDG's 250 MWe threshold; at most two concurrently open module tabs/instances receive exact owner/module EDG leases, and a third waits until a slot is released. Duplicate acquisition by the same owner is idempotent; a 64-way parallel contention test admits exactly two owners (`parallelWins=2/2`). · 後備關閉時九個閘門全部保持嚴格核電行為；容許後備時，達門檻嘅健康核電仍然優先。每個模組都要符合 EDG 嘅 250 MWe 門檻；同時最多兩個已開模組分頁／instance 取得準確 owner／module EDG lease，第三個要等有位釋放。同一 owner 重複取得唔會重複佔位；64 路並行爭位測試準確只容許兩個 owner（`parallelWins=2/2`）。 |
| Feature-bus EDG lifecycle · 功能匯流排 EDG 生命週期 | PASS · 通過 | Fallback permission defaults off and is the only persisted EDG setting. Every fresh session starts empty, stopped, and without leases; only a stopped EDG accepts a manual fill to 60 L. The state reaches running only after exactly 10.000 seconds, burns 1.0 L/min while starting or running, and loses fallback output when stopped or empty. Fuel exhaustion clears leases; manual stop retains remaining session fuel, while disabling fallback clears it. · 後備權限預設關閉，而且係唯一會保存嘅 EDG 設定。每個新 session 都由空缸、停機、冇 lease 開始；只可以喺 EDG 停機時手動加滿到 60 L。狀態準確等到 10.000 秒先進入運行，啟動中或者運行中每分鐘耗油 1.0 L，停機或者冇油就失去後備輸出。燃油耗盡會清 lease；手動停機保留今次 session 餘油，而關閉後備權限就會清空。 |
| Cake Factory EDG power · 蛋糕工廠 EDG 供電 | PASS · 通過 | A permitted, fuelled, running EDG with the exact Cake Factory owner lease creates its private 250 MWe in-process feature-bus snapshot. Stop or fuel exhaustion removes that continuous power immediately without navigating away or discarding factory state: powered machinery/actions lock and powered progress—including CIP wash/rinse/drain—freezes at its exact state, then resumes when that same owner regains power. The focused equality assertion covers active-CIP progress; the production model deliberately does **not** freeze passive biological change, milk warming/bacterial spoilage risk, cow/supply transport, or customer order clocks. Another tab cannot piggyback. Live nuclear remains preferred, the public reactor snapshot is not rewritten, and the other 19 reactor-industrial simulations remain nuclear-only. · 已容許、有油、運行中兼有準確蛋糕工廠 owner lease 嘅 EDG 會建立私有 250 MWe 程式內功能匯流排快照；停機或者燃油耗盡會即刻移除持續供電，但唔會導走頁面或者丟棄廠房狀態：有電機器／動作會鎖住，而包括 CIP 清洗／沖洗／排水在內嘅有電進度會停喺準確狀態，同一 owner 恢復供電後先繼續。專項相等檢查針對進行中 CIP 進度；production model 刻意**唔會**凍結被動生物變化、牛奶升溫／細菌變壞風險、奶牛／供應運輸或客戶訂單時鐘。另一分頁唔可以借 lease 搭便車。核電繼續優先，公開反應堆快照唔會被改寫，其餘 19 個反應堆工業模擬繼續只用核電。 |
| Green-ammonia plant · 綠氨工廠 | PASS · 通過 | The 280 MW default reaches synthesis pressure, production and CO₂ accounting advance only on distinct ticks, reactor loss stops output and depressurises the loop, and reset restores safe defaults. · 280 MW 預設值可到合成壓力；產量同 CO₂ 只會喺唔重複 tick 推進；反應堆失電會停產降壓，重設會還原安全預設。 |
| Grid load shedding · 電網卸載 | PASS · 通過 | A cold bus reports 990 MW shed with no invented trip; healthy power serves all demand; sag preserves P1/P2 while shedding 640 MW; energy, anti-flap reclose, operator-off, blackout, reset, and duplicate ticks follow contract. · 冷母線顯示 990 MW 已卸載但唔虛構跳脫；健康供電滿足全部需求；下跌時保留 P1/P2 並卸載 640 MW；能量、防拍翼重合閘、操作員關閉、全黑、重設同重複 tick 都符合合約。 |

---

## Thermal-balance regression · 熱平衡回歸

**EN —** The P3 correction uses engineering-unit aggregate coefficients: fuel→coolant conductance **4.3 MW/°C**, four-loop steam-generator conductance **4 + 39·RCS-flow MW/°C** (43 MW/°C at full flow), fuel heat capacity **30 MW·s/°C**, and coolant heat capacity **60 MW·s/°C**. At the rated design point these terms can carry the 3411 MW core output across plausible temperature gradients. The sustained test then verifies the coupled model actually settles rather than merely matching a static calculation.

**粵語 —** P3 修正採用有工程單位嘅總體係數：燃料→冷卻劑熱導 **4.3 MW/°C**、四迴路蒸汽產生器熱導 **4 + 39·RCS 流量 MW/°C**（滿流量係 43 MW/°C）、燃料熱容量 **30 MW·s/°C**、冷卻劑熱容量 **60 MW·s/°C**。喺額定設計點，呢啲項目可以用合理溫差帶走 3411 MW 爐心輸出；持續測試再驗證耦合模型真係會穩定落嚟，唔係只啱一條靜態算式。

---

## Historical defect status · 歷史缺陷狀態

**EN —** Older lower-count and open-defect status reports are obsolete. The original technical findings are retained only as an archival baseline in [Reactor Realism Review #001](../reactor-realism-review-001.md), with a current P1–P5 disposition at the top.

**粵語 —** 舊有較低通過數目同未完成缺陷狀態已經過時。原始技術發現只保留喺 [Reactor Realism Review #001](../reactor-realism-review-001.md) 做歷史基準，文件頂部已有現時 P1–P5 處理狀態。

---

### Reactor pages · 反應堆頁面導覽
[Reactor Hub · 反應堆總覽](Nuclear-Reactor.md) · [Overview · 總覽](Reactor-Overview.md) · [Control Room · 控制室](Reactor-Control-Room.md) · [Operating Procedures · 操作程序](Reactor-Operating-Procedures.md) · [Emergencies & Scenarios · 緊急與情景](Reactor-Emergencies-and-Scenarios.md) · [Fuel & Waste · 燃料與廢料](Reactor-Fuel-and-Waste.md) · [Water Treatment · 水處理](Reactor-Water-Treatment.md) · [Industrial Loads · 工業負載](Reactor-Industrial-Loads.md) · [Safety & Integrations · 安全與整合](Reactor-Safety-and-Integrations.md) · [Operating Manual · 操作手冊](Nuclear-Reactor-Operating-Manual.md)

*English + 繁體中文／粵語*
