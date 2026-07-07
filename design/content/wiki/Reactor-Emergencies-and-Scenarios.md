# Reactor Emergencies & Scenarios · 反應堆緊急與情景

**EN —** This page covers emergency response — the SCRAM / reactor trip and the E-0 Emergency Operating Procedure, meltdown and its recovery — plus the scenario drills you can inject from the **Scenario** selector to practice classic PWR transients.

**粵語 —** 呢頁涵蓋緊急應對——SCRAM／反應堆跳脫同 E-0 緊急操作程序、熔毀同其復原——以及你可由**情景選擇器**注入嘅情景演習，用嚟練習典型 PWR 瞬態。

---

## SCRAM (reactor trip) & E-0 · 緊急停堆與 E-0

**EN —** Press **SCRAM — EMERGENCY SHUTDOWN** (or let an automatic trip fire). All rods drop, inserting large negative reactivity; the annunciators light **first-out**. Then follow **E-0 (Reactor Trip / Safety Injection)**: verify rods in, verify turbine/feedwater response, verify safety-injection criteria, monitor the **Critical Safety Functions**.

**粵語 —** 揿 **SCRAM — EMERGENCY SHUTDOWN**（或等自動跳脫觸發）。所有棒落下，插入大量負反應性；警示磚顯示**首發**。然後跟 **E-0（反應堆跳脫／安全注入）**：確認棒已插入、確認渦輪／給水反應、確認安全注入準則、監察**關鍵安全功能**。

**EN —** Automatic trips include (Westinghouse-style setpoints): high neutron flux (~109 %), low RCS flow (~90 %), low/high pressurizer pressure (~1865 / 2385 psig), low SG level, turbine trip.

**粵語 —** 自動跳脫包括（西屋式設定點）：高中子通量（約 109%）、低 RCS 流量（約 90%）、穩壓器低／高壓（約 1865／2385 psig）、蒸汽產生器低水位、渦輪跳脫。

---

## Meltdown & recovery · 熔毀與復原

**EN —** If fuel temperature exceeds structural limits for too long, the core melts. The simulation shows the **CORE MELTDOWN** overlay and (with real shutdown OFF — the default) the message *"Real shutdown is OFF — your PC is safe."* Click **Dismiss & reset simulation · 關閉並重設模擬** to recover. Only if you have armed "ARM REAL SHUTDOWN" will an actual (abortable, 10 s countdown) Windows shutdown follow — see [Safety & Integrations](Reactor-Safety-and-Integrations.md).

**粵語 —** 如果燃料溫度長時間超過結構極限，爐心就會熔毀。模擬會顯示 **CORE MELTDOWN** 畫面，並（喺真實關機 OFF——即預設下）顯示*「Real shutdown is OFF — your PC is safe.」*。揿**關閉並重設模擬**復原。只有當你已開啟「ARM REAL SHUTDOWN」，先會跟住觸發真實（可中止、10 秒倒數）嘅 Windows 關機——詳見[安全與整合](Reactor-Safety-and-Integrations.md)。

---

## Scenario drills · 情景演習

**EN —** Use the **Scenario** selector to inject classic transients and practice your response. Each has a realistic parameter signature and automatic protective actions.

**粵語 —** 用**情景選擇器**注入典型瞬態並練習應對。每個都有真實嘅參數特徵同自動保護動作。

| Scenario · 情景 | Signature · 特徵 | Operator response · 操作員應對 |
|---|---|---|
| **LOCA** (Loss-of-Coolant Accident · 失冷事故) | Falling primary pressure & inventory, subcooling lost. · 一次側壓力與存量下降、過冷度喪失。 | Verify safety injection, follow E-0 → E-1. · 確認安全注入，跟 E-0 → E-1。 |
| **SBO** (Station Blackout · 全廠停電) | Loss of AC power, RCPs trip, rely on decay-heat removal. · 喪失交流電源、主泵跳脫，靠衰變熱排除。 | Conserve battery, restore power, monitor inventory. · 節省電池、恢復電源、監察存量。 |
| **LOFW** (Loss of Feedwater · 喪失給水) | SG level falls, secondary heat sink degrades. · 蒸汽產生器水位下降、二次側熱阱劣化。 | Start aux feedwater, verify SG level recovery. · 啟動輔助給水、確認水位回復。 |
| **ATWS** (Anticipated Transient Without Scram · 預期瞬態未停堆) | Trip demand with rods failing to insert. · 有跳脫需求但棒未能插入。 | Manual rod insertion / boration, reduce power. · 手動插棒／加硼、降功率。 |
| **SGTR** (Steam Generator Tube Rupture · 蒸汽產生器傳熱管破裂) | Primary-to-secondary leak, secondary radiation rises. · 一次往二次洩漏、二次側輻射上升。 | Isolate affected SG, cool & depressurize. · 隔離受影響 SG、冷卻並洩壓。 |
| **MSLB** (Main Steam Line Break · 主蒸汽管破裂) | Rapid secondary depressurization, overcooling. · 二次側快速洩壓、過度冷卻。 | Isolate steam line, control overcooling transient. · 隔離蒸汽管、控制過冷瞬態。 |

> ⚠️ **EN —** Because the at-power reactivity calibration is unfinished (P1–P3), some scenarios may run the core away once power rises; the reactor is safe to view in Shutdown. See the [Test Report](Reactor-Test-Report.md).
> ⚠️ **粵語 —** 由於滿載反應性校準未完成（P1–P3），部分情景一旦功率上升可能令爐心失控；喺停堆狀態下觀看係安全嘅。詳見[測試報告](Reactor-Test-Report.md)。

---

### Reactor pages · 反應堆頁面導覽
[🏠 Reactor Hub · 反應堆總覽](Nuclear-Reactor.md) · [Overview · 總覽](Reactor-Overview.md) · [Control Room · 控制室](Reactor-Control-Room.md) · [Operating Procedures · 操作程序](Reactor-Operating-Procedures.md) · [Emergencies & Scenarios · 緊急與情景](Reactor-Emergencies-and-Scenarios.md) · [Fuel & Waste · 燃料與廢料](Reactor-Fuel-and-Waste.md) · [Water Treatment · 水處理](Reactor-Water-Treatment.md) · [Safety & Integrations · 安全與整合](Reactor-Safety-and-Integrations.md) · [Operating Manual · 操作手冊](Nuclear-Reactor-Operating-Manual.md) · [Test Report · 測試報告](Reactor-Test-Report.md)

*English + 繁體中文／粵語*
