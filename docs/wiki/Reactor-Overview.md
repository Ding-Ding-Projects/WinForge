# Reactor Overview · 反應堆總覽

**EN —** The Nuclear Reactor is WinForge's flagship: a hyper-realistic **Pressurized Water Reactor (PWR)** control room rendered entirely in WinUI 3. It models 6-group point kinetics, reactivity feedback (Doppler / moderator / boron / xenon), thermal-hydraulics, a steam/turbine secondary plant, a Westinghouse-style protection system, synthesized control-room audio, and a live plant mimic. It is a **simulation / training toy** — it controls no real hardware.

**粵語 —** 核反應堆係 WinForge 嘅旗艦模組：一個完全用 WinUI 3 繪製、超寫實嘅**壓水式反應堆（PWR）**控制室。佢模擬六組點動力學、反應性回饋（都卜勒／緩和劑／硼／氙）、熱工水力、蒸汽渦輪二次側、西屋式保護系統、合成控制室音效，同即時機組流程圖。佢只係一個**模擬／訓練玩具**，唔會控制任何真實硬件。

![Reactor control room · 反應堆控制室](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-reactor.png)

---

## Where to find it · 喺邊度搵到

**EN —** It is the **first tile on the Dashboard** (★ FLAGSHIP) and the **top entry in the navigation**. You can also deep-link from a terminal: `WinForge.exe --reactor` (or `--page reactor`).

**粵語 —** 它係**儀表板第一個磚** (★ 旗艦) 同**導覽列最頂**。亦可由終端機深層連結：`WinForge.exe --reactor`（或 `--page reactor`）。

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

## Safety summary · 安全摘要

> ⚠️ **EN —** Two real-world side-effects are **opt-in and clearly gated**:
> - **Meltdown → real PC shutdown** is **OFF by default**. When meltdown occurs it only shows a simulated overlay. You must arm "ARM REAL SHUTDOWN" to enable an actual (abortable, 10 s countdown) Windows shutdown.
> - **Keep PC awake while generating** holds the PC awake only while the generator is on-load; it releases the instant you SCRAM or trip.
> - The reactor is **sim-only** — it controls no real hardware.

> ⚠️ **粵語 —** 兩個會影響真實系統嘅效果都係**預設關閉、明確開關**：
> - **熔毀 → 真實關機**預設 **OFF**；熔毀時只播模擬畫面。你要開啟「ARM REAL SHUTDOWN」先會觸發真實（可中止、10 秒倒數）嘅 Windows 關機。
> - **發電時保持喚醒**只喺發電機併網時保持電腦喚醒；一 SCRAM 或跳脫即刻放開。
> - 反應堆**只係模擬**，唔會控制任何真實硬件。

See [Reactor Safety & Integrations · 反應堆安全與整合](Reactor-Safety-and-Integrations.md) for the full detail. · 詳情見反應堆安全與整合頁。

---

### Reactor pages · 反應堆頁面導覽
[🏠 Reactor Hub · 反應堆總覽](Nuclear-Reactor.md) · [Overview · 總覽](Reactor-Overview.md) · [Control Room · 控制室](Reactor-Control-Room.md) · [Operating Procedures · 操作程序](Reactor-Operating-Procedures.md) · [Emergencies & Scenarios · 緊急與情景](Reactor-Emergencies-and-Scenarios.md) · [Fuel & Waste · 燃料與廢料](Reactor-Fuel-and-Waste.md) · [Water Treatment · 水處理](Reactor-Water-Treatment.md) · [Safety & Integrations · 安全與整合](Reactor-Safety-and-Integrations.md) · [Operating Manual · 操作手冊](Nuclear-Reactor-Operating-Manual.md) · [Test Report · 測試報告](Reactor-Test-Report.md)

*English + 繁體中文／粵語*
