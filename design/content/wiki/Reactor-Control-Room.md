# Reactor Control Room · 反應堆控制室

**EN —** The control room is the heart of the reactor module: a Westinghouse-style hard-panel layout with a status banner, analog instrument gauges, the Reactor Protection System (RPS) channel panel, annunciator tiles, strip-chart recorders, and the live plant mimic. It can run inline on the page or pop out into its own dedicated full-screen window, and rooms are organized as tabs in an HTML5 / WebView2 surface.

**粵語 —** 控制室係反應堆模組嘅核心：西屋式硬面板佈局，附狀態橫額、模擬指針儀錶、反應堆保護系統（RPS）通道面板、警示磚、走紙記錄儀同即時機組流程圖。可以喺頁面內嵌運行，亦可彈出做獨立全螢幕視窗；各房間以 HTML5／WebView2 介面嘅分頁形式組織。

![Reactor control room · 反應堆控制室](https://raw.githubusercontent.com/Ding-Ding-Projects/WinForge/main/docs/screenshot-reactor.png)

---

## Top-bar panels & controls · 頂部面板與控制

| Area · 區域 | What it shows · 內容 |
|---|---|
| **Status banner** · 狀態橫額 | Mode (Shutdown / Startup / Run / Tripped / Meltdown), the first-out trip cause, and mission time `T+…s`. <br> 模式（停堆／起動／運行／跳脫／熔毀）、首發跳脫原因，同任務時間 `T+…s`。 |
| **Keep-awake pill** · 喚醒指示 | `⚡ Grid online — this PC is kept awake (N MWe)` when generating, else `Generator offline — normal sleep allowed`. <br> 發電時顯示「併網 — 保持喚醒」，否則顯示「發電機離線 — 允許正常睡眠」。 |
| **Toolbar** · 工具列 | **Open full control room** (pop-out window), **Mini widgets** (desktop gadgets), **Mute audio**, **Scenario** selector. <br> 開啟完整控制室（彈出視窗）、迷你小工具（桌面小工具）、靜音、情景選擇器。 |
| **SCRAM bar** · 緊急停堆 | The big red **SCRAM — EMERGENCY SHUTDOWN · 緊急停堆** button + **Reset trip · 重置跳脫**. <br> 大紅色 SCRAM 緊急停堆按鈕加重置跳脫。 |
| **Auto rod control** · 自動棒控制 | Hands the regulating bank to an automatic controller targeting a power setpoint. <br> 將調節棒組交畀自動控制器，追蹤功率設定點。 |
| **Plant Mimic Diagram** · 機組流程圖 | Vessel → pressurizer → steam generator → turbine → generator → condenser, animated by flow and temperature. <br> 壓力槽 → 穩壓器 → 蒸汽產生器 → 渦輪 → 發電機 → 冷凝器，按流量同溫度動態顯示。 |

---

## Instruments & protection · 儀錶與保護

**EN —** Scroll down for the instrument gauges, the RPS channel panel, the annunciator tiles, and the strip-chart recorders.

**粵語 —** 向下捲動可見儀錶、RPS 通道面板、警示磚同走紙記錄儀。

- **Critical Safety Functions · 關鍵安全功能** (top): `P Integrity`, `Z Containment`, `I Inventory` status tiles (green / amber / red). · 頂部嘅 `P 完整性`、`Z 圍阻`、`I 存量` 狀態磚（綠／琥珀／紅）。
- **Reactor Protection System · 反應堆保護系統** — one card per protection function with its **2-out-of-4 instrument channels** as LEDs. (2-of-4 coincidence with Westinghouse setpoints: a single tripped channel is a *partial* trip — amber; the reactor trips only when ≥2 of 4 channels of a function trip.) · 每個保護功能一張卡，以 LED 顯示其 **四取二儀表通道**。（西屋式四取二符合邏輯：單一通道跳脫只係*部分*跳脫，顯示琥珀色；要某功能四個通道有 ≥2 個跳脫，反應堆先會跳脫。）
- **Instrument Gauges · 儀錶** — analog dials for Reactor power (%), Thermal power (MW), Electrical (MWe), Decay heat (%), Reactor period (s), Reactivity (pcm), fuel temp, Tavg / Thot / Tcold, subcooling, primary pressure. · 模擬指針錶顯示反應堆功率（%）、熱功率（MW）、電功率（MWe）、衰變熱（%）、反應堆週期（s）、反應性（pcm）、燃料溫度、Tavg／Thot／Tcold、過冷度同一次側壓力。
- **Annunciator tiles · 警示磚** — alarm windows that latch first-out and acknowledge with a beep. · 警示窗格鎖定首發、確認時發出嗶聲。
- **Strip-chart recorders · 走紙記錄儀** — live trends of power, temperatures, pressure and reactivity. · 功率、溫度、壓力同反應性嘅即時趨勢。

---

## Foreground authority & handoff · 前景 authority 同交接

**EN —** Every Reactor page sees the same canonical plant state, but only the newest visible page/window has foreground authority to drive it and own safety UI. Parallel pages stay live as read-only observers: status, gauges, annunciators, mimic, strip charts, NIS/CSF/RPS, synchronized control positions, and the status-API card keep rendering from shared state, while plant/scenario/SCRAM controls and companion launchers are disabled behind a non-dismissible observer notice.

**粵語 —** 每個反應堆頁都會睇到同一個正式機組狀態，但只有最新可見頁面／視窗有前景 authority 去驅動同持有安全 UI。並行頁面會保持即時唯讀觀察：狀態、儀錶、警示、流程圖、走紙記錄、NIS／CSF／RPS、同步控制位置同狀態 API 卡會照樣由共用狀態更新；機組／情景／SCRAM 控制同 companion 啟動掣就會停用，並顯示唔可以關閉嘅觀察提示。

**EN —** Moving authority does not reset the plant. Before handoff it automatically aborts any active real-shutdown countdown, then closes mutating HTML/full-control-room, startup-checklist, and SCRAM-widget companions owned by the demoted page; read-only core-power, status, and startup-gauge widgets may remain. During a shared meltdown, a demoted page keeps a live overlay but exposes no ABORT/reset controls; the overlay synchronizes and collapses automatically when the authoritative driver resets the shared plant, so it cannot survive as stale state if that page is promoted later. When the final control room closes, real shutdown disarms, Home Assistant/Awake/SystemLink and audio stop or restore, and a minimal background loop keeps only physics, automatic-start progression, the mission clock, and the truthful public API current.

**粵語 —** 交接 authority 唔會重設機組。交接之前會自動中止任何進行中嘅真實關機倒數，再關閉由降級頁面持有、可改動模擬嘅 HTML／完整控制室、起動 checklist 同 SCRAM widget companion；唯讀爐心功率、狀態同起動儀錶 widget 可以留低。共用機組熔毀時，降級頁面會保持即時 overlay，但唔會提供 ABORT／重設控制；authoritative driver 重設共用機組後，overlay 會自動同步兼收起，所以之後再升級嗰頁都唔會殘留舊 overlay。最後一個控制室關閉時，真實關機會解除武裝，Home Assistant／Awake／SystemLink 同音效會停止或還原；最小化背景 loop 只會繼續更新物理、自動起動進度、任務時鐘同如實公開 API。

---

## Rooms, pop-out & widgets · 房間、彈出與小工具

- **Rooms as tabs · 房間分頁** — the control room is rendered as an HTML5 / WebView2 surface where each room is a tab you switch between. · 控制室以 HTML5／WebView2 介面繪製，每個房間係一個可切換嘅分頁。
- **Open full control room · 開啟完整控制室** pops the reactor into its own dedicated window for a full-screen control-room view. · 將反應堆彈出做獨立視窗，提供全螢幕控制室畫面。
- **Mini widgets · 迷你小工具** spawn small always-on-top desktop gauges (e.g. core power, status) you can place anywhere. · 產生細小、永遠置頂嘅桌面儀錶（例如爐心功率、狀態），可隨意擺放。
- **Mute audio · 靜音** toggles the synthesized soundscape (ambient pump/turbine hum, SCRAM/annunciator alarms, acknowledgement beeps), generated in C# — no external files. · 開關合成音景（泵／渦輪環境聲、SCRAM／警示警報、確認嗶聲），全部由 C# 生成，冇外部檔案。

---

### Reactor pages · 反應堆頁面導覽
[🏠 Reactor Hub · 反應堆總覽](Nuclear-Reactor.md) · [Overview · 總覽](Reactor-Overview.md) · [Control Room · 控制室](Reactor-Control-Room.md) · [Operating Procedures · 操作程序](Reactor-Operating-Procedures.md) · [Emergencies & Scenarios · 緊急與情景](Reactor-Emergencies-and-Scenarios.md) · [Fuel & Waste · 燃料與廢料](Reactor-Fuel-and-Waste.md) · [Water Treatment · 水處理](Reactor-Water-Treatment.md) · [Safety & Integrations · 安全與整合](Reactor-Safety-and-Integrations.md) · [Operating Manual · 操作手冊](Nuclear-Reactor-Operating-Manual.md) · [Test Report · 測試報告](Reactor-Test-Report.md)

*English + 繁體中文／粵語*
