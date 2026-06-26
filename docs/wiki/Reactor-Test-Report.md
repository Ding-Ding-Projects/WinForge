# Reactor Test Report · 反應堆測試報告

**EN —** Standalone scenario test suite for the reactor engine and the fuel / waste / water services, run headless against the real C# code. Harness: `tests/ReactorSim.Tests` — run with `dotnet run --project tests/ReactorSim.Tests`.

**粵語 —** 反應堆引擎同燃料／廢料／水服務嘅獨立情景測試套件，針對真實 C# 程式碼以無介面方式運行。測試框架：`tests/ReactorSim.Tests` — 用 `dotnet run --project tests/ReactorSim.Tests` 執行。

**Latest run · 最新運行:** 2026-06-26 · `dotnet run --project tests\ReactorSim.Tests\ReactorSim.Tests.csproj`

**Build / harness · 建置／測試框架:** 0 compile errors · 0 個編譯錯誤. **Result · 結果: 16 / 16 scenarios PASS · 16 / 16 情景通過.**

> **Headline · 重點:** **EN —** The headless reactor suite now covers every `ReactorScenario` enum value directly. SCRAM testing matches the current release-delay / gravity-drop rod model, and the `XenonRestart` scenario now preserves its axial xenon state instead of being wiped on the first ODE tick. The known P1-P3 reactivity-calibration defect is still reproduced and tracked separately. · **粵語 —** 無介面反應堆測試套件而家直接覆蓋每個 `ReactorScenario` enum 值。SCRAM 測試已配合現時嘅釋放延遲／重力落棒模型；`XenonRestart` 情景亦會保存軸向氙狀態，唔會喺第一個 ODE tick 被清走。已知 P1-P3 反應性校準缺陷仍然被重現並另行追蹤。

---

## Results · 結果

| # | Scenario · 情景 | Result · 結果 | Key evidence · 關鍵證據 |
|---|---|---|---|
| 1 | Cold-shutdown held · 冷停堆保持 | PASS · 通過 | 5 min held: stays Shutdown, power flat at source level, fuel 35 °C, no meltdown · 保持 5 分鐘：維持停堆，功率維持源中子水平，燃料 35 °C，冇熔毀 |
| 2 | Startup integrator stability · 起動積分穩定 | PASS · 通過 | finite, 0 sign-flips; known runaway reaches clamp but no NaN/oscillation · 有限值、0 次正負反轉；已知失控達上限但冇 NaN／振盪 |
| 3 | Known P1-P3 bug reproduced · 重現已知缺陷 | PASS · 通過 | fresh core is positive-reactivity when leaving Shutdown, reproducing the tracked calibration defect · 一離開停堆即呈正反應性，重現已追蹤校準缺陷 |
| 4 | SCRAM mechanism · 緊急停堆機構 | PASS · 通過 | trip latches, release delay holds, rods start dropping over the next second · 跳脫鎖定、釋放延遲成立、控制棒於下一秒開始下插 |
| 5 | SCRAM cannot hold due P1-P3 symptom · 停堆仍受 P1-P3 症狀影響 | PASS · 通過 | fully-rodded tripped core still melts, intentionally documenting the calibration symptom · 全棒插入且已跳脫仍熔毀，刻意記錄校準症狀 |
| 6 | Decay heat · 衰變熱 | PASS · 通過 | decay heat charges during power excursion and decays after trip · 衰變熱於功率暫態累積，跳脫後衰減 |
| 7 | Overpower auto-SCRAM · 超功率自動停堆 | PASS · 通過 | RPS auto-trips via Power Range Flux Hi · RPS 經功率區中子通量過高自動跳脫 |
| 8 | Xenon transient · 氙暫態 | PASS · 通過 | `XenonRestart` jumps to a post-trip peak and decays through the axial-node ODE · `XenonRestart` 跳至跳堆後峰值，並經軸向節點 ODE 衰減 |
| 9 | Accident scenario injection coverage · 事故情景注入覆蓋 | PASS · 通過 | all 16 `ReactorScenario` enum values exercised and asserted · 全部 16 個 `ReactorScenario` enum 值均已運行及斷言 |
| 10 | Fuel fabricate + validate / tamper · 燃料製造與驗證 | PASS · 通過 | authentic fuel validates; tampered fuel is rejected · 真品燃料驗證通過；竄改燃料被拒 |
| 11 | Load consumes file · 入料即刪檔 | PASS · 通過 | authentic assembly loads, fresh file is consumed, loaded list updates · 真品燃料組件入堆、原始檔被消耗、已載入列表更新 |
| 12 | Forged harm vs inspect · 偽冒損堆 vs 檢查 | PASS · 通過 | validate/inspect is harmless; unsafe load damages core and SCRAMs · 驗證／檢查無損；不安全入料會損堆並 SCRAM |
| 13 | Waste cap logic · 廢料上限 | PASS · 通過 | sparse-file cap test refuses write past cap without filling disk · 稀疏檔上限測試拒絕超限寫入，唔會填滿磁碟 |
| 14 | Waste safety floor · 磁碟安全下限 | PASS · 通過 | free-space floor blocks waste write safely · 剩餘空間安全下限安全阻止廢料寫入 |
| 15 | Water chemistry · 水質 | PASS · 通過 | treatment train drives conductivity to ultrapure range and tank inventory rises · 水處理列車令電導率達超純範圍，水箱存量上升 |
| 16 | Water tank empty · 水箱耗盡 | PASS · 通過 | makeup availability degrades and low-tank alarm gates plant-side availability · 補給可用率劣化，低水位警報限制機組側可用性 |

