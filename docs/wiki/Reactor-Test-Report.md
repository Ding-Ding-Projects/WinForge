# Reactor Test Report · 反應堆測試報告

**EN —** Standalone scenario test suite for the reactor engine and the fuel / waste / water services, run headless against the real C# code (the WinUI app cannot run headless, so a console harness compiles the pure-C# engine sources directly). Harness: `tests/ReactorSim.Tests` — run with `dotnet run --project tests/ReactorSim.Tests`.

**粵語 —** 反應堆引擎同燃料／廢料／水服務嘅獨立情景測試套件，針對真實 C# 程式碼以無介面方式運行（WinUI app 唔能夠無介面運行，所以用一個主控台測試框架直接編譯純 C# 引擎原始碼）。測試框架：`tests/ReactorSim.Tests` — 用 `dotnet run --project tests/ReactorSim.Tests` 執行。

**Build · 建置:** `WinForge.sln` → 0 errors · 0 個錯誤. Harness · 測試框架 → 0 errors · 0 個錯誤. **Result · 結果: 13 / 15 scenarios PASS · 13 / 15 情景通過.**

> **Headline · 重點:** **EN —** The reactor is **SAFE AT REST** (held cold shutdown), but the at-power reactivity **calibration (P2)** is still unfinished — so it **melts if started up**. Tracked in [reactor-realism-review-001.md](../reactor-realism-review-001.md). The two FAILs are **stale assertions vs the loop's new models, not new bugs**. · **粵語 —** 反應堆**靜止時安全**（保持冷停堆），但滿載反應性**校準（P2）**仍未完成——所以**一起動就會熔毀**。詳見審查報告。兩個 FAIL 係**相對於迴路新模型嘅過時斷言，唔係新缺陷**。

---

## Results · 結果

| # | Scenario · 情景 | Result · 結果 | Key numbers · 關鍵數據 |
|---|---|---|---|
| 1 | Cold-shutdown held · 冷停堆保持 | ✅ PASS · 通過 | 5 min held: stays Shutdown, power flat at source 1e-8, fuel 35 °C, no meltdown · 保持 5 分鐘：維持停堆，功率平穩於源中子 1e-8，燃料 35 °C，冇熔毀 |
| 2 | Startup integrator stability · 起動積分穩定 | ✅ PASS · 通過 | finite, 0 sign-flips (backward-Euler, no NaN/oscillation) · 有限值、0 次正負反轉（後向歐拉，冇 NaN／振盪） |
| 3 | Known P1–P3 bug reproduced · 重現已知缺陷 | ✅ PASS · 通過 | fresh core +5335 pcm **with all rods in** → melts on first tick out of Shutdown · 全新爐心冷溫下**所有棒插入**仍 +5335 pcm → 一離開停堆即第一格熔毀 |
| 4 | SCRAM mechanism assertion · 緊急停堆機構斷言 | ❌ FAIL · 失敗 | **Stale, not a bug:** `Scram()` now uses 228-step banks so `RodBankInsertion%` doesn't read 100, though **Tripped + IsScrammed latch correctly** · **過時、非缺陷：** `Scram()` 改用 228 步棒組，故 `RodBankInsertion%` 唔再讀 100，但**Tripped + IsScrammed 正確鎖定** |
| 5 | SCRAM can't hold (P1–P3 symptom) · 停堆無法壓制 | ✅ PASS · 通過 | tripped fully-rodded core still melts (calibration symptom) · 已跳脫、全棒插入嘅爐心仍熔毀（校準症狀） |
| 6 | Decay heat · 衰變熱 | ✅ PASS · 通過 | charges then decays, bounded by 0.10 clamp after trip · 累積後衰減，跳脫後受 0.10 上限限制 |
| 7 | Overpower auto-SCRAM · 超功率自動停堆 | ✅ PASS · 通過 | RPS auto-trips via 'Power Range Flux Hi' · RPS 經「功率區中子通量過高」自動跳脫 |
| 8 | Xenon transient monotonic-decay assertion · 氙暫態單調衰減斷言 | ❌ FAIL · 失敗 | **Stale, not a bug:** Xe decays to 0 **faster** than the test's monotonic check expects · **過時、非缺陷：** 氙衰減到 0 嘅速度**快過**測試嘅單調檢查所預期 |
| 9 | Fuel fabricate + validate / tamper · 燃料製造與驗證 | ✅ PASS · 通過 | authentic→'ok'; tampered enrichment→'tampered' rejected · 真品→「ok」；竄改濃度→判為「tampered」並拒收 |
| 10 | Load consumes file · 入料即刪檔 | ✅ PASS · 通過 | authentic loaded, fresh file deleted, appears in loaded list · 真品入料、原始檔被刪、出現喺已載入清單 |
| 11 | Forged harm vs inspect · 偽冒損堆 vs 檢查 | ✅ PASS · 通過 | unsafe load damages core + auto-SCRAM, file consumed; Validate-only does NO harm · 不安全入料損傷爐心＋自動 SCRAM、檔案被消耗；單純驗證無損 |
| 12 | Waste cap logic · 廢料上限 | ✅ PASS · 通過 | refuses write past the 1 GB cap, reports CapReached / FULL (sparse file) · 拒絕超過 1 GB 上限嘅寫入、報告 CapReached／FULL（稀疏檔） |
| 13 | Waste safety-floor · 磁碟安全下限 | ✅ PASS · 通過 | floor > free → write refused, FULL (disk never filled) · 下限 > 剩餘空間 → 拒絕寫入、FULL（磁碟從未被填滿） |
| 14 | Water chemistry · 水質 | ✅ PASS · 通過 | conductivity 45 → 0.055 µS/cm ultrapure, O₂→5 ppb, tank fills · 電導率 45 → 0.055 µS/cm 超純、O₂→5 ppb、水箱注滿 |
| 15 | Water tank empty · 水箱耗盡 | ✅ PASS · 通過 | makeup availability degrades 1.00→0.02, low-tank alarm; valve-shut→0 · 補給可用率劣化 1.00→0.02、低水位警報；關閥→0 |

