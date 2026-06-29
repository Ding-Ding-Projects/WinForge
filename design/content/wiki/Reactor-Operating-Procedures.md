# Reactor Operating Procedures · 反應堆操作程序

**EN —** These procedures follow real PWR practice and take the plant from a held cold-shutdown state all the way to at-power operation and back. The reactor ships **held in cold shutdown** — the operator must start it up; it will not self-start. Design references: ~3411 MWth / ~1100 MWe, Tavg ≈ 305 °C at full power, primary ≈ 155 bar (2250 psia), β-eff ≈ 0.0065.

**粵語 —** 呢啲程序跟足真實 PWR 實務，帶機組由保持冷停堆狀態一路去到滿載運行再返回。反應堆出廠時**保持喺冷停堆**——必須由操作員起動，唔會自行起動。設計參考：約 3411 MWth／約 1100 MWe、滿載 Tavg ≈ 305 °C、一次側 ≈ 155 bar（2250 psia）、β-eff ≈ 0.0065。

---

## 1. Cold startup → approach to criticality · 冷態起動 → 接近臨界

1. **Establish primary flow.** Start ≥3 reactor coolant pumps; confirm flow > 85 %. · 啟動主泵，流量 > 85%。
2. **Pressurize.** Energize pressurizer heaters; raise primary pressure toward ~2235 psia. · 加壓至約 2235 psia。
3. **Dilute / withdraw.** With shutdown banks fully out, slowly dilute boron and/or withdraw the regulating bank, watching the **source-range** count-rate and the **1/M** plot trend toward zero. · 喺停堆棒組全部抽出後，慢慢稀釋硼／提調節棒，睇**源區**計數率同 **1/M** 圖趨向零。
4. **Declare criticality** when a small, *stable* positive period (> 30 s) holds at low power. Reactivity should hover near **0 pcm**. · 喺低功率、穩定正週期 (>30 s) 時宣布臨界；反應性應喺 **0 pcm** 附近。
5. Never let reactivity approach prompt-critical (+β ≈ +650 pcm). The period gauge going very short is your warning. · 切勿接近瞬發臨界（+β ≈ +650 pcm）；週期錶變得好短就係警告。

> ⚠️ **EN —** The reactor is held safe in cold shutdown but the at-power reactivity calibration is unfinished — see the [Test Report](Reactor-Test-Report.md) and [reactor-realism-review-001.md](../reactor-realism-review-001.md). Starting up the current build will currently run the core away to meltdown.
> ⚠️ **粵語 —** 反應堆喺冷停堆下保持安全，但滿載反應性校準未完成——詳見[測試報告](Reactor-Test-Report.md)同審查報告。目前建置一起動就會令爐心失控走向熔毀。

---

## 2. Power ascension & grid sync · 升功率與併網

1. Raise power on the regulating bank (or **Auto rod control** to a power setpoint), keeping Tavg on its program. · 用調節棒（或**自動棒控制**追蹤功率設定點）升功率，保持 Tavg 跟程序。
2. As steam pressure builds, roll the turbine to **1800 rpm** (4-pole / 60 Hz), then **close the generator breaker** to sync. · 蒸汽壓力建立後，將渦輪升到 **1800 rpm**（4 極／60 Hz），再**合上發電機斷路器**併網。
3. Raise turbine load; the **keep-awake pill** turns gold and the PC will not sleep while you're on-load. · 加渦輪負載；**喚醒指示**變金色，併網期間電腦唔會睡眠。

---

## 3. Normal at-power operation · 正常滿載運行

- Hold Tavg on program with the regulating bank; trim **boron** for slow reactivity (xenon burn-in / out over hours). · 用調節棒保持 Tavg 跟程序；用**硼**補償慢速反應性（氙喺數小時內累積／燒去）。
- Watch **axial offset** and keep gauges inside their green bands. · 睇**軸向偏差**，保持儀錶喺綠帶內。

---

## 4. Normal shutdown · 正常停堆

1. Ramp turbine load down, **open the generator breaker**. · 減渦輪負載、**打開發電機斷路器**解列。
2. Insert the regulating bank / borate to bring power down. · 插入調節棒／加硼降功率。
3. Trip when subcritical and cooled per program. · 達次臨界並按程序冷卻後停堆。

---

**EN —** For emergency response (SCRAM, E-0, meltdown recovery, scenario drills) see [Emergencies & Scenarios](Reactor-Emergencies-and-Scenarios.md). · **粵語 —** 緊急應對（SCRAM、E-0、熔毀復原、情景演習）見[緊急與情景](Reactor-Emergencies-and-Scenarios.md)頁。

---

### Reactor pages · 反應堆頁面導覽
[🏠 Reactor Hub · 反應堆總覽](Nuclear-Reactor.md) · [Overview · 總覽](Reactor-Overview.md) · [Control Room · 控制室](Reactor-Control-Room.md) · [Operating Procedures · 操作程序](Reactor-Operating-Procedures.md) · [Emergencies & Scenarios · 緊急與情景](Reactor-Emergencies-and-Scenarios.md) · [Fuel & Waste · 燃料與廢料](Reactor-Fuel-and-Waste.md) · [Water Treatment · 水處理](Reactor-Water-Treatment.md) · [Safety & Integrations · 安全與整合](Reactor-Safety-and-Integrations.md) · [Operating Manual · 操作手冊](Nuclear-Reactor-Operating-Manual.md) · [Test Report · 測試報告](Reactor-Test-Report.md)

*English + 繁體中文／粵語*
