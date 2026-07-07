# Reactor Safety & Integrations · 反應堆安全與整合

**EN —** Every real-world side-effect of the reactor is **opt-in, clearly gated, and reversible**. This page collects the safety toggles, the OS integrations, the crash-safe autosave, and the public status API that lets other apps depend on the reactor.

**粵語 —** 反應堆對真實世界嘅每一個影響都係**可選、明確開關、可逆轉**。呢頁集合咗安全開關、作業系統整合、防崩潰自動儲存，以及畀其他 app 依賴反應堆嘅公開狀態 API。

**EN —** All of these live on a **dedicated Reactor Settings page** (⚙ button on the reactor toolbar, or `WinForge.exe --page reactorsettings`), kept separate from the pure-simulation controls. Defaults: ARM real-shutdown **OFF**, Windows-settings link **OFF**, Home Assistant mirror **OFF**, status API **ON**, autosave **ON**, keep-awake **ON**.

**粵語 —** 以上全部都喺一個**獨立嘅反應堆設定頁**（反應堆工具列嘅 ⚙ 掣，或 `WinForge.exe --page reactorsettings`），同純模擬控制分開。預設：真實關機 **OFF**、Windows 設定連動 **OFF**、Home Assistant 連動 **OFF**、狀態 API **ON**、自動儲存 **ON**、保持喚醒 **ON**。

![Reactor Settings · 反應堆設定](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-reactorsettings.png)

---

## Meltdown → real shutdown (ARM toggle) · 熔毀 → 真實關機（ARM 開關）

**EN —** The **ARM REAL SHUTDOWN** toggle is **OFF by default**. With it off, a meltdown only shows a simulated overlay and the message *"Real shutdown is OFF — your PC is safe."* Only when you explicitly arm it will a meltdown trigger an **actual Windows shutdown** — and even then it runs a **10 s abortable countdown** so you can cancel. State is flushed *before* any armed shutdown so nothing is lost.

**粵語 —** **ARM REAL SHUTDOWN** 開關**預設 OFF**。關閉時，熔毀只播模擬畫面同顯示*「Real shutdown is OFF — your PC is safe.」*。只有當你明確開啟，熔毀先會觸發**真實 Windows 關機**——而且仍會跑一個**10 秒可中止倒數**畀你取消。狀態會喺任何已開啟嘅關機*之前*寫入，所以唔會遺失任何嘢。

---

## Keep PC awake while generating · 發電時保持喚醒

**EN —** When the generator is on-load, the reactor holds the PC awake (the keep-awake pill turns gold). It **releases the instant you SCRAM or trip** the generator offline — normal sleep is allowed again.

**粵語 —** 當發電機併網，反應堆會保持電腦喚醒（喚醒指示變金色）。一旦你 **SCRAM 或跳脫**令發電機離線即刻放開——正常睡眠再次被允許。

---

## Reactor ↔ Windows-settings linkage · 反應堆 ↔ Windows 設定連動

**EN —** An **opt-in, reversible** linkage can tie reactor state to Windows settings. It is never silent: it is opt-in, has a visible switch, and can be undone. The **Always-On Reactor** option (also opt-in) registers a logon task so the reactor relaunches if closed — it has a clearly visible OFF switch and is never hidden or unkillable.

**粵語 —** 一個**可選、可逆轉**嘅連動可以將反應堆狀態同 Windows 設定綁定。它從不靜默：可選、有明顯開關、可撤銷。**常駐反應堆**選項（同樣可選）會註冊登入工作，令反應堆關閉後重新啟動——它有明顯嘅關閉開關，永遠唔會隱藏或無法終止。

---

## Crash-safe autosave · 防崩潰自動儲存

**EN —** A crash/shutdown-safe autosave snapshots reactor state (power, precursors, temps, pressures, xenon, rods, boron, mode, setpoints, alarms) every few seconds with **atomic writes + a `.bak` fallback**, and flushes on app exit, crash, session-ending, and **before** any armed real shutdown — so a reopened reactor resumes where it left off.

**粵語 —** 防崩潰／關機自動儲存每隔幾秒快照反應堆狀態（功率、先驅核、溫度、壓力、氙、控制棒、硼、模式、設定點、警報），採用**原子寫入加 `.bak` 後備**，並喺 app 退出、崩潰、工作階段結束，以及任何已開啟嘅真實關機**之前**寫入——所以重開嘅反應堆會由上次嘅位置續行。

---

## Public status API · 公開狀態 API

**EN —** The reactor publishes a public status feed so **other apps can depend on it**. A lightweight client, [`Sdk/ReactorStatusClient.cs`](../../Sdk/ReactorStatusClient.cs), reads the live status (mode, power, alarms, etc.) without coupling to the WinUI app. This is what lets other WinForge modules and external tools observe the reactor in real time.

**粵語 —** 反應堆發布公開狀態饋送，畀**其他 app 依賴它**。一個輕量客戶端 [`Sdk/ReactorStatusClient.cs`](../../Sdk/ReactorStatusClient.cs) 讀取即時狀態（模式、功率、警報等），無需同 WinUI app 耦合。呢個就係令其他 WinForge 模組同外部工具可以即時觀察反應堆嘅機制。

---

### Reactor pages · 反應堆頁面導覽
[🏠 Reactor Hub · 反應堆總覽](Nuclear-Reactor.md) · [Overview · 總覽](Reactor-Overview.md) · [Control Room · 控制室](Reactor-Control-Room.md) · [Operating Procedures · 操作程序](Reactor-Operating-Procedures.md) · [Emergencies & Scenarios · 緊急與情景](Reactor-Emergencies-and-Scenarios.md) · [Fuel & Waste · 燃料與廢料](Reactor-Fuel-and-Waste.md) · [Water Treatment · 水處理](Reactor-Water-Treatment.md) · [Safety & Integrations · 安全與整合](Reactor-Safety-and-Integrations.md) · [Operating Manual · 操作手冊](Nuclear-Reactor-Operating-Manual.md) · [Test Report · 測試報告](Reactor-Test-Report.md)

*English + 繁體中文／粵語*
