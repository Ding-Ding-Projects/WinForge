# Reactor Test Report · 反應堆測試報告

**EN —** Standalone scenario test suite for the reactor engine and the fuel / waste / water services, run headless against the real C# code (the WinUI app cannot run headless, so a console harness compiles the pure-C# engine sources directly). Harness: `tests/ReactorSim.Tests` — run with `dotnet run --project tests/ReactorSim.Tests`.

**粵語 —** 反應堆引擎同燃料／廢料／水服務嘅獨立情景測試套件，針對真實 C# 程式碼以無介面方式運行（WinUI app 唔能夠無介面運行，所以用一個主控台測試框架直接編譯純 C# 引擎原始碼）。測試框架：`tests/ReactorSim.Tests` — 用 `dotnet run --project tests/ReactorSim.Tests` 執行。

**Build · 建置:** `WinForge.sln` → 0 errors · 0 個錯誤. Harness · 測試框架 → 0 errors · 0 個錯誤. **Result · 結果: 15 / 15 scenarios PASS · 15 / 15 情景通過.**

| # | Scenario · 情景 | Result · 結果 | Key numbers · 關鍵數據 |
|---|---|---|---|
| 1 | Cold-shutdown held · 冷停堆保持 | ✅ PASS · 通過 | 5 min held: stays Shutdown, power flat at source 1e-8, fuel 35 °C, no meltdown · 保持 5 分鐘：維持停堆，功率平穩於源中子 1e-8，燃料 35 °C，冇熔毀 |
| 2 | Startup integrator stability · 起動積分穩定 | ✅ PASS · 通過 | finite, 0 sign-flips (backward-Euler does not oscillate) · 有限值、0 次正負反轉（後向歐拉唔會振盪） |
| 3 | Known P1–P3 bug reproduced · 重現已知缺陷 | ✅ PASS · 通過 | fresh core→Run: ρ = +5335 pcm **with all rods in** at cold temp → meltdown in 1 tick · 全新爐心→運行：冷溫下**所有棒插入**仍有 ρ = +5335 pcm → 一格內熔毀 |
| 4 | SCRAM mechanism · 緊急停堆機構 | ✅ PASS · 通過 | rods 10%→100%, Mode=Tripped, IsScrammed latched · 控制棒 10%→100%、模式＝跳脫、IsScrammed 鎖定 |
| 5 | SCRAM can't hold (P1–P3 symptom) · 停堆無法壓制 | ✅ PASS · 通過 | tripped fully-rodded core still runs kinetics → meltdown · 已跳脫、全棒插入嘅爐心仍運行動力學 → 熔毀 |
| 6 | Decay heat · 衰變熱 | ✅ PASS · 通過 | charges to 0.10 clamp, bounded & decaying after trip · 累積至 0.10 上限，跳脫後受限並衰減 |
| 7 | Overpower auto-SCRAM · 超功率自動停堆 | ✅ PASS · 通過 | RPS auto-trips via 'Power Range Flux Hi' · RPS 經「功率區中子通量過高」自動跳脫 |
| 8 | Xenon transient · 氙暫態 | ✅ PASS · 通過 | restart jump Xe 2.60 → decays 2.60→2.41→2.24 over 2 h · 重起時氙跳升至 2.60 → 2 小時內衰減 2.60→2.41→2.24 |
| 9 | Fuel fabricate + validate / tamper · 燃料製造與驗證 | ✅ PASS · 通過 | authentic→'ok'; tampered enrichment→'tampered' rejected · 真品→「ok」；竄改濃度→判為「tampered」並拒收 |
| 10 | Load consumes file · 入料即刪檔 | ✅ PASS · 通過 | authentic loaded, fresh file deleted, appears in loaded list · 真品入料、原始檔被刪、出現喺已載入清單 |
| 11 | Forged harm vs inspect · 偽冒損堆 vs 檢查 | ✅ PASS · 通過 | unsafe load sev=0.70: dmg 0→8.4, auto-SCRAM, rad=0.35, file consumed; Validate-only does NO harm · 不安全入料 sev=0.70：損傷 0→8.4、自動 SCRAM、輻射=0.35、檔案被消耗；單純驗證無損 |
| 12 | Waste cap logic · 廢料上限 | ✅ PASS · 通過 | 1 GB cap → 100 MB write refused, CapReached, FULL (tested via sparse file) · 1 GB 上限 → 拒絕 100 MB 寫入、CapReached、FULL（用稀疏檔測試） |
| 13 | Waste safety-floor · 磁碟安全下限 | ✅ PASS · 通過 | floor > free → write refused, FULL (disk never filled) · 下限 > 剩餘空間 → 拒絕寫入、FULL（磁碟從未被填滿） |
| 14 | Water chemistry · 水質 | ✅ PASS · 通過 | conductivity 45→0.055 µS/cm, O₂→5 ppb, tank fills, product 889 L/min · 電導率 45→0.055 µS/cm、O₂→5 ppb、水箱注滿、產水 889 L/min |
| 15 | Water tank empty · 水箱耗盡 | ✅ PASS · 通過 | availability 1.00→0.02, low-tank alarm; valve-shut→0 · 可用率 1.00→0.02、低水位警報；關閥→0 |