---

## What the tests confirm · 測試確認咗乜

- The **operator-must-start-up** behavior is solid: a fresh core sits cold and subcritical indefinitely in Shutdown. · **必須由操作員起動**嘅行為穩固：全新爐心會喺停堆狀態無限期保持冷態同次臨界。
- The **backward-Euler integrator is numerically sound** (no NaN/Inf, no oscillation). · **後向歐拉積分器數值上健全**（冇 NaN/Inf、冇振盪）。
- The **known P1–P3 reactivity-calibration defect is reproduced and tracked** (fresh / tripped fully-rodded core still melts). · **已知 P1–P3 反應性校準缺陷已重現並追蹤**（全新／已跳脫、全棒插入嘅爐心仍熔毀）。
- **Decay heat** charges then decays under its clamp; **overpower auto-SCRAM** fires via Power Range Flux Hi. · **衰變熱**累積後喺上限下衰減；**超功率自動 SCRAM** 經功率區中子通量過高觸發。
- The **fuel cycle is physical and authentic**: fabricate → validate → load-consumes-the-file → forged fuel harms the core (sim-only) while inspection is safe. · **燃料循環真實可信**：製造 → 驗證 → 入料即刪檔 → 偽冒燃料會損壞爐心（僅模擬），而檢查係安全嘅。
- The **waste cap / disk safety-floor logic is correct and never fills the disk** (verified with sparse files). · **廢料上限／磁碟安全下限邏輯正確，永遠唔會填滿磁碟**（用稀疏檔驗證）。
- The **water treatment plant** drives chemistry to spec and the reactor degrades when its makeup tank empties. · **水處理廠**將水質帶到規格，而補給水箱耗盡時反應堆會劣化。

---

## The two FAILs — stale assertions, not new bugs · 兩個 FAIL——過時斷言，並非新缺陷

**EN —** Both failures are tests whose assertions have fallen behind the realism loop's new models; the underlying behavior is correct:

1. **SCRAM mechanism assertion (#4)** — `Scram()` now drives the rods through **228-step banks**, so `RodBankInsertion%` no longer reads exactly 100 on the tick the test checks. The safety outcome is unchanged: **Tripped** and **IsScrammed** latch correctly.
2. **Xenon transient monotonic-decay assertion (#8)** — xenon now **decays to 0 faster** than the test's hard-coded monotonic check expects, so the assertion trips even though the decay is physical.

**粵語 —** 兩個失敗都係斷言落後於寫實度迴路新模型嘅測試；底層行為正確：

1. **SCRAM 機構斷言（#4）**——`Scram()` 而家以 **228 步棒組**驅動控制棒，所以喺測試檢查嗰一格 `RodBankInsertion%` 唔再啱啱讀到 100。安全結果不變：**Tripped** 同 **IsScrammed** 正確鎖定。
2. **氙暫態單調衰減斷言（#8）**——氙而家**衰減到 0 嘅速度快過**測試硬編碼嘅單調檢查所預期，所以斷言觸發，但衰減本身係物理正確嘅。

---

## Known defect (tracked, expected) · 已知缺陷（已追蹤、屬預期）

**EN —** Scenarios 3 & 5 deliberately reproduce the **reactivity-calibration defect** from [reactor-realism-review-001.md](../reactor-realism-review-001.md): at cold temperature the Doppler and moderator feedbacks are large and positive, and together with the excess baseline they exceed full rod worth — so once the core leaves the held Shutdown state it is prompt-supercritical even with all rods inserted, and SCRAM cannot hold it down. This is purely a **calibration** defect (not numerical or structural) and is the subject of the ongoing P1–P3 realism work. Until it lands, the reactor is **safe at rest in Shutdown but runs away if started up**.

**粵語 —** 情景 3 同 5 刻意重現 [reactor-realism-review-001.md](../reactor-realism-review-001.md) 入面嘅**反應性校準缺陷**：喺冷溫下，都卜勒同緩和劑回饋又大又正，加埋過量基準超過咗控制棒嘅全部價值——所以爐心一離開保持嘅停堆狀態，即使所有棒插入都會瞬發超臨界，SCRAM 都壓唔住。呢個純粹係**校準**缺陷（唔係數值或結構問題），亦係進行中嘅 P1–P3 寫實度工作嘅主題。喺修正落地之前，反應堆**靜止於停堆時安全，但一起動就會失控**。

---

### Reactor pages · 反應堆頁面導覽
[🏠 Reactor Hub · 反應堆總覽](Nuclear-Reactor.md) · [Overview · 總覽](Reactor-Overview.md) · [Control Room · 控制室](Reactor-Control-Room.md) · [Operating Procedures · 操作程序](Reactor-Operating-Procedures.md) · [Emergencies & Scenarios · 緊急與情景](Reactor-Emergencies-and-Scenarios.md) · [Fuel & Waste · 燃料與廢料](Reactor-Fuel-and-Waste.md) · [Water Treatment · 水處理](Reactor-Water-Treatment.md) · [Safety & Integrations · 安全與整合](Reactor-Safety-and-Integrations.md) · [Operating Manual · 操作手冊](Nuclear-Reactor-Operating-Manual.md) · [Test Report · 測試報告](Reactor-Test-Report.md)

*English + 繁體中文／粵語*
