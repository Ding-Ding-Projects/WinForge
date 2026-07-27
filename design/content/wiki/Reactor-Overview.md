# Reactor Overview · 反應堆總覽

**EN —** The Nuclear Reactor is WinForge's flagship: a hyper-realistic **Pressurized Water Reactor (PWR)** control room rendered entirely in WinUI 3. It models 6-group point kinetics, reactivity feedback (Doppler / moderator / boron / xenon), thermal-hydraulics, a steam/turbine secondary plant, a Westinghouse-style protection system, synthesized control-room audio, and a live plant mimic. It is a **simulation / training toy** — it controls no real hardware.

**粵語 —** 核反應堆係 WinForge 嘅旗艦模組：一個完全用 WinUI 3 繪製、超寫實嘅**壓水式反應堆（PWR）**控制室。佢模擬六組點動力學、反應性回饋（都卜勒／緩和劑／硼／氙）、熱工水力、蒸汽渦輪二次側、西屋式保護系統、合成控制室音效，同即時機組流程圖。佢只係一個**模擬／訓練玩具**，唔會控制任何真實硬件。

![Reactor control room · 反應堆控制室](https://raw.githubusercontent.com/Ding-Ding-Projects/WinForge/main/docs/screenshot-reactor.png)

---

## Where to find it · 喺邊度搵到

**EN —** It is the **first tile on the Dashboard** (★ FLAGSHIP) and the **top entry in the navigation**. You can also deep-link from a terminal: `WinForge.exe --reactor` (or `--page reactor`). `WinForge.exe --auto-start-reactor` opens the same canonical reactor session and applies the automatic-start preset once per app session; reopening the page cannot replay it.

**粵語 —** 它係**儀表板第一個磚** (★ 旗艦) 同**導覽列最頂**。亦可由終端機深層連結：`WinForge.exe --reactor`（或 `--page reactor`）。`WinForge.exe --auto-start-reactor` 會開啟同一個正式反應堆 session，並喺每個 app session 只套用一次自動起動 preset；重開頁面唔會重播。

---

## The plant at a glance · 機組概覽

**EN —** Design references follow real PWR practice: ~3411 MWth / ~1100 MWe, Tavg ≈ 305 °C at full power, primary ≈ 155 bar (2250 psia), β-eff ≈ 0.0065. The mimic threads vessel → pressurizer → steam generator → turbine → generator → condenser, animated by flow and temperature.

**粵語 —** 設計參考跟足真實 PWR 實務：約 3411 MWth／約 1100 MWe、滿載 Tavg ≈ 305 °C、一次側 ≈ 155 bar（2250 psia）、β-eff ≈ 0.0065。流程圖串連壓力槽 → 穩壓器 → 蒸汽產生器 → 渦輪 → 發電機 → 冷凝器，按流量同溫度動態顯示。

| Subsystem · 子系統 | Summary · 摘要 |
|---|---|
| **Reactor core · 爐心** | 6-group point kinetics + Doppler / moderator / boron / xenon feedback. · 六組點動力學加都卜勒／緩和劑／硼／氙回饋。 |
| **Primary loop · 一次迴路** | Reactor coolant pumps, pressurizer, ~155 bar. · 主泵、穩壓器、約 155 bar。 |
| **Secondary plant · 二次側** | Steam generator → turbine → generator → condenser. · 蒸汽產生器 → 渦輪 → 發電機 → 冷凝器。 |
| **Protection · 保護** | Westinghouse-style RPS with 2-of-4 channels. · 西屋式 RPS，四取二通道。 |

---

## Session continuity · Session 連續性

**EN —** All Reactor pages share one in-memory simulation. The newest visible page is the sole foreground driver; parallel pages remain live read-only observers whose gauges/status keep rendering while plant controls are disabled. Authority demotion aborts any active shutdown countdown and closes mutating HTML/control-room, checklist, and SCRAM-widget companions, while read-only widgets may remain. A demoted meltdown page keeps its live overlay without ABORT/reset and auto-collapses it when the driver resets shared state. After the last control room closes, a minimal UI-thread loop continues only simulated physics, automatic-start progression, the mission clock, and the truthful public status API. Page-owned audio, Home Assistant, keep-awake, Windows linkage, and real-shutdown handling stop or restore rather than running invisibly.

**粵語 —** 全部反應堆頁共用一個記憶體內模擬；最新可見頁係唯一前景 driver，而並行頁面會保持即時唯讀觀察，儀錶／狀態繼續更新但機組控制會停用。Authority 降級會中止任何進行中嘅關機倒數，並關閉可改動模擬嘅 HTML／控制室、checklist 同 SCRAM widget companion；唯讀 widget 可以留低。降級頁面嘅熔毀 overlay 會保持即時顯示但冇 ABORT／重設，driver 重設共用狀態時會自動收起。最後一個控制室關閉後，最小化 UI-thread loop 只會繼續模擬物理、自動起動進度、任務時鐘同如實公開狀態 API。頁面持有嘅音效、Home Assistant、保持喚醒、Windows 連動同真實關機處理會停止或還原，唔會暗中運行。

---

## Safety summary · 安全摘要

> ⚠️ **EN —** Real-world side-effects are **opt-in or clearly bounded**:
> - **Meltdown → real PC shutdown** is **OFF by default** and can be armed only with a fully loaded, visible control room that can show ABORT. The ten-second deadline and OS accepted/refused outcome are truthful session-global state, but changing foreground page/window authority automatically aborts an active countdown. If Windows accepts the request, ABORT disappears because the request can no longer be cancelled.
> - **Keep PC awake while generating** holds the PC awake only while the generator is on-load and a Reactor page is visible; it releases the instant you SCRAM/trip or close the last page.
> - The reactor is **sim-only** — it controls no real hardware.

> ⚠️ **粵語 —** 真實世界效果全部都係**可選或者有清楚界線**：
> - **熔毀 → 真實關機**預設 **OFF**，只可以喺已完成載入、可顯示 ABORT 嘅控制室明確 ARM。十秒 deadline 同 OS 接受／拒絕結果係如實嘅全 session 狀態，但前景頁面／視窗 authority 一改變就會自動中止進行中倒數。Windows 接受要求後，因為已經唔可以取消，所以 ABORT 會消失。
> - **發電時保持喚醒**只喺發電機併網兼有反應堆頁面可見時保持電腦喚醒；一 SCRAM／跳脫，或者關閉最後一頁，就即刻放開。
> - 反應堆**只係模擬**，唔會控制任何真實硬件。

See [Reactor Safety & Integrations · 反應堆安全與整合](Reactor-Safety-and-Integrations.md) for the full detail. · 詳情見反應堆安全與整合頁。

---

### Reactor pages · 反應堆頁面導覽
[🏠 Reactor Hub · 反應堆總覽](Nuclear-Reactor.md) · [Overview · 總覽](Reactor-Overview.md) · [Control Room · 控制室](Reactor-Control-Room.md) · [Operating Procedures · 操作程序](Reactor-Operating-Procedures.md) · [Emergencies & Scenarios · 緊急與情景](Reactor-Emergencies-and-Scenarios.md) · [Fuel & Waste · 燃料與廢料](Reactor-Fuel-and-Waste.md) · [Water Treatment · 水處理](Reactor-Water-Treatment.md) · [Safety & Integrations · 安全與整合](Reactor-Safety-and-Integrations.md) · [Operating Manual · 操作手冊](Nuclear-Reactor-Operating-Manual.md) · [Test Report · 測試報告](Reactor-Test-Report.md)

*English + 繁體中文／粵語*