## What the tests confirm · 測試確認咗乜
- The **operator-must-start-up** behavior is solid: a fresh core sits cold and subcritical indefinitely in Shutdown. · **必須由操作員起動**嘅行為穩固：全新爐心會喺停堆狀態無限期保持冷態同次臨界。
- The **backward-Euler integrator is numerically sound** (no NaN/Inf, no oscillation). · **後向歐拉積分器數值上健全**（冇 NaN/Inf、冇振盪）。
- The **fuel cycle is physical and authentic**: fabricate → validate → load-consumes-the-file → forged fuel harms the core (sim-only) while inspection is safe. · **燃料循環真實可信**：製造 → 驗證 → 入料即刪檔 → 偽冒燃料會損壞爐心（僅模擬），而檢查係安全嘅。
- The **waste cap / disk safety-floor logic is correct and never fills the disk** (verified with sparse files — no real multi-GB writes in tests). · **廢料上限／磁碟安全下限邏輯正確，永遠唔會填滿磁碟**（用稀疏檔驗證 — 測試中冇真正寫入幾 GB）。
- The **water treatment plant** drives chemistry to spec and the reactor degrades when its makeup tank empties. · **水處理廠**將水質帶到規格，而補給水箱耗盡時反應堆會劣化。

## Known defect (tracked, expected) · 已知缺陷（已追蹤、屬預期）
**EN —** Scenarios 3 & 5 deliberately reproduce the **reactivity-calibration defect** from [reactor-realism-review-001.md](../reactor-realism-review-001.md): at cold temperature the Doppler (datum 600 °C) and moderator (datum 305 °C) feedbacks are large and positive, and together with `ExcessBaseline` (+0.0914 dk/k) they exceed full rod worth — so once the core leaves the held Shutdown state it is prompt-supercritical even with all rods inserted, and SCRAM cannot hold it down. This is purely a **calibration** defect (not a numerical or structural one) and is the subject of the ongoing P1–P3 realism work (`reactor-realism-loop`). Until it lands, the reactor is safe to view in Shutdown but runs away if started up.

**粵語 —** 情景 3 同 5 刻意重現 [reactor-realism-review-001.md](../reactor-realism-review-001.md) 入面嘅**反應性校準缺陷**：喺冷溫下，都卜勒（基準 600 °C）同緩和劑（基準 305 °C）回饋又大又正，加埋 `ExcessBaseline`（+0.0914 dk/k）超過咗控制棒嘅全部價值 — 所以爐心一離開保持嘅停堆狀態，即使所有棒插入都會瞬發超臨界，SCRAM 都壓唔住。呢個純粹係**校準**缺陷（唔係數值或結構問題），亦係進行中嘅 P1–P3 寫實度工作（`reactor-realism-loop`）嘅主題。喺修正落地之前，喺停堆狀態下觀看反應堆係安全嘅，但一起動就會失控。

## Not headless-testable · 無法以無介面測試
**EN —** The reactor-side makeup-water coupling (pressurizer/SG level sag, low-makeup alarm) lives only in the at-power `Update()` path, which the P1–P3 runaway prevents reaching headlessly; the water-plant side (availability degradation) is tested instead, and the gap is documented rather than faked.

**粵語 —** 反應堆側嘅補給水耦合（穩壓器／蒸汽產生器水位下降、低補給警報）只存在於滿載嘅 `Update()` 路徑，而 P1–P3 失控令無介面測試無法到達該路徑；改為測試水廠側（可用率劣化），而呢個缺口係如實記錄而非造假。