---

## Audit Changes · 審核變更

- The test project now links the current pure-C# reactor dependencies: core models, localization helpers, electrical model, and reactivity meter. · 測試專案已連結現行純 C# 反應堆依賴：核心模型、本地化 helper、電氣模型、反應性儀。
- SCRAM assertions now test release delay and rod motion rather than expecting rods to snap to 100% insertion on the same tick. · SCRAM 斷言而家測試釋放延遲同落棒動作，而唔係假設同一 tick 即 100% 插入。
- `XenonRestart` now seeds the axial top/bottom xenon nodes so the scalar xenon peak persists into the ODE update. · `XenonRestart` 而家會設定頂／底軸向氙節點，令標量氙峰值可持續進入 ODE 更新。
- A direct scenario-injection coverage test now enumerates and asserts every reactor accident/training scenario. · 新增直接情景注入覆蓋測試，列舉並斷言每個反應堆事故／訓練情景。

---

## Known Defect · 已知缺陷

**EN —** The P1-P3 reactivity-calibration defect is intentionally still reproduced by scenarios 3 and 5: once the cold core leaves held Shutdown, the current reactivity budget can overpower full rod insertion. This is a calibration issue, not a numerical-instability issue; the integrator remains finite and deterministic.

**粵語 —** P1-P3 反應性校準缺陷仍然由情景 3 同 5 刻意重現：冷態爐心一離開保持停堆，現時反應性預算可壓過全棒插入。呢個係校準問題，唔係數值不穩定問題；積分器仍然保持有限值同可重現。

---

### Reactor pages · 反應堆頁面導覽
[Reactor Hub · 反應堆總覽](Nuclear-Reactor.md) · [Overview · 總覽](Reactor-Overview.md) · [Control Room · 控制室](Reactor-Control-Room.md) · [Operating Procedures · 操作程序](Reactor-Operating-Procedures.md) · [Emergencies & Scenarios · 緊急與情景](Reactor-Emergencies-and-Scenarios.md) · [Fuel & Waste · 燃料與廢料](Reactor-Fuel-and-Waste.md) · [Water Treatment · 水處理](Reactor-Water-Treatment.md) · [Safety & Integrations · 安全與整合](Reactor-Safety-and-Integrations.md) · [Operating Manual · 操作手冊](Nuclear-Reactor-Operating-Manual.md) · [Test Report · 測試報告](Reactor-Test-Report.md)

*English + 繁體中文／粵語*
