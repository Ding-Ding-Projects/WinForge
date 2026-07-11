# Reactor Test Report · 反應堆測試報告

**EN —** This is the current headless verification report for the real C# reactor engine and its dependent fuel, waste, water, app-gating, and cake-factory services. The harness compiles the production service sources directly; it does not substitute a mock reactor.

**粵語 —** 呢份係現時真實 C# 反應堆引擎，同燃料、廢料、水處理、app 供電閘門、蛋糕工廠相依服務嘅無介面驗證報告。測試框架直接編譯正式服務程式碼，唔係用假反應堆代替。

**Latest verified run · 最新已驗證執行：** 2026-07-11

```powershell
dotnet run --project tests/ReactorSim.Tests -c Debug
```

## CI exit-code contract · CI 退出碼規約

**EN —** The normal harness run emits a per-scenario PASS/FAIL line and ends with an explicit summary. It exits **0 only when all scenarios pass**; any failed assertion or caught scenario exception makes the process exit **1**. CI must treat any nonzero exit as a failed reactor verification rather than relying on the printed count alone. For a fast deterministic regression of the mapping itself (without executing reactor scenarios), run:

**粵語 —** 正常測試框架運行會列印每個情景嘅 PASS／FAIL 行，同埋最後清楚嘅總結。**全部情景通過**先會退出 **0**；任何斷言失敗或者捉到嘅情景例外都會令程序退出 **1**。CI 一見到非零 exit 就要當反應堆驗證失敗，唔可以淨係靠睇列印嘅數目。想快速而確定噉回歸 mapping 本身（唔會跑反應堆情景），請行：

```powershell
dotnet run --project tests/ReactorSim.Tests -c Debug -- --verify-exit-code-contract
```

**Visual evidence · 視覺證據 —** Not applicable / 不適用. This change touches only the headless console harness and documentation; no WinUI page or visual layout changed, so no screenshot replacement is required or claimed.

**Build / harness · 建置／測試框架：** 0 compile errors · 0 個編譯錯誤

**Result · 結果： 63 / 63 scenarios PASS · 63 / 63 個情景全部通過**

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
| Reactor-dependent app gating · 反應堆相依 app 閘門 | **1** | Live reactor-bus availability and ordinary-module exemption. · 即時反應堆母線可用性，同普通模組豁免。 |
| Cake-factory dependency chain · 蛋糕工廠相依鏈 | **37** | Reactor power gate, manual production, ingredient provenance, factory processes, QA, maintenance, dispatch, signed files, credits, and sanitation. · 反應堆供電閘門、手動生產、原料來源、工廠流程、品質檢驗、維修、出貨、簽署檔案、額度同清潔。 |
| **Total · 總數** | **63** | **All pass · 全部通過** |

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
[Reactor Hub · 反應堆總覽](Nuclear-Reactor.md) · [Overview · 總覽](Reactor-Overview.md) · [Control Room · 控制室](Reactor-Control-Room.md) · [Operating Procedures · 操作程序](Reactor-Operating-Procedures.md) · [Emergencies & Scenarios · 緊急與情景](Reactor-Emergencies-and-Scenarios.md) · [Fuel & Waste · 燃料與廢料](Reactor-Fuel-and-Waste.md) · [Water Treatment · 水處理](Reactor-Water-Treatment.md) · [Safety & Integrations · 安全與整合](Reactor-Safety-and-Integrations.md) · [Operating Manual · 操作手冊](Nuclear-Reactor-Operating-Manual.md)

*English + 繁體中文／粵語*
