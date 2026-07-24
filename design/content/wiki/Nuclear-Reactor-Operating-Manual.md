# Nuclear Reactor — Operating Manual · 核反應堆操作手冊

**EN —** The Nuclear Reactor is WinForge's flagship: a hyper-realistic **Pressurized Water Reactor (PWR)** control room rendered entirely in WinUI 3, modeling 6-group point kinetics, reactivity feedback (Doppler / moderator / boron / xenon), thermal-hydraulics, a steam/turbine secondary plant, a Westinghouse-style protection system, synthesized control-room audio, and a live plant mimic. It is a **simulation / training toy** — it controls no real hardware.

**粵語 —** 核反應堆係 WinForge 嘅旗艦模組：一個完全用 WinUI 3 繪製、超寫實嘅**壓水式反應堆（PWR）**控制室，模擬六組點動力學、反應性回饋（都卜勒／緩和劑／硼／氙）、熱工水力、蒸汽渦輪二次側、西屋式保護系統、合成控制室音效，同即時機組流程圖。佢只係一個**模擬／訓練玩具**，唔會控制任何真實硬件。

> **EN —** This manual has been **split into focused pages** so it is no longer one long page. This page is now a **manual index** — pick a section below. · **粵語 —** 本手冊已**拆分為多個聚焦頁面**，唔再係一條長頁。呢頁而家係**手冊索引**——喺下面揀一個章節。

> ⚠️ **Safety / 安全** — Real-world side-effects are **opt-in and clearly gated**: **meltdown → real PC shutdown** is **OFF by default** (sim overlay only); **keep PC awake** holds awake only while on-load and releases on SCRAM/trip; the reactor is **sim-only**. Full detail in [Safety & Integrations](Reactor-Safety-and-Integrations.md). · 影響真實系統嘅效果都係**預設關閉、明確開關**：**熔毀 → 真實關機**預設 **OFF**（只播模擬畫面）；**保持喚醒**只喺併網時生效、SCRAM／跳脫即放開；反應堆**只係模擬**。詳見[安全與整合](Reactor-Safety-and-Integrations.md)。

---

## Manual contents · 手冊目錄

| Section · 章節 | Page · 頁面 |
|---|---|
| **1. Overview & where to find it · 總覽與位置** | [Reactor Overview · 反應堆總覽](Reactor-Overview.md) |
| **2. Control room — panels, gauges, RPS, mimic · 控制室面板儀錶** | [Reactor Control Room · 反應堆控制室](Reactor-Control-Room.md) |
| **3. Operating procedures — startup → shutdown · 操作程序** | [Reactor Operating Procedures · 反應堆操作程序](Reactor-Operating-Procedures.md) |
| **4. Emergencies & scenario drills · 緊急與情景** | [Reactor Emergencies & Scenarios · 反應堆緊急與情景](Reactor-Emergencies-and-Scenarios.md) |
| **5. Fuel factory & nuclear waste · 燃料工廠與核廢料** | [Reactor Fuel & Waste · 反應堆燃料與廢料](Reactor-Fuel-and-Waste.md) |
| **6. Makeup-water treatment plant · 補給水處理廠** | [Reactor Water Treatment · 反應堆水處理](Reactor-Water-Treatment.md) |
| **7. Safety toggles, integrations & status API · 安全開關、整合與狀態 API** | [Reactor Safety & Integrations · 反應堆安全與整合](Reactor-Safety-and-Integrations.md) |
| **8. Reactor-powered industrial loads · 反應堆供電工業負載** | [Reactor Industrial Loads · 反應堆工業負載](Reactor-Industrial-Loads.md) |

---

## Quick procedure summary · 程序速覽

**EN —** The reactor ships **held in cold shutdown** — the operator must start it up. In brief: establish primary flow → pressurize → dilute/withdraw to approach criticality on the **1/M** and source range → ascend power → roll turbine to **1800 rpm** and sync → operate at power on Tavg program → ramp down and trip to shut down. Full step-by-step detail is in [Operating Procedures](Reactor-Operating-Procedures.md); emergencies in [Emergencies & Scenarios](Reactor-Emergencies-and-Scenarios.md).

**粵語 —** 反應堆出廠時**保持喺冷停堆**——必須由操作員起動。簡而言之：建立一次側流量 → 加壓 → 稀釋／提棒，靠 **1/M** 同源區接近臨界 → 升功率 → 渦輪升到 **1800 rpm** 併網 → 按 Tavg 程序滿載運行 → 減負載並跳脫停堆。完整逐步詳情見[操作程序](Reactor-Operating-Procedures.md)；緊急情況見[緊急與情景](Reactor-Emergencies-and-Scenarios.md)。

---

## Realism roadmap · 寫實度路線圖

**EN —** The first multi-agent realism review is now an **archival baseline**, not an open-defect list. P1–P5 are implemented: stable backward-Euler kinetics, verified shutdown margin, a closed fuel/steam-generator energy balance, corrected decay-heat/xenon/pressurizer behavior, wired 2-of-4 RPS protection, and 1/M startup guidance. The current headless suite is **65/65 green**, including ammonia and grid load-shed regressions. Its sustained high-power regression holds **0.836→0.835 RTP** without emergency cooling, SCRAM, runaway, or meltdown. Historical analysis: [reactor-realism-review-001.md](../reactor-realism-review-001.md); live evidence: [Test Report](Reactor-Test-Report.md).

**粵語 —** 首次多代理寫實度審查而家係**歷史基準**，唔再係未完成缺陷清單。P1–P5 已完成：穩定後向歐拉動力學、已驗證停堆裕度、閉合燃料／蒸汽產生器能量平衡、已修正衰變熱／氙／穩壓器行為、四取二 RPS 保護，同 1/M 起動指引。現時無介面測試 **65/65** 全綠，包括合成氨同電網卸載回歸；持續高功率回歸維持 **0.836→0.835 RTP**，冇應急冷卻、SCRAM、失控或者熔毀。歷史分析見 [reactor-realism-review-001.md](../reactor-realism-review-001.md)；現時證據見[測試報告](Reactor-Test-Report.md)。

---

### Reactor pages · 反應堆頁面導覽
[🏠 Reactor Hub · 反應堆總覽](Nuclear-Reactor.md) · [Overview · 總覽](Reactor-Overview.md) · [Control Room · 控制室](Reactor-Control-Room.md) · [Operating Procedures · 操作程序](Reactor-Operating-Procedures.md) · [Emergencies & Scenarios · 緊急與情景](Reactor-Emergencies-and-Scenarios.md) · [Fuel & Waste · 燃料與廢料](Reactor-Fuel-and-Waste.md) · [Water Treatment · 水處理](Reactor-Water-Treatment.md) · [Industrial Loads · 工業負載](Reactor-Industrial-Loads.md) · [Safety & Integrations · 安全與整合](Reactor-Safety-and-Integrations.md) · [Operating Manual · 操作手冊](Nuclear-Reactor-Operating-Manual.md) · [Test Report · 測試報告](Reactor-Test-Report.md)

*Screenshots captured from the running self-contained build · 截圖擷取自實際運行的自包含建置 · `English + 繁體中文／粵語`*
